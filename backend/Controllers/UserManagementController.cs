using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Models;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserManagementController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PermissionService _permService;
    private readonly AuthService _auth;

    public UserManagementController(AppDbContext db, PermissionService permService, AuthService auth)
    {
        _db = db;
        _permService = permService;
        _auth = auth;
    }

    // 获取用户列表（分页、排序、搜索）
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortField = null,
        [FromQuery] string? sortOrder = null)
    {
        var query = _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).AsQueryable();

        // 搜索：按用户名、邮箱、工号模糊匹配
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(u =>
                u.Username.Contains(kw) ||
                u.Email.Contains(kw) ||
                (u.EmployeeId != null && u.EmployeeId.Contains(kw)) ||
                (u.IdCard != null && u.IdCard.Contains(kw)));
        }

        // 排序
        var isAsc = string.Equals(sortOrder, "ascending", StringComparison.OrdinalIgnoreCase);
        query = sortField?.ToLower() switch
        {
            "username" => isAsc ? query.OrderBy(u => u.Username) : query.OrderByDescending(u => u.Username),
            "email" => isAsc ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email),
            "gender" => isAsc ? query.OrderBy(u => u.Gender) : query.OrderByDescending(u => u.Gender),
            "idcard" => isAsc ? query.OrderBy(u => u.IdCard) : query.OrderByDescending(u => u.IdCard),
            "employeeid" => isAsc ? query.OrderBy(u => u.EmployeeId) : query.OrderByDescending(u => u.EmployeeId),
            "isfrozen" => isAsc ? query.OrderBy(u => u.IsFrozen) : query.OrderByDescending(u => u.IsFrozen),
            "createdat" => isAsc ? query.OrderBy(u => u.CreatedAt) : query.OrderByDescending(u => u.CreatedAt),
            "updatedat" => isAsc ? query.OrderBy(u => u.UpdatedAt) : query.OrderByDescending(u => u.UpdatedAt),
            _ => query.OrderByDescending(u => u.CreatedAt)
        };

        var total = await query.CountAsync();

        // 获取在线用户 ID 集合
        var onlineUserIds = new HashSet<int>();
        try
        {
            var onlineUsers = await _auth.GetOnlineUsersAsync();
            foreach (dynamic u in onlineUsers)
                onlineUserIds.Add(u.Id);
        }
        catch { }

        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => ToListResponse(u))
            .ToListAsync();

        // 标记在线状态
        foreach (var u in users)
            u.IsOnline = onlineUserIds.Contains(u.Id);

        return Ok(new
        {
            success = true,
            data = new
            {
                list = users,
                total,
                page,
                pageSize
            }
        });
    }

    // 新增用户
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = GetModelError() });

        if (await _db.Users.AnyAsync(u => u.Username == request.Username))
            return BadRequest(new { success = false, message = "用户名已存在1" });

        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return BadRequest(new { success = false, message = "邮箱已被使用1" });

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
            Gender = request.Gender,
            IdCard = request.IdCard,
            EmployeeId = request.EmployeeId,
            Remark = request.Remark,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, data = ToListResponse(user) });
    }

    // 修改用户信息
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = GetModelError() });

        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { success = false, message = "用户不存在" });

        if (await _db.Users.AnyAsync(u => u.Email == request.Email && u.Id != id))
            return BadRequest(new { success = false, message = "邮箱已被其他用户使用" });

        user.Email = request.Email;
        user.Gender = request.Gender;
        user.IdCard = request.IdCard;
        user.EmployeeId = request.EmployeeId;
        user.Remark = request.Remark;
        user.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();

        return Ok(new { success = true, data = ToListResponse(user) });
    }

    // 重置密码
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] List<int> userIds)
    {
        if (userIds == null || userIds.Count == 0)
            return BadRequest(new { success = false, message = "请选择要重置密码的用户" });

        var users = await _db.Users.Where(u => userIds.Contains(u.Id)).ToListAsync();
        foreach (var user in users)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("password");
            user.UpdatedAt = DateTime.Now;
        }

        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = $"已重置 {users.Count} 个用户的密码为 password" });
    }

    // 冻结/解冻用户
    [HttpPost("{id}/freeze")]
    public async Task<IActionResult> ToggleFreeze(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { success = false, message = "用户不存在" });

        if (user.Username == "admin")
            return BadRequest(new { success = false, message = "内置管理员账号不可冻结" });

        user.IsFrozen = !user.IsFrozen;
        user.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = user.IsFrozen ? "用户已冻结" : "用户已解冻",
            data = ToListResponse(user)
        });
    }

    // 批量冻结
    [HttpPost("batch-freeze")]
    public async Task<IActionResult> BatchFreeze([FromBody] BatchFreezeRequest request)
    {
        if (request.UserIds == null || request.UserIds.Count == 0)
            return BadRequest(new { success = false, message = "请选择用户" });

        var users = await _db.Users.Where(u => request.UserIds.Contains(u.Id)).ToListAsync();

        // 过滤掉admin用户
        var adminUser = users.FirstOrDefault(u => u.Username == "admin");
        var targetUsers = users.Where(u => u.Username != "admin").ToList();

        foreach (var user in targetUsers)
        {
            user.IsFrozen = request.Freeze;
            user.UpdatedAt = DateTime.Now;
        }

        await _db.SaveChangesAsync();

        // 冻结时强制踢出在线用户
        if (request.Freeze)
        {
            foreach (var user in targetUsers)
                await _auth.LogoutAsync(user.Id, null);
        }

        var action = request.Freeze ? "冻结" : "解冻";
        var msg = $"已{action} {targetUsers.Count} 个用户";
        if (adminUser != null)
            msg += "（内置管理员账号已跳过）";

        return Ok(new { success = true, message = msg });
    }

    // ========== 删除用户 ==========

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { success = false, message = "用户不存在" });

        if (user.Username == "admin")
            return BadRequest(new { success = false, message = "内置管理员账号不可删除" });

        // 不允许删除自己
        var myId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        if (user.Id == myId)
            return BadRequest(new { success = false, message = "不能删除自己" });

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = $"用户 \"{user.Username}\" 已删除" });
    }

    [HttpPost("batch-delete")]
    public async Task<IActionResult> BatchDelete([FromBody] List<int> userIds)
    {
        if (userIds == null || userIds.Count == 0)
            return BadRequest(new { success = false, message = "请选择要删除的用户" });

        var users = await _db.Users.Where(u => userIds.Contains(u.Id)).ToListAsync();

        // 过滤掉admin和自己
        var myId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var protectedUsers = users.Where(u => u.Username == "admin" || u.Id == myId).ToList();
        var targetUsers = users.Where(u => u.Username != "admin" && u.Id != myId).ToList();

        if (targetUsers.Count == 0)
            return BadRequest(new { success = false, message = "所选用户均不可删除（admin 或当前用户）" });

        _db.Users.RemoveRange(targetUsers);
        await _db.SaveChangesAsync();

        var msg = $"已删除 {targetUsers.Count} 个用户";
        if (protectedUsers.Count > 0)
            msg += $"（跳过 {protectedUsers.Count} 个保护用户）";

        return Ok(new { success = true, message = msg });
    }

    // ========== 踢出用户 ==========

    [HttpPost("kick")]
    public async Task<IActionResult> KickUsers([FromBody] List<int> userIds)
    {
        if (userIds == null || userIds.Count == 0)
            return BadRequest(new { success = false, message = "请选择要踢出的用户" });

        // 不允许踢出自己
        var myId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        userIds = userIds.Where(id => id != myId).ToList();

        if (userIds.Count == 0)
            return BadRequest(new { success = false, message = "不能踢出自己" });

        foreach (var id in userIds)
            await _auth.LogoutAsync(id, null);

        return Ok(new { success = true, message = $"已踢出 {userIds.Count} 个用户" });
    }

    private static UserListResponse ToListResponse(User u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        Gender = u.Gender,
        IdCard = u.IdCard,
        EmployeeId = u.EmployeeId,
        Remark = u.Remark,
        IsFrozen = u.IsFrozen,
        CreatedAt = u.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
        UpdatedAt = u.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
        RoleIds = u.UserRoles.Select(ur => ur.RoleId).ToList(),
        RoleNames = u.UserRoles.Select(ur => ur.Role.Name).ToList()
    };

    // 获取用户角色
    [HttpGet("{id}/roles")]
    public async Task<IActionResult> GetUserRoles(int id)
    {
        var userWithRoles = await _permService.GetUserWithRolesAsync(id);
        if (userWithRoles == null)
            return NotFound(new { success = false, message = "用户不存在" });
        return Ok(new { success = true, data = userWithRoles });
    }

    // 分配用户角色（仅管理员）
    [HttpPut("{id}/roles")]
    public async Task<IActionResult> AssignUserRoles(int id, [FromBody] AssignUserRolesRequest request)
    {
        if (!User.Claims.Any(c => c.Type == "permission" && c.Value == "role:manage"))
            return Forbid();

        try
        {
            await _permService.AssignUserRolesAsync(id, request.RoleIds);
            return Ok(new { success = true, message = "角色分配成功" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    private string GetModelError()
    {
        var errors = ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .Where(m => !string.IsNullOrEmpty(m));
        return string.Join("；", errors);
    }
}

public class BatchFreezeRequest
{
    public List<int> UserIds { get; set; } = new();
    public bool Freeze { get; set; }
}
