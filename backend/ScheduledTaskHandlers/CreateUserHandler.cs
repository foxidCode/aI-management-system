using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.ScheduledTaskHandlers;

/// <summary>
/// 定时自动创建用户
/// 参数: password=密码(默认password123), role=角色名(默认"普通用户"), prefix=用户名前缀(默认"auto")
/// </summary>
public class CreateUserHandler : IScheduledTaskHandler
{
    public async Task<string> ExecuteAsync(IServiceProvider services, Dictionary<string, string?> parameters, CancellationToken cancellation)
    {
        var db = services.GetRequiredService<AppDbContext>();

        var prefix = parameters.GetValueOrDefault("prefix", "auto");
        var password = parameters.GetValueOrDefault("password", "password123");
        var roleName = parameters.GetValueOrDefault("role", "普通用户");

        var uname = $"{prefix}_{DateTime.Now:HHmmss}_{Guid.NewGuid().ToString()[..6]}";
        var user = new User
        {
            Username = uname,
            Email = $"{uname}@auto.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellation);

        // 分配默认角色
        var defaultRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellation);
        if (defaultRole != null)
        {
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = defaultRole.Id });
            await db.SaveChangesAsync(cancellation);
        }

        return $"Created user: {uname} (role: {roleName})";
    }

    public List<HandlerParameterMeta> GetParameterMetas() => new()
    {
        new() { Key = "prefix", Label = "用户名前缀", Description = "自动生成用户的用户名前缀，最终用户名格式为 {前缀}_{时间}_{随机码}", DefaultValue = "auto" },
        new() { Key = "password", Label = "默认密码", Description = "新创建用户的初始登录密码", DefaultValue = "password123" },
        new() { Key = "role", Label = "分配角色", Description = "自动分配的角色名称，用户将获得该角色的所有权限", DefaultValue = "普通用户" },
    };
}
