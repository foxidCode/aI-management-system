using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/workflow/[controller]")]
[Authorize]
public class InstancesController : ControllerBase
{
    private readonly WorkflowDefinitionService _defSvc;
    private readonly WorkflowService _wfSvc;

    public InstancesController(WorkflowDefinitionService defSvc, WorkflowService wfSvc)
    {
        _defSvc = defSvc;
        _wfSvc = wfSvc;
    }

    private int GetUserId()
    {
        var nameId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(nameId, out var uid) ? uid : 0;
    }

    /// <summary>获取我的申请列表（或管理员查看所有）</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var userId = GetUserId();
        var result = await _defSvc.GetInstancesAsync(userId, status, page, pageSize);
        return Ok(new { success = true, data = result.List, total = result.Total });
    }

    /// <summary>获取实例详情（含任务和历史）</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _defSvc.GetInstanceByIdAsync(id);
        if (item == null) return NotFound(new { success = false, message = "流程实例不存在" });
        return Ok(new { success = true, data = item });
    }

    /// <summary>提交新流程实例</summary>
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitWorkflowInstanceRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = GetModelError() });
        try
        {
            var userId = GetUserId();
            var instance = await _wfSvc.SubmitInstanceAsync(req.DefinitionId, req.FormData, userId);
            var detail = await _defSvc.GetInstanceByIdAsync(instance.Id);
            return Ok(new { success = true, data = detail, message = "申请已提交" });
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
