using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using backend.Data;
using backend.DTOs;
using backend.Models;

namespace backend.Services;

public class OidcService
{
    private readonly AppDbContext _db;
    private readonly RsaKeyProvider _rsa;
    private readonly IConfiguration _config;

    public OidcService(AppDbContext db, RsaKeyProvider rsa, IConfiguration config)
    {
        _db = db;
        _rsa = rsa;
        _config = config;
    }

    public string GenerateIdToken(
        User user, string clientId, string? nonce,
        string? scopes, List<string> permissions)
    {
        var issuer = _config["OAuth:Issuer"] ?? "http://localhost:5000";
        var now = DateTime.Now;
        var lifetime = _config.GetValue<int>("OAuth:AccessTokenLifetimeHours", 2);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Iss, issuer),
            new(JwtRegisteredClaimNames.Aud, clientId),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString()),
            new(JwtRegisteredClaimNames.Exp, new DateTimeOffset(now.AddHours(lifetime)).ToUnixTimeSeconds().ToString()),
            new("name", user.Username),
            new("preferred_username", user.Username),
        };

        if (!string.IsNullOrEmpty(user.Email))
        {
            claims.Add(new(JwtRegisteredClaimNames.Email, user.Email));
            claims.Add(new("email_verified", "false"));
        }

        if (!string.IsNullOrEmpty(nonce))
        {
            claims.Add(new(JwtRegisteredClaimNames.Nonce, nonce));
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var header = new JwtHeader(_rsa.SigningCredentials)
        {
            { "kid", _rsa.Kid }
        };
        var payload = new JwtPayload(claims);

        var jwt = new JwtSecurityToken(header, payload);
        return tokenHandler.WriteToken(jwt);
    }

    public async Task<UserInfoResponse> GetUserInfoAsync(int userId)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            throw new InvalidOperationException("用户不存在");

        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

        var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();

        return new UserInfoResponse
        {
            Sub = user.Id.ToString(),
            Name = user.Username,
            PreferredUsername = user.Username,
            Email = user.Email,
            EmailVerified = false,
            Permissions = permissions,
            RoleIds = roleIds
        };
    }

    public object GetDiscoveryDocument()
    {
        var issuer = _config["OAuth:Issuer"] ?? "http://localhost:5000";

        return new
        {
            issuer,
            authorization_endpoint = $"{issuer}/authorize",
            token_endpoint = $"{issuer}/api/oauth/token",
            userinfo_endpoint = $"{issuer}/api/oauth/userinfo",
            jwks_uri = $"{issuer}/.well-known/jwks.json",
            revocation_endpoint = $"{issuer}/api/oauth/revoke",
            scopes_supported = new[] { "openid", "profile", "email" },
            response_types_supported = new[] { "code" },
            grant_types_supported = new[] { "authorization_code", "refresh_token" },
            subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { "RS256" },
            token_endpoint_auth_methods_supported = new[] { "none", "client_secret_post" },
            code_challenge_methods_supported = new[] { "S256" },
            claims_supported = new[] {
                "sub", "name", "preferred_username", "email",
                "email_verified", "permissions"
            }
        };
    }
}
