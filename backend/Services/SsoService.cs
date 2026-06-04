using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using backend.Data;
using backend.DTOs;
using backend.Models;

namespace backend.Services;

public class SsoService
{
    private readonly AppDbContext _db;
    private readonly IConnectionMultiplexer _redis;
    private readonly AuthService _auth;

    private const string AuthCodeChars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    public SsoService(AppDbContext db, IConnectionMultiplexer redis, AuthService auth)
    {
        _db = db;
        _redis = redis;
        _auth = auth;
    }

    // ==================== 公共辅助方法 ====================

    private async Task<AuthResponse> IssueJwtForUserAsync(User user)
    {
        var (jwt, permissions, roleIds) = await _auth.GenerateJwtTokenAsync(user);
        await _auth.CacheTokenAsync(user.Id, jwt);

        return new AuthResponse
        {
            Token = jwt,
            Username = user.Username,
            Email = user.Email,
            Permissions = permissions,
            RoleIds = roleIds
        };
    }

    // ==================== 魔法链接 SSO ====================

    public async Task<SsoTokenResponse> CreateTokenAsync(int userId, int expireHours, int createdByAdminId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new InvalidOperationException("用户不存在");

        if (user.IsFrozen)
            throw new InvalidOperationException("该用户已被冻结，无法生成SSO链接");

        // 生成密码学随机 Token（64字符，URL安全）
        var bytes = RandomNumberGenerator.GetBytes(48);
        var token = Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");

        var expiresAt = DateTime.Now.AddHours(expireHours);

        var ssoToken = new SsoToken
        {
            Token = token,
            Type = "link",
            UserId = userId,
            CreatedBy = createdByAdminId,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.Now
        };

        _db.SsoTokens.Add(ssoToken);
        await _db.SaveChangesAsync();

        // 缓存到 Redis（加速验证）
        try
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync($"sso_token:{token}", userId.ToString(), TimeSpan.FromHours(expireHours));
        }
        catch { /* Redis 不可用时不影响功能 */ }

        var createdByAdmin = await _db.Users.FirstOrDefaultAsync(u => u.Id == createdByAdminId);

