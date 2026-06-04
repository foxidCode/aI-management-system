using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/workflow-instance")]
[Authorize]
public class WorkflowInstanceController : ControllerBase
{
    private readonly WorkflowEngine _engine;
    private readonly WorkflowService _svc;

    public WorkflowInstanceController(WorkflowEngine engine, WorkflowService svc)
    {
        _engine = engine;
        _svc = svc;
    }

    private int UserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return Ok(new { success = true, data = await _svc.GetInstancesAsync(status, page, pageSize) });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        try { return Ok(new { success = true, data = await _svc.GetInstanceAsync(id) }); }
        catch (InvalidOperationException ex) { return NotFound(new { success = false, message = ex.Message }); }
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartWorkflowRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = GetModelError() });
        try
        {
            var instance = await _engine.StartWorkflowAsync(req.DefinitionId, req.ModuleName, req.RelatedId, UserId);
            return Ok(new { success = true, data = new { instance.Id, instance.Status }, message = "流程已发起" });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApprovalRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = GetModelError() });
        try
        {
            var msg = await _engine.ApproveAsync(id, req.NodeId, UserId, req.Comment);
            return Ok(new { success = true, message = msg });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] ApprovalRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = GetModelError() });
        try
        {
            var msg = await _engine.RejectAsync(id, req.NodeId, UserId, req.Comment);
            return Ok(new { success = true, message = msg });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    [HttpPost("{id:int}/recall")]
    public async Task<IActionResult> Recall(int id)
    {
        try
        {
            var msg = await _engine.RecallAsync(id, UserId);
            return Ok(new { success = true, message = msg });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    private string GetModelError()
    {
        var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(m => !string.IsNullOrEmpty(m));
        return string.Join("；", errors);
    }
}
