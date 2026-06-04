using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/oauth")]
public class OAuthController : ControllerBase
{
    private readonly OAuthService _oauth;
    private readonly OidcService _oidc;
    private readonly AppDbContext _db;

    public OAuthController(OAuthService oauth, OidcService oidc, AppDbContext db)
    {
        _oauth = oauth;
        _oidc = oidc;
        _db = db;
    }

    /// <summary>
    /// GET /authorize — OAuth 2.0 授权端点
    /// 未认证：302 到前端登录页
    /// 已认证：生成授权码，302 到 redirect_uri
    /// </summary>
    [HttpGet("/authorize")]
    public async Task<IActionResult> Authorize(
        [FromQuery] string response_type,
        [FromQuery] string client_id,
        [FromQuery] string redirect_uri,
        [FromQuery] string? scope,
        [FromQuery] string? state,
        [FromQuery] string? code_challenge,
        [FromQuery] string? code_challenge_method,
        [FromQuery] string? nonce)
    {
        // 验证参数
        if (response_type != "code")
            return RedirectWithError(redirect_uri, "unsupported_response_type", "仅支持 response_type=code", state);

        if (code_challenge_method != null && code_challenge_method != "S256")
            return RedirectWithError(redirect_uri, "invalid_request", "仅支持 code_challenge_method=S256", state);

        var client = await _oauth.ValidateClientAsync(client_id, redirect_uri, scope);
        if (client == null)
            return RedirectWithError(redirect_uri, "unauthorized_client", "客户端无效或 redirect_uri 未注册", state);

        if (scope != null && !_oauth.ValidateScopes(client, scope))
            return RedirectWithError(redirect_uri, "invalid_scope", "请求的 scope 超出允许范围", state);

        // 检查 IdP 会话
        var authResult = await HttpContext.AuthenticateAsync("IdpSession");
        if (!authResult.Succeeded)
        {
            // 未登录：302 到前端登录页，带上 return_url
            var returnUrl = $"/authorize?{HttpContext.Request.QueryString}";
            var loginUrl = $"http://localhost:5173/login?return_url={Uri.EscapeDataString(returnUrl)}";
            return Redirect(loginUrl);
        }

        var userIdClaim = authResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            // 清除无效会话，重定向到登录
            await HttpContext.SignOutAsync("IdpSession");
            return RedirectWithError(redirect_uri, "server_error", "会话无效", state);
        }

        // 生成授权码
        var finalScope = scope ?? "openid";
        try
        {
            var code = await _oauth.GenerateAuthorizationCodeAsync(
                userId, client.Id, redirect_uri,
                code_challenge, code_challenge_method,
                finalScope, nonce);

            // 302 重定向到客户端回调
            var callbackUrl = $"{redirect_uri}?code={Uri.EscapeDataString(code)}";
            if (!string.IsNullOrEmpty(state))
                callbackUrl += $"&state={Uri.EscapeDataString(state)}";

            return Redirect(callbackUrl);
        }
        catch (Exception ex)
        {
            return RedirectWithError(redirect_uri, "server_error", ex.Message, state);
        }
    }

    /// <summary>
    /// POST /api/oauth/token — 令牌端点
    /// </summary>
    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<IActionResult> Token([FromBody] TokenRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(OAuthError("invalid_request", GetModelError()));

        try
        {
            if (request.GrantType == "authorization_code")
            {
                if (string.IsNullOrEmpty(request.Code))
                    return BadRequest(OAuthError("invalid_request", "缺少 code"));

                var response = await _oauth.ExchangeCodeAsync(
                    request.Code, request.CodeVerifier,
                    request.ClientId ?? string.Empty,
                    request.RedirectUri ?? string.Empty);

                // 生成 id_token
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    var jwt = handler.ReadJwtToken(response.AccessToken);
                    var subClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                    var nameClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                    var emailClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
                    var perms = jwt.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();

                    if (subClaim != null && int.TryParse(subClaim.Value, out var uid))
                    {
                        var user = new Models.User { Id = uid, Username = nameClaim?.Value ?? "", Email = emailClaim?.Value ?? "" };

                        // 从已使用的授权码获取 nonce
                        var codeHash = OAuthService.HashToken(request.Code);
                        var usedCode = await _db.AuthorizationCodes
                            .FirstOrDefaultAsync(a => a.Code == codeHash && a.IsUsed);
                        var nonce = usedCode?.Nonce;
                        var scopes = usedCode?.Scopes ?? "openid";

                        response.IdToken = _oidc.GenerateIdToken(user, request.ClientId ?? "vue-spa", nonce, scopes, perms);
                    }
                }
                catch { /* id_token 生成失败不影响主流程 */ }

                return Ok(response);
            }
            else if (request.GrantType == "refresh_token")
            {
                if (string.IsNullOrEmpty(request.RefreshToken))
                    return BadRequest(OAuthError("invalid_request", "缺少 refresh_token"));

                var response = await _oauth.ExchangeRefreshTokenAsync(
                    request.RefreshToken,
                    request.ClientId ?? string.Empty);
                return Ok(response);
            }
            else
            {
                return BadRequest(OAuthError("unsupported_grant_type", "不支持的 grant_type"));
            }
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(OAuthError(ex.Message, ex.InnerException?.Message ?? ex.Message));
        }
    }

    /// <summary>
    /// GET /api/oauth/userinfo — OIDC UserInfo 端点
    /// </summary>
    [HttpGet("userinfo")]
    [Authorize]
    public async Task<IActionResult> UserInfo()
    {
        var nameId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(nameId) || !int.TryParse(nameId, out var userId))
            return Unauthorized();

        try
        {
            var info = await _oidc.GetUserInfoAsync(userId);
            return Ok(info);
        }
        catch (InvalidOperationException)
        {
            return Unauthorized();
        }
    }

    /// <summary>
    /// POST /api/oauth/userinfo — UserInfo（支持 body 传 token）
    /// </summary>
    [HttpPost("userinfo")]
    [AllowAnonymous]
    public async Task<IActionResult> UserInfoPost([FromBody] Dictionary<string, string> body)
    {
        // 此端点简化实现，主要使用 GET + Bearer
        return Unauthorized();
    }

    /// <summary>
    /// POST /api/oauth/revoke — 令牌撤销端点
    /// </summary>
    [HttpPost("revoke")]
    [AllowAnonymous]
    public async Task<IActionResult> Revoke([FromBody] RevokeRequest request)
    {
        await _oauth.RevokeTokenAsync(request.Token, request.TokenTypeHint);
        return Ok();
    }

    // ==================== 辅助方法 ====================

    private static IActionResult RedirectWithError(string redirectUri, string error, string description, string? state)
    {
        var url = $"{redirectUri}?error={Uri.EscapeDataString(error)}&error_description={Uri.EscapeDataString(description)}";
        if (!string.IsNullOrEmpty(state))
            url += $"&state={Uri.EscapeDataString(state)}";
        return new RedirectResult(url, false);
    }

    private static object OAuthError(string error, string description)
    {
        return new { error, error_description = description };
    }

    private string GetModelError()
    {
        return string.Join("; ", ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage));
    }

}
