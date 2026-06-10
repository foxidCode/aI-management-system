using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/aichat/admin")]
[Authorize]
public class AiAdminController : ControllerBase
{
    private readonly AiChatService _chatService;

    public AiAdminController(AiChatService chatService)
    {
        _chatService = chatService;
    }

    private bool IsAdmin() =>
        User.Claims.Any(c => c.Type == "permission" && (c.Value == "role:manage" || c.Value == "ai:chat:manage"));

    /// <summary>获取所有会话列表（管理员，支持日期和用户筛选）</summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetAllSessions(
        [FromQuery] int? userId,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!IsAdmin()) return Forbid();

        var filter = new AdminSessionFilterRequest
        {
            UserId = userId,
            StartDate = startDate,
            EndDate = endDate,
            Page = page,
            PageSize = pageSize
        };

        var result = await _chatService.GetAllSessionsAdminAsync(filter);
        return Ok(new { success = true, data = result.List, total = result.Total });
    }

    /// <summary>删除指定会话（管理员）</summary>
    [HttpDelete("sessions/{id}")]
    public async Task<IActionResult> DeleteSession(int id)
    {
        if (!IsAdmin()) return Forbid();

        var ok = await _chatService.AdminDeleteSessionAsync(id);
        if (!ok) return NotFound(new { success = false, message = "会话不存在" });
        return Ok(new { success = true, message = "已删除" });
    }

    /// <summary>批量删除会话（管理员）</summary>
    [HttpPost("sessions/batch-delete")]
    public async Task<IActionResult> BatchDelete([FromBody] BatchDeleteRequest req)
    {
        if (!IsAdmin()) return Forbid();

        if (req.Ids == null || req.Ids.Count == 0)
            return BadRequest(new { success = false, message = "请选择要删除的会话" });

        var deleted = await _chatService.AdminBatchDeleteSessionsAsync(req.Ids);
        return Ok(new { success = true, message = $"已删除 {deleted} 个会话" });
    }
}
