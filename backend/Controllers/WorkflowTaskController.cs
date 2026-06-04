using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/workflow-task")]
[Authorize]
public class WorkflowTaskController : ControllerBase
{
    private readonly WorkflowService _svc;

    public WorkflowTaskController(WorkflowService svc) => _svc = svc;

    private int UserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("todo")]
    public async Task<IActionResult> GetTodo()
    {
        var tasks = await _svc.GetTodoTasksAsync(UserId);
        tasks = await _svc.EnrichTaskResponsesAsync(tasks);
        return Ok(new { success = true, data = tasks });
    }

    [HttpGet("done")]
    public async Task<IActionResult> GetDone()
    {
        var tasks = await _svc.GetDoneTasksAsync(UserId);
        tasks = await _svc.EnrichTaskResponsesAsync(tasks);
        return Ok(new { success = true, data = tasks });
    }

    [HttpGet("my-applications")]
    public async Task<IActionResult> GetMyApplications()
    {
        var tasks = await _svc.GetMyApplicationsAsync(UserId);
        tasks = await _svc.EnrichTaskResponsesAsync(tasks);
        return Ok(new { success = true, data = tasks });
    }
}
