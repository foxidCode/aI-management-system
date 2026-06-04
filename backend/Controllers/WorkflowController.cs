using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkflowController : ControllerBase
{
    private readonly WorkflowService _svc;

    public WorkflowController(WorkflowService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? category)
    {
        var list = await _svc.GetDefinitionsAsync(category);
        return Ok(new { success = true, data = list });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        try { return Ok(new { success = true, data = await _svc.GetDefinitionAsync(id) }); }
        catch (InvalidOperationException ex) { return NotFound(new { success = false, message = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = GetModelError() });
        try { return Ok(new { success = true, data = await _svc.CreateDefinitionAsync(req) }); }
        catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateWorkflowRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = GetModelError() });
        try { return Ok(new { success = true, data = await _svc.UpdateDefinitionAsync(id, req) }); }
        catch (InvalidOperationException ex) { return NotFound(new { success = false, message = ex.Message }); }
    }

    [HttpPost("{id:int}/publish")]
    public async Task<IActionResult> Publish(int id)
    {
        try { return Ok(new { success = true, message = await _svc.PublishAsync(id) }); }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try { await _svc.DeleteDefinitionAsync(id); return Ok(new { success = true, message = "已删除" }); }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        return Ok(new { success = true, data = await _svc.GetStatsAsync(userId) });
    }

    private string GetModelError()
    {
        var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(m => !string.IsNullOrEmpty(m));
        return string.Join("；", errors);
    }
}
