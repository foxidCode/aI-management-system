using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PermissionManagementController : ControllerBase
{
    private readonly PermissionService _permService;
    private readonly AuditService _auditService;

    public PermissionManagementController(PermissionService permService, AuditService auditService)
    {
        _permService = permService;
        _auditService = auditService;
    }

    private bool IsAdmin() =>
        User.Claims.Any(c => c.Type == "permission" && c.Value == "role:manage");

    private int GetUserId() =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private string GetUsername() =>
        User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("username")?.Value ?? "unknown";

    private string? GetClientIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    // ========== 权限项 CRUD ==========

    /// <summary>分页获取权限列表</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!IsAdmin()) return Forbid();

        var (list, total) = await _permService.GetAllPermissionsPaginatedAsync(keyword, page, pageSize);
        return Ok(new { success = true, data = list, total });
    }

    /// <summary>创建权限项</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePermissionRequest request)
    {
        if (!IsAdmin()) return Forbid();
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = GetModelError() });

        try
        {
            var perm = await _permService.CreatePermissionAsync(request);

            // 审计日志
            await _auditService.LogOperationAsync(
                GetUserId(), GetUsername(), GetClientIp(),
                "permission:create", "Permission", perm.Id.ToString(), perm.Name,
                $"创建权限项：{perm.Name}({perm.Code})", isSensitive: true);

            return Ok(new { success = true, data = perm });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>编辑权限项</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePermissionRequest request)
    {
        if (!IsAdmin()) return Forbid();
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = GetModelError() });

        try
        {
            var perm = await _permService.UpdatePermissionAsync(id, request);
            if (perm == null)
                return NotFound(new { success = false, message = "权限不存在" });

            await _auditService.LogOperationAsync(
                GetUserId(), GetUsername(), GetClientIp(),
                "permission:update", "Permission", id.ToString(), perm.Name,
                $"编辑权限项：{perm.Name}({perm.Code})", isSensitive: true);

            return Ok(new { success = true, data = perm });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>删除权限项</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdmin()) return Forbid();

        // 获取权限信息用于日志
        var perm = await _permService.GetAllPermissionCodesAsync();
        var target = perm.FirstOrDefault(p => p.Id == id);

        var result = await _permService.DeletePermissionAsync(id);
        if (!result)
            return NotFound(new { success = false, message = "权限不存在" });

        await _auditService.LogOperationAsync(
            GetUserId(), GetUsername(), GetClientIp(),
            "permission:delete", "Permission", id.ToString(), target?.Name ?? id.ToString(),
            $"删除权限项：{target?.Name}({target?.Code})", isSensitive: true);

        return Ok(new { success = true, message = "权限已删除" });
    }

    // ========== 直接用户授权 ==========

    /// <summary>授予用户直接权限</summary>
    [HttpPut("grant-user")]
    public async Task<IActionResult> GrantUserPermission([FromBody] GrantUserPermissionRequest request)
    {
        if (!IsAdmin()) return Forbid();

        try
        {
            await _permService.GrantUserPermissionsAsync(request.UserId, request.PermissionIds, GetUserId());

            // 获取权限名称用于日志
            var allPerms = await _permService.GetAllPermissionCodesAsync();
            var permNames = allPerms.Where(p => request.PermissionIds.Contains(p.Id))
                .Select(p => $"{p.Name}({p.Code})").ToList();

            // 操作日志
            await _auditService.LogOperationAsync(
                GetUserId(), GetUsername(), GetClientIp(),
                "permission:grant_user", "User", request.UserId.ToString(), null,
                $"直接授予用户权限：{string.Join(", ", permNames)}", isSensitive: true);

            // 权限变更日志（每个权限一条）
            foreach (var p in allPerms.Where(p => request.PermissionIds.Contains(p.Id)))
            {
                await _auditService.LogPermissionChangeAsync(
                    request.UserId, null, "Grant", p.Code, p.Name,
                    GetUserId(), GetUsername(), GetClientIp(),
                    "直接授予用户权限");
            }

            return Ok(new { success = true, message = "权限已授予" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>撤销用户直接权限</summary>
    [HttpPut("revoke-user")]
    public async Task<IActionResult> RevokeUserPermission([FromBody] RevokeUserPermissionRequest request)
    {
        if (!IsAdmin()) return Forbid();

        // 获取权限名称用于日志
        var allPerms = await _permService.GetAllPermissionCodesAsync();
        var permNames = allPerms.Where(p => request.PermissionIds.Contains(p.Id))
            .Select(p => $"{p.Name}({p.Code})").ToList();

        await _permService.RevokeUserPermissionsAsync(request.UserId, request.PermissionIds);

        // 操作日志
        await _auditService.LogOperationAsync(
            GetUserId(), GetUsername(), GetClientIp(),
            "permission:revoke_user", "User", request.UserId.ToString(), null,
            $"撤销用户直接权限：{string.Join(", ", permNames)}", isSensitive: true);

        // 权限变更日志（每个权限一条）
        foreach (var p in allPerms.Where(p => request.PermissionIds.Contains(p.Id)))
        {
            await _auditService.LogPermissionChangeAsync(
                request.UserId, null, "Revoke", p.Code, p.Name,
                GetUserId(), GetUsername(), GetClientIp(),
                "撤销用户直接权限");
        }

        return Ok(new { success = true, message = "权限已撤销" });
    }

    /// <summary>获取用户权限汇总（角色权限 + 直接权限）</summary>
    [HttpGet("user-summary/{userId}")]
    public async Task<IActionResult> GetUserPermissionSummary(int userId)
    {
        if (!IsAdmin()) return Forbid();

        var summary = await _permService.GetUserPermissionSummaryAsync(userId);
        if (summary == null)
            return NotFound(new { success = false, message = "用户不存在" });

        return Ok(new { success = true, data = summary });
    }

    /// <summary>获取用户直接权限 ID 列表</summary>
    [HttpGet("user-direct/{userId}")]
    public async Task<IActionResult> GetUserDirectPermissions(int userId)
    {
        if (!IsAdmin()) return Forbid();

        var permIds = await _permService.GetUserDirectPermissionsAsync(userId);
        return Ok(new { success = true, data = permIds });
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
