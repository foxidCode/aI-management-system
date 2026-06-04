using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using backend.Data;
using backend.DTOs;
using backend.Models;

namespace backend.Services;

public class OAuthService
{
    private readonly AppDbContext _db;
    private readonly IConnectionMultiplexer _redis;
    private readonly AuthService _auth;
    private readonly IConfiguration _config;

    public OAuthService(AppDbContext db, IConnectionMultiplexer redis, AuthService auth, IConfiguration config)
    {
        _db = db;
        _redis = redis;
        _auth = auth;
        _config = config;
    }

    // ==================== 授权码生成 ====================

    public async Task<string> GenerateAuthorizationCodeAsync(
        int userId, int clientId, string redirectUri,
        string? codeChallenge, string? codeChallengeMethod,
        string scopes, string? nonce)
    {
        // 生成 256 位随机授权码
        var rawCode = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
        var codeHash = HashToken(rawCode);

        var lifetime = _config.GetValue<int>("OAuth:AuthorizationCodeLifetimeSeconds", 60);

        var authCode = new AuthorizationCode
        {
            Code = codeHash,
            UserId = userId,
            ClientId = clientId,
            RedirectUri = redirectUri,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod,
            Scopes = scopes,
            Nonce = nonce,
            IsUsed = false,
            ExpiresAt = DateTime.Now.AddSeconds(lifetime),
            CreatedAt = DateTime.Now
        };

        _db.AuthorizationCodes.Add(authCode);
        await _db.SaveChangesAsync();

        return rawCode;
    }

    // ==================== Token 交换 ====================

    public async Task<TokenResponse> ExchangeCodeAsync(
        string code, string? codeVerifier, string clientId, string redirectUri)
    {
        // 查找并验证授权码
        var codeHash = HashToken(code);
        var authCode = await _db.AuthorizationCodes
            .Include(a => a.User)
            .Include(a => a.Client)
            .FirstOrDefaultAsync(a => a.Code == codeHash);

        if (authCode == null)
            throw new InvalidOperationException("invalid_grant", new Exception("授权码无效"));

        if (authCode.IsUsed)
            throw new InvalidOperationException("invalid_grant", new Exception("授权码已被使用"));

        if (authCode.ExpiresAt <= DateTime.Now)
            throw new InvalidOperationException("invalid_grant", new Exception("授权码已过期"));

        if (authCode.Client.ClientId != clientId)
            throw new InvalidOperationException("invalid_grant", new Exception("client_id 不匹配"));

        if (!string.Equals(authCode.RedirectUri, redirectUri, StringComparison.Ordinal))
            throw new InvalidOperationException("invalid_grant", new Exception("redirect_uri 不匹配"));

        // PKCE 验证
        if (!string.IsNullOrEmpty(authCode.CodeChallenge))
        {
            if (string.IsNullOrEmpty(codeVerifier))
                throw new InvalidOperationException("invalid_grant", new Exception("缺少 code_verifier"));

            var expectedChallenge = ComputeS256Challenge(codeVerifier);
            if (authCode.CodeChallenge != expectedChallenge)
                throw new InvalidOperationException("invalid_grant", new Exception("code_verifier 验证失败"));
        }

        var user = authCode.User;
        var client = authCode.Client;

        if (user.IsFrozen)
            throw new InvalidOperationException("invalid_grant", new Exception("用户已被冻结"));

        // 标记授权码为已使用（防并发重用）
        authCode.IsUsed = true;
        authCode.UsedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return await IssueTokensAsync(user, client, authCode.Scopes, authCode.Nonce);
    }

    public async Task<TokenResponse> ExchangeRefreshTokenAsync(string refreshToken, string clientId)
    {
        var tokenHash = HashToken(refreshToken);
        var storedToken = await _db.RefreshTokens
            .Include(r => r.User)
            .Include(r => r.Client)
            .FirstOrDefaultAsync(r => r.Token == tokenHash);

        if (storedToken == null)
            throw new InvalidOperationException("invalid_grant", new Exception("刷新令牌无效"));

        if (storedToken.IsRevoked)
            throw new InvalidOperationException("invalid_grant", new Exception("刷新令牌已被撤销"));

        if (storedToken.ExpiresAt <= DateTime.Now)
            throw new InvalidOperationException("invalid_grant", new Exception("刷新令牌已过期"));

        if (storedToken.Client.ClientId != clientId)
            throw new InvalidOperationException("invalid_grant", new Exception("client_id 不匹配"));

        var user = storedToken.User;
        var client = storedToken.Client;

        if (user.IsFrozen)
            throw new InvalidOperationException("invalid_grant", new Exception("用户已被冻结"));

        // 轮换：撤销旧令牌
        if (_config.GetValue<bool>("OAuth:RefreshTokenRotationEnabled", true))
        {
            storedToken.IsRevoked = true;
            storedToken.RevokedAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }

        return await IssueTokensAsync(user, client, storedToken.Scopes, null);
    }

