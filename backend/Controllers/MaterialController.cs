using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MaterialController : ControllerBase
{
    private readonly InboundOrderService _svc;

    public MaterialController(InboundOrderService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 0)
    {
        var result = await _svc.GetMaterialsAsync(keyword, page, pageSize);
        return Ok(new { success = true, data = result.List, total = result.Total });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var m = await _svc.GetMaterialByIdAsync(id);
        if (m == null) return NotFound(new { success = false, message = "材料不存在" });
        return Ok(new { success = true, data = m });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMaterialRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = GetModelError() });
        try
        {
            var m = await _svc.CreateMaterialAsync(req);
            return Ok(new { success = true, data = m });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMaterialRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = GetModelError() });
        var m = await _svc.UpdateMaterialAsync(id, req);
        if (m == null) return NotFound(new { success = false, message = "材料不存在" });
        return Ok(new { success = true, data = m });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _svc.DeleteMaterialAsync(id);
        if (!ok) return NotFound(new { success = false, message = "材料不存在" });
        return Ok(new { success = true, message = "材料已删除" });
    }

    [HttpPost("batch-delete")]
    public async Task<IActionResult> BatchDelete([FromBody] List<int> ids)
    {
        var count = 0;
        foreach (var id in ids)
        {
            if (await _svc.DeleteMaterialAsync(id)) count++;
        }
        return Ok(new { success = true, message = $"已删除 {count} 条材料" });
    }

    private string GetModelError()
    {
        var e = ModelState.Values.SelectMany(v => v.Errors).Select(x => x.ErrorMessage).Where(m => !string.IsNullOrEmpty(m));
        return string.Join("；", e);
    }
}
