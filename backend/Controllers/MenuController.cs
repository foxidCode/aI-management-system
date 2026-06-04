using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
