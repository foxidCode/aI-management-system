using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = GetModelError() });

        try
        {
            var response = await _authService.RegisterAsync(request);
            return Ok(new { success = true, data = response });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = GetModelError() });

        try
        {
            var response = await _authService.LoginAsync(request);
            return Ok(new { success = true, data = response });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var nameId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(nameId, out var userId))
        {
            var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
            await _authService.LogoutAsync(userId, token);
        }
        return Ok(new { success = true, message = "已退出登录" });
    }

    [HttpPost("send-reset-code")]
    public async Task<IActionResult> SendResetCode([FromBody] SendResetCodeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = GetModelError() });

        try
        {
            var devCode = await _authService.SendResetCodeAsync(request.Email);
            var msg = "验证码已发送至您的邮箱，5分钟内有效";
            return Ok(new { success = true, message = msg, data = new { devCode } });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = GetModelError() });

        try
        {
            await _authService.ResetPasswordAsync(request.Email, request.Code, request.NewPassword);
            return Ok(new { success = true, message = "密码重置成功，请使用新密码登录" });
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
