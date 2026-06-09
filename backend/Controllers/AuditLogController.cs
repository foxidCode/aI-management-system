using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditLogController : ControllerBase
{
    private readonly AuditService _auditService;

    public AuditLogController(AuditService auditService)
    {
        _auditService = auditService;
    }

    private bool IsAdmin() =>
        User.Claims.Any(c => c.Type == "permission" && (c.Value == "role:manage" || c.Value == "audit:view"));

    // ========== 操作日志 ==========

    /// <summary>分页查询操作日志</summary>
    [HttpGet("operations")]
    public async Task<IActionResult> GetOperationLogs(
        [FromQuery] string? keyword,
        [FromQuery] string? action,
        [FromQuery] int? userId,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!IsAdmin()) return Forbid();

        var req = new OperationLogQueryRequest
        {
            Keyword = keyword,
            Action = action,
            UserId = userId,
            StartDate = startDate,
            EndDate = endDate,
            Page = page,
            PageSize = pageSize
        };

        var result = await _auditService.GetOperationLogsAsync(req);
        return Ok(new { success = true, data = result.List, total = result.Total });
    }

    // ========== 权限变更日志 ==========

    /// <summary>分页查询权限变更日志</summary>
    [HttpGet("permission-changes")]
    public async Task<IActionResult> GetPermissionChangeLogs(
        [FromQuery] string? keyword,
        [FromQuery] int? targetUserId,
        [FromQuery] string? changeType,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!IsAdmin()) return Forbid();

        var req = new PermissionChangeLogQueryRequest
        {
            Keyword = keyword,
            TargetUserId = targetUserId,
            ChangeType = changeType,
            StartDate = startDate,
            EndDate = endDate,
            Page = page,
            PageSize = pageSize
        };

        var result = await _auditService.GetPermissionChangeLogsAsync(req);
        return Ok(new { success = true, data = result.List, total = result.Total });
    }

    // ========== 操作统计 ==========

    /// <summary>获取操作统计数据</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] int days = 7)
    {
        if (!IsAdmin()) return Forbid();

        var stats = await _auditService.GetOperationStatsAsync(days);
        return Ok(new { success = true, data = stats });
    }

    // ========== 异常告警 ==========

    /// <summary>检测并返回异常告警列表</summary>
    [HttpGet("anomalies")]
    public async Task<IActionResult> GetAnomalies()
    {
        if (!IsAdmin()) return Forbid();

        var alerts = await _auditService.DetectAnomaliesAsync();
        return Ok(new { success = true, data = alerts });
    }
}
