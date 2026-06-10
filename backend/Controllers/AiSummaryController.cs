using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiSummaryController : ControllerBase
{
    private readonly AiSummaryService _summaryService;

    public AiSummaryController(AiSummaryService summaryService)
    {
        _summaryService = summaryService;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private bool IsAdmin() =>
        User.Claims.Any(c => c.Type == "permission" && (c.Value == "role:manage" || c.Value == "ai:summary:manage"));

    // ========== List ==========

    /// <summary>获取每日总结列表（管理员）</summary>
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!IsAdmin()) return Forbid();

        var result = await _summaryService.GetListAsync(page, pageSize);
        return Ok(new { success = true, data = result.List, total = result.Total });
    }

    /// <summary>获取单个总结详情</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!IsAdmin()) return Forbid();

        var summary = await _summaryService.GetByIdAsync(id);
        if (summary == null) return NotFound(new { success = false, message = "总结不存在" });
        return Ok(new { success = true, data = summary });
    }

    // ========== Generate ==========

    /// <summary>手动触发生成总结（默认为今天）</summary>
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateSummaryRequest? req)
    {
        if (!IsAdmin()) return Forbid();

        try
        {
            var summary = await _summaryService.GenerateAsync(req?.Date);
            return Ok(new { success = true, data = summary, message = "总结生成成功" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"生成失败: {ex.Message}" });
        }
    }

    // ========== Review ==========

    /// <summary>审批总结（通过 / 拒绝）</summary>
    [HttpPut("{id}/review")]
    public async Task<IActionResult> Review(int id, [FromBody] ReviewSummaryRequest req)
    {
        if (!IsAdmin()) return Forbid();

        if (req.Status != "approved" && req.Status != "rejected")
            return BadRequest(new { success = false, message = "Status 必须为 approved 或 rejected" });

        var result = await _summaryService.ReviewAsync(id, GetUserId(), req);
        if (result == null) return NotFound(new { success = false, message = "总结不存在" });

        return Ok(new { success = true, data = result, message = req.Status == "approved" ? "已通过审批，已归档到知识库" : "已拒绝" });
    }

    // ========== Delete ==========

    /// <summary>删除总结</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdmin()) return Forbid();

        var ok = await _summaryService.DeleteAsync(id);
        if (!ok) return NotFound(new { success = false, message = "总结不存在" });
        return Ok(new { success = true });
    }
}

public class GenerateSummaryRequest
{
    public string? Date { get; set; }
}