        return new SsoTokenResponse
        {
            Id = ssoToken.Id,
            Token = token,
            Type = "link",
            Username = user.Username,
            Email = user.Email,
            ExpiresAt = expiresAt,
            LoginLink = $"http://localhost:5173/?sso_token={token}",
            CreatedAt = ssoToken.CreatedAt,
            CreatedByAdmin = createdByAdmin?.Username ?? "unknown"
        };
    }

    public async Task<AuthResponse> LoginByTokenAsync(string token)
    {
        // 快速路径：Redis 检查
        try
        {
            var db = _redis.GetDatabase();
            if (!await db.KeyExistsAsync($"sso_token:{token}"))
            {
                // Redis 未命中，检查是否是 Redis 故障导致的
                // 继续走 DB 查询
            }
        }
        catch { /* Redis 不可用 */ }

        // 数据库验证
        var ssoToken = await _db.SsoTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token);

        if (ssoToken == null)
            throw new InvalidOperationException("SSO链接无效");

        if (ssoToken.IsUsed)
            throw new InvalidOperationException("该SSO链接已被使用");

        if (ssoToken.ExpiresAt <= DateTime.Now)
            throw new InvalidOperationException("SSO链接已过期");

        var user = ssoToken.User;
        if (user.IsFrozen)
            throw new InvalidOperationException("该用户已被冻结，请联系管理员");

        // 先标记为已用（防止并发重复使用）
        ssoToken.IsUsed = true;
        ssoToken.UsedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        // 清理 Redis
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync($"sso_token:{token}");
        }
        catch { }

        return await IssueJwtForUserAsync(user);
    }

    // ==================== 授权码 SSO ====================

    public async Task<SsoTokenResponse> CreateAuthCodeAsync(int userId, int expireMinutes, int createdByAdminId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new InvalidOperationException("用户不存在");

        if (user.IsFrozen)
            throw new InvalidOperationException("该用户已被冻结，无法生成授权码");

        // 生成 8 位授权码（排除易混淆字符 0O1Il）
        var code = GenerateAuthCode();
        var expiresAt = DateTime.Now.AddMinutes(expireMinutes);

        var ssoToken = new SsoToken
        {
            Token = code,
            AuthCode = code,
            Type = "code",
            UserId = userId,
            CreatedBy = createdByAdminId,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.Now
        };

        _db.SsoTokens.Add(ssoToken);
        await _db.SaveChangesAsync();

        // 缓存到 Redis
        try
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync($"sso_auth_code:{code}", userId.ToString(), TimeSpan.FromMinutes(expireMinutes));
        }
        catch { }

        var createdByAdmin = await _db.Users.FirstOrDefaultAsync(u => u.Id == createdByAdminId);

        return new SsoTokenResponse
        {
            Id = ssoToken.Id,
            AuthCode = code,
            Type = "code",
            Username = user.Username,
            Email = user.Email,
            ExpiresAt = expiresAt,
            LoginLink = "",
            CreatedAt = ssoToken.CreatedAt,
            CreatedByAdmin = createdByAdmin?.Username ?? "unknown"
        };
    }

    public async Task<AuthResponse> LoginByAuthCodeAsync(string code)
    {
        // 快速路径：Redis 检查
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyExistsAsync($"sso_auth_code:{code}");
        }
        catch { }

        // 数据库验证
        var ssoToken = await _db.SsoTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.AuthCode == code);

        if (ssoToken == null)
            throw new InvalidOperationException("授权码无效");

        if (ssoToken.IsUsed)
            throw new InvalidOperationException("该授权码已被使用");

        if (ssoToken.ExpiresAt <= DateTime.Now)
            throw new InvalidOperationException("授权码已过期");

        var user = ssoToken.User;
        if (user.IsFrozen)
            throw new InvalidOperationException("该用户已被冻结，请联系管理员");

        // 先标记为已用（防止并发重复使用）
        ssoToken.IsUsed = true;
        ssoToken.UsedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        // 清理 Redis
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync($"sso_auth_code:{code}");
        }
        catch { }

        return await IssueJwtForUserAsync(user);
    }

    private static string GenerateAuthCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        var chars = new char[8];
        for (int i = 0; i < 8; i++)
        {
            chars[i] = AuthCodeChars[bytes[i] % AuthCodeChars.Length];
        }
        return new string(chars);
    }

    // ==================== 通用管理 ====================

    public async Task<List<SsoTokenResponse>> GetTokensAsync(int page = 1, int pageSize = 20, string? type = null)
    {
        var query = _db.SsoTokens
            .Include(t => t.User)
            .Include(t => t.CreatedByAdmin)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(t => t.Type == type);
        }

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new SsoTokenResponse
            {
                Id = t.Id,
                Token = t.Token != null && !t.IsUsed ? t.Token.Substring(0, 8) + "****" : "****",
                AuthCode = t.AuthCode != null && !t.IsUsed ? t.AuthCode : "****",
                Type = t.Type,
                Username = t.User.Username,
                Email = t.User.Email,
                ExpiresAt = t.ExpiresAt,
                IsUsed = t.IsUsed,
                UsedAt = t.UsedAt,
                CreatedAt = t.CreatedAt,
                CreatedByAdmin = t.CreatedByAdmin.Username
            })
            .ToListAsync();
    }

    public async Task RevokeTokenAsync(int tokenId, int adminId)
    {
        var ssoToken = await _db.SsoTokens.FirstOrDefaultAsync(t => t.Id == tokenId)
            ?? throw new InvalidOperationException("令牌不存在");

        if (ssoToken.IsUsed)
            throw new InvalidOperationException("该令牌已被使用，无需撤销");

        if (ssoToken.ExpiresAt <= DateTime.Now)
            throw new InvalidOperationException("该令牌已过期");

        ssoToken.IsUsed = true;
        ssoToken.UsedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        // 清理 Redis
        try
        {
            var db = _redis.GetDatabase();
            if (!string.IsNullOrEmpty(ssoToken.Token))
                await db.KeyDeleteAsync($"sso_token:{ssoToken.Token}");
            if (!string.IsNullOrEmpty(ssoToken.AuthCode))
                await db.KeyDeleteAsync($"sso_auth_code:{ssoToken.AuthCode}");
        }
        catch { }
    }
}
