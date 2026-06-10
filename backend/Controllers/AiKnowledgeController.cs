using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiKnowledgeController : ControllerBase
{
    private readonly AiKnowledgeService _knowledgeService;

    public AiKnowledgeController(AiKnowledgeService knowledgeService)
    {
        _knowledgeService = knowledgeService;
    }

    private bool IsAdmin() =>
        User.Claims.Any(c => c.Type == "permission" && (c.Value == "role:manage" || c.Value == "ai:config"));

    // ========== Search (public for chat usage) ==========

    /// <summary>搜索知识库（AI 对话时内部调用）</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string keyword, [FromQuery] int topK = 5)
    {
        var results = await _knowledgeService.SearchAsync(keyword, topK);
        return Ok(new { success = true, data = results });
    }

    /// <summary>获取所有分类</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _knowledgeService.GetCategoriesAsync();
        return Ok(new { success = true, data = categories });
    }

    // ========== CRUD (Admin only) ==========

    /// <summary>获取知识库列表</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var list = await _knowledgeService.GetAllAsync(category, page, pageSize);
        var total = await _knowledgeService.GetTotalCountAsync(category);
        return Ok(new { success = true, data = list, total });
    }

    /// <summary>获取单条知识</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var entry = await _knowledgeService.GetByIdAsync(id);
        if (entry == null) return NotFound(new { success = false, message = "知识条目不存在" });
        return Ok(new { success = true, data = entry });
    }

    /// <summary>创建知识条目</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] KnowledgeEntryRequest req)
    {
        if (!IsAdmin()) return Forbid();

        var entry = await _knowledgeService.CreateAsync(req);
        return Ok(new { success = true, data = entry });
    }

    /// <summary>更新知识条目</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] KnowledgeEntryRequest req)
    {
        if (!IsAdmin()) return Forbid();

        var entry = await _knowledgeService.UpdateAsync(id, req);
        if (entry == null) return NotFound(new { success = false, message = "知识条目不存在" });
        return Ok(new { success = true, data = entry });
    }

    /// <summary>删除知识条目</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdmin()) return Forbid();

        var ok = await _knowledgeService.DeleteAsync(id);
        if (!ok) return NotFound(new { success = false, message = "知识条目不存在" });
        return Ok(new { success = true });
    }
}
