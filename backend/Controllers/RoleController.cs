using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RoleController : ControllerBase
{
    private readonly PermissionService _permService;

    public RoleController(PermissionService permService)
    {
        _permService = permService;
    }

    private bool IsAdmin() =>
        User.Claims.Any(c => c.Type == "permission" && c.Value == "role:manage");

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 0)
    {
        if (!IsAdmin()) return Forbid();
        var result = await _permService.GetAllRolesAsync(keyword, page, pageSize);
        return Ok(new { success = true, data = result.List, total = result.Total });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!IsAdmin()) return Forbid();
        var role = await _permService.GetRoleByIdAsync(id);
        if (role == null)
            return NotFound(new { success = false, message = "角色不存在" });
        return Ok(new { success = true, data = role });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request)
    {
        if (!IsAdmin()) return Forbid();
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = GetModelError() });

        try
        {
            var role = await _permService.CreateRoleAsync(request);
            return Ok(new { success = true, data = role });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRoleRequest request)
    {
        if (!IsAdmin()) return Forbid();
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = GetModelError() });

        try
        {
            var role = await _permService.UpdateRoleAsync(id, request);
            if (role == null)
                return NotFound(new { success = false, message = "角色不存在" });
            return Ok(new { success = true, data = role });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdmin()) return Forbid();
        var result = await _permService.DeleteRoleAsync(id);
        if (!result)
            return NotFound(new { success = false, message = "角色不存在" });
        return Ok(new { success = true, message = "角色已删除" });
    }

    private string GetModelError()
    {
        var errors = ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .Where(m => !string.IsNullOrEmpty(m));
        return string.Join("；", errors);
    }
}
