using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DatabaseController : ControllerBase
{
    private readonly DatabaseService _dbService;

    public DatabaseController(DatabaseService dbService) => _dbService = dbService;

    /// <summary>数据库管理仅限admin账号使用（拥有 role:manage 权限）</summary>
    private bool IsAdmin =>
        User.Claims.Any(c => c.Type == "permission" && c.Value == "role:manage");

    private IActionResult? CheckAdmin()
    {
        if (!IsAdmin)
            return Forbid();
        return null;
    }

    // ========== 连接配置管理 ==========

    [HttpGet("connections")]
    public async Task<IActionResult> GetConnections()
    {
        var admin = CheckAdmin(); if (admin != null) return admin;
        var list = await _dbService.GetConnectionsAsync();
        return Ok(new { success = true, data = list });
    }

    [HttpPost("connections")]
    public async Task<IActionResult> CreateConnection([FromBody] DbConnectionRequest req)
    {
        var admin = CheckAdmin(); if (admin != null) return admin;
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return BadRequest(new { success = false, message = "参数有误", errors });
        }
        var result = await _dbService.CreateConnectionAsync(req);
        return Ok(new { success = true, data = result });
    }

    [HttpPut("connections/{id:int}")]
    public async Task<IActionResult> UpdateConnection(int id, [FromBody] DbConnectionRequest req)
    {
        var admin = CheckAdmin(); if (admin != null) return admin;
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return BadRequest(new { success = false, message = "参数有误", errors });
        }
        var result = await _dbService.UpdateConnectionAsync(id, req);
        if (result == null) return NotFound(new { success = false, message = "连接配置不存在" });
        return Ok(new { success = true, data = result });
    }

    [HttpDelete("connections/{id:int}")]
    public async Task<IActionResult> DeleteConnection(int id)
    {
        var admin = CheckAdmin(); if (admin != null) return admin;
        var ok = await _dbService.DeleteConnectionAsync(id);
        if (!ok) return NotFound(new { success = false, message = "连接配置不存在" });
        return Ok(new { success = true, message = "连接配置已删除" });
    }

    // ========== 测试连接 ==========

    [HttpPost("test-connection")]
    public async Task<IActionResult> TestConnection([FromBody] DbConnectionRequest req)
    {
        var admin = CheckAdmin(); if (admin != null) return admin;
        try
        {
            await _dbService.TestConnectionAsync(req);
            return Ok(new { success = true, message = "连接成功" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = $"连接失败: {ex.Message}" });
        }
    }

    // ========== 浏览表 ==========

    [HttpGet("{id:int}/tables")]
    public async Task<IActionResult> GetTables(int id)
    {
        var admin = CheckAdmin(); if (admin != null) return admin;
        try
        {
            var tables = await _dbService.GetTablesAsync(id);
            return Ok(new { success = true, data = tables });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("{id:int}/tables/{table}/schema")]
    public async Task<IActionResult> GetTableSchema(int id, string table)
    {
        var admin = CheckAdmin(); if (admin != null) return admin;
        try
        {
            var columns = await _dbService.GetTableSchemaAsync(id, table);
            return Ok(new { success = true, data = columns });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // ========== 执行 SQL ==========

    [HttpPost("{id:int}/execute")]
    public async Task<IActionResult> ExecuteSql(int id, [FromBody] ExecuteSqlRequest req)
    {
        var admin = CheckAdmin(); if (admin != null) return admin;
        try
        {
            var result = await _dbService.ExecuteSqlAsync(id, req.Sql);
            return Ok(new { success = true, data = result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = $"执行失败: {ex.Message}" });
        }
    }
}
