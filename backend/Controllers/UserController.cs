using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public UserController(AppDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { success = false, message = "用户不存在" });

        return Ok(new
        {
            success = true,
            data = new UserProfileResponse
            {
                Username = user.Username,
                Email = user.Email,
                CreatedAt = user.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            }
        });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = GetModelError() });

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { success = false, message = "用户不存在" });

        // 验证当前密码
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { success = false, message = "当前密码错误" });

        // 检查邮箱是否被其他用户占用
        if (await _db.Users.AnyAsync(u => u.Email == request.Email && u.Id != userId))
            return BadRequest(new { success = false, message = "邮箱已被其他账号使用" });

        user.Email = request.Email;

        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            data = new UserProfileResponse
            {
                Username = user.Username,
                Email = user.Email,
                CreatedAt = user.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            }
        });
    }

    /// <summary>
    /// 获取当前用户的主页配置
    /// </summary>
    [HttpGet("home-config")]
    public async Task<IActionResult> GetHomeConfig()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { success = false, message = "用户不存在" });

        return Ok(new { success = true, data = user.HomeConfig });
    }

    /// <summary>
    /// 保存当前用户的主页配置
    /// </summary>
    [HttpPut("home-config")]
    public async Task<IActionResult> SaveHomeConfig([FromBody] HomeConfigRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { success = false, message = "用户不存在" });

        user.HomeConfig = request.Config;
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "主页配置已保存" });
    }

    /// <summary>
    /// 获取当前在线用户（最近5分钟有活动）
    /// </summary>
    [HttpGet("online")]
    public async Task<IActionResult> GetOnlineUsers()
    {
        var users = await _auth.GetOnlineUsersAsync();
        return Ok(new { success = true, data = users, total = users.Count });
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
