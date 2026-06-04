using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using backend.Data;
using backend.DTOs;
using backend.Models;

namespace backend.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IConnectionMultiplexer _redis;
    private readonly EmailService _email;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(AppDbContext db, IConfiguration config, IConnectionMultiplexer redis,
        EmailService email, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _config = config;
        _redis = redis;
        _email = email;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Username == request.Username))
            throw new InvalidOperationException("用户名已存在");

        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            throw new InvalidOperationException("邮箱已被注册");

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.Now
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // 默认分配"普通用户"角色
        var defaultRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "普通用户");
        if (defaultRole != null)
        {
            _db.UserRoles.Add(new Models.UserRole { UserId = user.Id, RoleId = defaultRole.Id });
            await _db.SaveChangesAsync();
        }

        var (token, permissions, roleIds) = await GenerateJwtTokenAsync(user);
        await CacheTokenAsync(user.Id, token);

        return new AuthResponse
        {
            Token = token,
            Username = user.Username,
            Email = user.Email,
            Permissions = permissions,
            RoleIds = roleIds
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new InvalidOperationException("用户名或密码错误");

        if (user.IsFrozen)
            throw new InvalidOperationException("该用户已被冻结，请联系管理员");

        var (token, permissions, roleIds) = await GenerateJwtTokenAsync(user);
        await CacheTokenAsync(user.Id, token);

        // 设置 IdP 会话 Cookie（用于 /authorize 端点）
        await SignInIdpSessionAsync(user.Id, user.Username);

        return new AuthResponse
        {
            Token = token,
            Username = user.Username,
            Email = user.Email,
            Permissions = permissions,
            RoleIds = roleIds
        };
    }

    /// <summary>
    /// 设置 IdP 会话 Cookie
    /// </summary>
    private async Task SignInIdpSessionAsync(int userId, string username)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
        };

        var identity = new ClaimsIdentity(claims, "IdpSession");
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync("IdpSession", principal, new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30),
            AllowRefresh = true,
        });
    }

    /// <summary>
    /// 记录用户活动（在线心跳）
    /// </summary>
    public async Task RecordUserActivityAsync(int userId)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync($"online:{userId}", DateTime.Now.ToString("o"), TimeSpan.FromMinutes(5));
        }
        catch { }
    }

    /// <summary>
    /// 获取在线用户列表（最近5分钟有活动）
    /// </summary>
    public async Task<List<object>> GetOnlineUsersAsync()
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: "online:*").ToArray();
            var userIds = keys
                .Select(k => int.TryParse(k.ToString().Replace("online:", ""), out var id) ? id : 0)
                .Where(id => id > 0)
                .ToList();

            if (userIds.Count == 0) return new List<object>();

            var users = await _db.Users
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                })
                .ToListAsync();

            return users.Cast<object>().ToList();
        }
        catch
        {
            return new List<object>();
        }
    }

    /// <summary>
    /// 用户登出：清除在线状态和 Redis 中的 token
    /// </summary>
    public async Task LogoutAsync(int userId, string? token)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync($"online:{userId}");
            if (!string.IsNullOrEmpty(token))
                await db.KeyDeleteAsync($"token:{token}");
        }
        catch { }
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        var db = _redis.GetDatabase();
        return await db.KeyExistsAsync($"token:{token}");
    }

    internal async Task<(string token, List<string> permissions, List<int> roleIds)> GenerateJwtTokenAsync(User user)
    {
        var jwtSettings = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // 获取用户的权限编码
        var permissionCodes = await _db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync();

        var roleIds = await _db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email)
        };

        // 将每个权限编码作为 claim 加入
        foreach (var code in permissionCodes)
        {
            claims.Add(new Claim("permission", code));
        }

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.Now.AddHours(2),
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), permissionCodes, roleIds);
    }

    internal async Task<(string token, List<string> permissions, List<int> roleIds)> GenerateJwtTokenWithLifetimeAsync(User user, int lifetimeHours)
    {
        var jwtSettings = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var permissionCodes = await _db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync();

        var roleIds = await _db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email)
        };

        foreach (var code in permissionCodes)
        {
            claims.Add(new Claim("permission", code));
        }

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.Now.AddHours(lifetimeHours),
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), permissionCodes, roleIds);
    }

    internal async Task CacheTokenAsync(int userId, string token)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync($"token:{token}", userId.ToString(), TimeSpan.FromHours(2));
    }

    // ========== 忘记密码 ==========

    public async Task<string?> SendResetCodeAsync(string email)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
            throw new InvalidOperationException("该邮箱未注册");

        var code = Random.Shared.Next(100000, 999999).ToString();

        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Email = email,
            Code = code,
            ExpiresAt = DateTime.Now.AddMinutes(5)
        });
        await _db.SaveChangesAsync();

        var (sent, devCode) = await _email.SendVerificationCodeAsync(email, code);
        if (!sent)
            throw new InvalidOperationException("验证码发送失败，请联系管理员");

        return devCode;
    }

    public async Task ResetPasswordAsync(string email, string code, string newPassword)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
            throw new InvalidOperationException("该邮箱未注册");

        // 查找有效验证码
        var token = await _db.PasswordResetTokens
            .Where(t => t.Email == email && t.Code == code && !t.IsUsed && t.ExpiresAt > DateTime.Now)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (token == null)
            throw new InvalidOperationException("验证码无效或已过期");

        // 标记验证码已使用
        token.IsUsed = true;

        // 更新密码
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();
    }
}
