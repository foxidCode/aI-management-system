using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IntegrationController : ControllerBase
{
    private readonly IntegrationService _svc;

    public IntegrationController(IntegrationService svc) => _svc = svc;

    // ========== 连接管理 ==========

    [HttpGet("connections")]
    public async Task<IActionResult> GetConnections()
    {
        var list = await _svc.GetConnectionsAsync();
        return Ok(new { success = true, data = list });
    }

    [HttpPost("connections")]
    public async Task<IActionResult> CreateConnection([FromBody] IntegrationConnectionRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = "参数有误" });
        var result = await _svc.CreateConnectionAsync(req);
        return Ok(new { success = true, data = result });
    }

    [HttpPut("connections/{id:int}")]
    public async Task<IActionResult> UpdateConnection(int id, [FromBody] IntegrationConnectionRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = "参数有误" });
        var result = await _svc.UpdateConnectionAsync(id, req);
        if (result == null) return NotFound(new { success = false, message = "连接不存在" });
        return Ok(new { success = true, data = result });
    }

    [HttpDelete("connections/{id:int}")]
    public async Task<IActionResult> DeleteConnection(int id)
    {
        var ok = await _svc.DeleteConnectionAsync(id);
        if (!ok) return NotFound(new { success = false, message = "连接不存在" });
        return Ok(new { success = true, message = "连接已删除" });
    }

    [HttpPost("connections/test")]
    public async Task<IActionResult> TestConnection([FromBody] IntegrationConnectionRequest req)
    {
        try
        {
            var ok = await _svc.TestConnectionAsync(req);
            return Ok(new { success = ok, message = ok ? "连接成功" : "连接失败" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = $"连接失败: {ex.Message}" });
        }
    }

    // ========== 任务管理 ==========

    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasks()
    {
        var list = await _svc.GetTasksAsync();
        return Ok(new { success = true, data = list });
    }

    [HttpPost("tasks")]
    public async Task<IActionResult> CreateTask([FromBody] IntegrationTaskRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = "参数有误" });
        var result = await _svc.CreateTaskAsync(req);
        return Ok(new { success = true, data = result });
    }

    [HttpPut("tasks/{id:int}")]
    public async Task<IActionResult> UpdateTask(int id, [FromBody] IntegrationTaskRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = "参数有误" });
        var result = await _svc.UpdateTaskAsync(id, req);
        if (result == null) return NotFound(new { success = false, message = "任务不存在" });
        return Ok(new { success = true, data = result });
    }

    [HttpDelete("tasks/{id:int}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var ok = await _svc.DeleteTaskAsync(id);
        if (!ok) return NotFound(new { success = false, message = "任务不存在" });
        return Ok(new { success = true, message = "任务已删除" });
    }

    [HttpPost("tasks/{id:int}/execute")]
    public async Task<IActionResult> ExecuteTask(int id)
    {
        try
        {
            var result = await _svc.ExecuteTaskAsync(id);
            return Ok(new { success = result.Success, data = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // ========== 执行日志 ==========

    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int? taskId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _svc.GetLogsAsync(taskId, page, pageSize);
        return Ok(new { success = true, data = result.List, total = result.Total });
    }
}
