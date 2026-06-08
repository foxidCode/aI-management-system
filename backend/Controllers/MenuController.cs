using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MenuController : ControllerBase
{
    private readonly PermissionService _permService;

    public MenuController(PermissionService permService)
    {
        _permService = permService;
    }

    private bool IsAdmin() =>
        User.Claims.Any(c => c.Type == "permission" && c.Value == "role:manage");

    [HttpGet]
    public async Task<IActionResult> GetUserMenus()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { success = false, message = "无效的Token" });

        var menus = await _permService.GetMenusForUserAsync(userId);
        return Ok(new { success = true, data = menus });
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAllMenus()
    {
        if (!IsAdmin()) return Forbid();
        var menus = await _permService.GetAllMenusAsync();
        return Ok(new { success = true, data = menus });
    }

    [HttpPost]
    public async Task<IActionResult> CreateMenu([FromBody] CreateMenuRequest req)
    {
        if (!IsAdmin()) return Forbid();
        try
        {
            var menu = await _permService.CreateMenuAsync(req);
            return Ok(new { success = true, data = menu });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMenu(int id, [FromBody] UpdateMenuRequest req)
    {
        if (!IsAdmin()) return Forbid();
        try
        {
            var menu = await _permService.UpdateMenuAsync(id, req);
            if (menu == null) return NotFound(new { success = false, message = "菜单不存在" });
            return Ok(new { success = true, data = menu });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMenu(int id)
    {
        if (!IsAdmin()) return Forbid();
        var ok = await _permService.DeleteMenuAsync(id);
        if (!ok) return NotFound(new { success = false, message = "菜单不存在" });
        return Ok(new { success = true, message = "已删除" });
    }

    [HttpPut("batch")]
    public async Task<IActionResult> BatchUpdateMenus([FromBody] BatchUpdateMenusRequest req)
    {
        if (!IsAdmin()) return Forbid();
        try
        {
            await _permService.BatchUpdateMenusAsync(req);
            return Ok(new { success = true, message = "排序已更新" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}
