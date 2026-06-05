using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/workflow/[controller]")]
[Authorize]
public class DefinitionsController : ControllerBase
{
    private readonly WorkflowDefinitionService _svc;

    public DefinitionsController(WorkflowDefinitionService svc)
    {
        _svc = svc;
    }

    /// <summary>获取流程定义列表</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _svc.GetAllDefinitionsAsync(keyword, page, pageSize);
        return Ok(new { success = true, data = result.List, total = result.Total });
    }

    /// <summary>获取单个流程定义</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _svc.GetDefinitionByIdAsync(id);
        if (item == null) return NotFound(new { success = false, message = "流程定义不存在" });
        return Ok(new { success = true, data = item });
    }

    /// <summary>创建流程定义</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowDefinitionRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = GetModelError() });
        try
        {
            var item = await _svc.CreateDefinitionAsync(req);
            return Ok(new { success = true, data = item });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>更新流程定义</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateWorkflowDefinitionRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = GetModelError() });
        var item = await _svc.UpdateDefinitionAsync(id, req);
        if (item == null) return NotFound(new { success = false, message = "流程定义不存在" });
        return Ok(new { success = true, data = item });
    }

    /// <summary>删除流程定义（软删除）</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _svc.DeleteDefinitionAsync(id);
        if (!ok) return NotFound(new { success = false, message = "流程定义不存在" });
        return Ok(new { success = true, message = "流程定义已删除" });
    }

    private string GetModelError()
    {
        var e = ModelState.Values.SelectMany(v => v.Errors)
            .Select(x => x.ErrorMessage).Where(m => !string.IsNullOrEmpty(m));
        return string.Join("；", e);
    }
}
