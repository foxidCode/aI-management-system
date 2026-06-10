using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiChatController : ControllerBase
{
    private readonly AiChatService _chatService;

    public AiChatController(AiChatService chatService)
    {
        _chatService = chatService;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    // ========== Sessions ==========

    /// <summary>获取当前用户的会话列表</summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var sessions = await _chatService.GetSessionsAsync(GetUserId());
        return Ok(new { success = true, data = sessions });
    }

    /// <summary>创建新会话</summary>
    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest req)
    {
        var session = await _chatService.CreateSessionAsync(GetUserId(), req);
        return Ok(new { success = true, data = session });
    }

    /// <summary>删除会话</summary>
    [HttpDelete("sessions/{id}")]
    public async Task<IActionResult> DeleteSession(int id)
    {
        var ok = await _chatService.DeleteSessionAsync(id, GetUserId());
        if (!ok) return NotFound(new { success = false, message = "会话不存在" });
        return Ok(new { success = true });
    }

    // ========== Messages ==========

    /// <summary>获取会话消息历史</summary>
    [HttpGet("sessions/{sessionId}/messages")]
    public async Task<IActionResult> GetMessages(int sessionId)
    {
        var result = await _chatService.GetMessagesAsync(sessionId, GetUserId());
        if (result == null) return NotFound(new { success = false, message = "会话不存在" });
        return Ok(new { success = true, data = result });
    }

    /// <summary>发送消息（SSE 流式响应）</summary>
    [HttpPost("send")]
    public async Task SendMessage([FromBody] SendMessageRequest req)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no"; // 禁用 nginx 缓冲

        var fullContent = string.Empty;

        // 流式响应完成后保存 AI 回复
        try
        {
            await _chatService.SendMessageStreamAsync(GetUserId(), req, Response);

            // 尝试从流中提取完整内容（由 AiChatService 在 done 事件中发送）
            // 这里我们不等待，因为流已经是实时的
        }
        catch (Exception ex)
        {
            // Write error as SSE event
            var errorJson = System.Text.Json.JsonSerializer.Serialize(new { type = "error", content = ex.Message });
            var errorBytes = System.Text.Encoding.UTF8.GetBytes($"data: {errorJson}\n\n");
            await Response.Body.WriteAsync(errorBytes);
            await Response.Body.FlushAsync();
        }
    }

    /// <summary>保存 AI 回复（流式完成后调用）</summary>
    [HttpPost("sessions/{sessionId}/save-assistant")]
    public async Task<IActionResult> SaveAssistantMessage(int sessionId, [FromBody] SaveAssistantRequest req)
    {
        // 验证会话属于当前用户
        await _chatService.SaveAssistantMessageAsync(sessionId, req.Content);
        return Ok(new { success = true });
    }
}

// 此 DTO 仅在此控制器使用
public class SaveAssistantRequest
{
    public string Content { get; set; } = string.Empty;
}
