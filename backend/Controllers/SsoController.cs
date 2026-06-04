using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SsoController : ControllerBase
{
    private readonly SsoService _sso;

    public SsoController(SsoService sso)
    {
        _sso = sso;
    }

    /// <summary>
    /// 通过 SSO Token（魔法链接）登录（公开接口，无需认证）
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> SsoLogin([FromBody] SsoLoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = GetModelError() });

        try
        {
            var authResponse = await _sso.LoginByTokenAsync(request.Token);
            return Ok(new { success = true, data = authResponse });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 通过授权码登录（公开接口，无需认证）
    /// </summary>
    [HttpPost("login-by-code")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginByAuthCode([FromBody] AuthCodeLoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = GetModelError() });

        try
        {
            var authResponse = await _sso.LoginByAuthCodeAsync(request.Code.Trim().ToUpper());
            return Ok(new { success = true, data = authResponse });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 创建 SSO 链接（需要 sso:manage 权限）
    /// </summary>
    [HttpPost("create")]
    [Authorize]
    public async Task<IActionResult> CreateToken([FromBody] CreateSsoTokenRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = GetModelError() });

        var userId = GetCurrentUserId();

        try
        {
            var result = await _sso.CreateTokenAsync(request.UserId, request.ExpireHours, userId);
            return Ok(new { success = true, data = result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 创建授权码（需要 sso:manage 权限）
    /// </summary>
    [HttpPost("create-auth-code")]
    [Authorize]
    public async Task<IActionResult> CreateAuthCode([FromBody] CreateAuthCodeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = GetModelError() });

        var userId = GetCurrentUserId();

        try
        {
            var result = await _sso.CreateAuthCodeAsync(request.UserId, request.ExpireMinutes, userId);
            return Ok(new { success = true, data = result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// 获取 SSO Token 列表（需要 sso:manage 权限）
    /// </summary>
    [HttpGet("tokens")]
    [Authorize]
    public async Task<IActionResult> GetTokens([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? type = null)
    {
        var tokens = await _sso.GetTokensAsync(page, pageSize, type);
        return Ok(new { success = true, data = tokens });
    }

    /// <summary>
    /// 撤销 SSO Token（需要 sso:manage 权限）
    /// </summary>
    [HttpPost("revoke/{id}")]
    [Authorize]
    public async Task<IActionResult> RevokeToken(int id)
    {
        var userId = GetCurrentUserId();

        try
        {
            await _sso.RevokeTokenAsync(id, userId);
            return Ok(new { success = true, data = "令牌已撤销" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    private int GetCurrentUserId()
    {
        var nameId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(nameId, out var id) ? id : 0;
    }

    private string GetModelError()
    {
        return string.Join("; ", ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage));
    }
}