    // ==================== Token 撤销 ====================

    public async Task RevokeTokenAsync(string token, string? tokenTypeHint)
    {
        if (tokenTypeHint == "refresh_token" || tokenTypeHint == null)
        {
            var tokenHash = HashToken(token);
            var storedToken = await _db.RefreshTokens
                .FirstOrDefaultAsync(r => r.Token == tokenHash);

            if (storedToken != null && !storedToken.IsRevoked)
            {
                storedToken.IsRevoked = true;
                storedToken.RevokedAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }
        }

        if (tokenTypeHint == "access_token" || tokenTypeHint == null)
        {
            // 在 Redis 中标记 access_token 为失效
            try
            {
                var db = _redis.GetDatabase();
                await db.StringSetAsync($"revoked_token:{token}", "1", TimeSpan.FromHours(2));
            }
            catch { }
        }
    }

    // ==================== 客户端验证 ====================

    public async Task<OAuthClient?> ValidateClientAsync(string clientId, string? redirectUri, string? scopes)
    {
        var client = await _db.OAuthClients
            .FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive);

        if (client == null) return null;

        // 验证 redirect_uri
        if (!string.IsNullOrEmpty(redirectUri))
        {
            var uris = JsonSerializer.Deserialize<List<string>>(client.RedirectUris) ?? new();
            if (!uris.Contains(redirectUri)) return null;
        }

        return client;
    }

    public bool ValidateScopes(OAuthClient client, string? scopes)
    {
        if (string.IsNullOrWhiteSpace(scopes)) return true;
        var allowed = client.AllowedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var requested = scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return requested.All(s => allowed.Contains(s));
    }

    // ==================== 私有辅助 ====================

    private async Task<TokenResponse> IssueTokensAsync(
        User user, OAuthClient client, string scopes, string? nonce)
    {
        // 生成 access_token（HMAC-SHA256 JWT）
        var accessTokenLifetime = _config.GetValue<int>("OAuth:AccessTokenLifetimeHours", 2);
        var (jwt, permissions, roleIds) = await _auth.GenerateJwtTokenWithLifetimeAsync(user, accessTokenLifetime);
        await _auth.CacheTokenAsync(user.Id, jwt);

        // 生成 refresh_token
        var rawRefresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
        var refreshLifetime = _config.GetValue<int>("OAuth:RefreshTokenLifetimeDays", 30);

        var refreshToken = new RefreshToken
        {
            Token = HashToken(rawRefresh),
            UserId = user.Id,
            ClientId = client.Id,
            Scopes = scopes,
            IsRevoked = false,
            ExpiresAt = DateTime.Now.AddDays(refreshLifetime),
            CreatedAt = DateTime.Now
        };
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        return new TokenResponse
        {
            AccessToken = jwt,
            TokenType = "Bearer",
            ExpiresIn = accessTokenLifetime * 3600,
            RefreshToken = rawRefresh,
            // id_token 由 OidcService 在 OAuthController 中调用生成
        };
    }

    /// <summary>
    /// 仅用于令牌端点内部：完成 access_token 后生成 id_token
    /// </summary>
    public async Task<TokenResponse> ExchangeCodeWithIdTokenAsync(
        string code, string? codeVerifier, string clientId, string redirectUri,
        OidcService oidc)
    {
        var response = await ExchangeCodeAsync(code, codeVerifier, clientId, redirectUri);

        // 从 access_token 中提取用户信息来生成 id_token
        // 简化实现：通过 refresh_token 查用户
        var tokenHash = HashToken(response.RefreshToken!);
        var storedToken = await _db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == tokenHash);

        if (storedToken != null)
        {
            // 查找 nonce（从授权码中获取）
            var authCode = await _db.AuthorizationCodes
                .Include(a => a.Client)
                .FirstOrDefaultAsync(a => a.UserId == storedToken.UserId &&
                    a.IsUsed && a.UsedAt > DateTime.Now.AddMinutes(-5));
            var nonce = authCode?.Nonce;
            var fkClientId = authCode?.Client.ClientId ?? clientId;

            var db = _redis.GetDatabase();
            var permissionsJson = await db.StringGetAsync($"token:{response.AccessToken}");
            var permissions = new List<string>();

            response.IdToken = oidc.GenerateIdToken(
                storedToken.User, fkClientId, nonce, storedToken.Scopes, permissions);
        }

        return response;
    }

    public static string HashToken(string raw)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    public static string ComputeS256Challenge(string verifier)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(verifier)))
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}
