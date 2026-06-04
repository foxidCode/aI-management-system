using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PermissionController : ControllerBase
{
    private readonly PermissionService _permService;

    public PermissionController(PermissionService permService)
    {
        _permService = permService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var permissions = await _permService.GetAllPermissionsAsync();
        return Ok(new { success = true, data = permissions });
    }

    [HttpGet("tree")]
    public async Task<IActionResult> GetPermissionTree()
    {
        var tree = await _permService.GetPermissionTreeAsync();
        return Ok(new { success = true, data = tree });
    }
}
