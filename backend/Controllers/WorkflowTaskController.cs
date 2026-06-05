using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/workflow/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly WorkflowDefinitionService _defSvc;
    private readonly WorkflowService _wfSvc;

    public TasksController(WorkflowDefinitionService defSvc, WorkflowService wfSvc)
    {
        _defSvc = defSvc;
        _wfSvc = wfSvc;
    }

    private int GetUserId()
    {
        var nameId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(nameId, out var uid) ? uid : 0;
    }

    /// <summary>获取待办任务列表</summary>
    [HttpGet]
    public async Task<IActionResult> GetPending(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var userId = GetUserId();
        var result = await _defSvc.GetPendingTasksAsync(userId, page, pageSize);
        return Ok(new { success = true, data = result.List, total = result.Total });
    }

    /// <summary>获取已处理任务历史</summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var userId = GetUserId();
        var result = await _defSvc.GetTaskHistoryAsync(userId, page, pageSize);
        return Ok(new { success = true, data = result.List, total = result.Total });
    }

    /// <summary>审批通过</summary>
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveTaskRequest req)
    {
        try
        {
            var userId = GetUserId();
            if (req == null) return BadRequest(new { success = false, message = "请求参数不能为空" });
            await _wfSvc.ApproveTaskAsync(id, req.Comment, req.FormData, userId);
            return Ok(new { success = true, message = "审批通过" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.ToString() });
        }
    }

    /// <summary>驳回</summary>
    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectTaskRequest req)
    {
        try
        {
            var userId = GetUserId();
            await _wfSvc.RejectTaskAsync(id, req.Comment, userId);
            return Ok(new { success = true, message = "已驳回" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>转办</summary>
    [HttpPost("{id}/transfer")]
    public async Task<IActionResult> Transfer(int id, [FromBody] TransferTaskRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = GetModelError() });
        try
        {
            var userId = GetUserId();
            await _wfSvc.TransferTaskAsync(id, req.ToUserId, req.Comment, userId);
            return Ok(new { success = true, message = "已转办" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    private string GetModelError()
    {
        var e = ModelState.Values.SelectMany(v => v.Errors)
            .Select(x => x.ErrorMessage).Where(m => !string.IsNullOrEmpty(m));
        return string.Join("；", e);
    }
}
