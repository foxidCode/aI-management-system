using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttachmentController : ControllerBase
{
    private readonly AttachmentService _attachmentService;

    public AttachmentController(AttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    private int UserId => int.Parse(User.Claims
        .First(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier").Value);

    // ========== 统一附件列表 ==========

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] string? moduleName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _attachmentService.GetAllAsync(keyword, moduleName, page, pageSize);
        return Ok(new { success = true, data = result.List, total = result.Total });
    }

    // ========== 模块列表（用于筛选下拉框） ==========

    [HttpGet("modules")]
    public async Task<IActionResult> GetModules()
    {
        var modules = await _attachmentService.GetModulesAsync();
        return Ok(new { success = true, data = modules });
    }

    // ========== 上传附件 ==========

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        [FromForm] List<IFormFile> files,
        [FromForm] string moduleName,
        [FromForm] string relatedId,
        [FromForm] string? relatedName)
    {
        if (files == null || files.Count == 0)
            return BadRequest(new { success = false, message = "请选择要上传的文件" });

        var minio = HttpContext.RequestServices.GetRequiredService<MinioService>();
        var results = new List<UnifiedAttachmentResponse>();

        foreach (var file in files)
        {
            var objectKey = $"attachments/{moduleName}/{relatedId}/{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            using var stream = file.OpenReadStream();
            await minio.UploadAsync(stream, objectKey, file.ContentType ?? "application/octet-stream", file.Length);

            var attachment = await _attachmentService.AddAsync(
                moduleName, relatedId, relatedName,
                file.FileName, objectKey, file.Length,
                file.ContentType, UserId);

            results.Add(attachment);
        }

        return Ok(new { success = true, data = results, message = $"已上传 {results.Count} 个附件" });
    }

    // ========== 下载附件 ==========

    [HttpGet("{attachmentId:long}/download")]
    public async Task<IActionResult> Download(long attachmentId)
    {
        var a = await _attachmentService.GetByIdAsync(attachmentId);
        if (a == null) return NotFound(new { success = false, message = "附件不存在" });

        var minio = HttpContext.RequestServices.GetRequiredService<MinioService>();
        var stream = await minio.DownloadAsync(a.ObjectKey);
        return File(stream, a.ContentType ?? "application/octet-stream", a.FileName);
    }

    // ========== 删除附件 ==========

    [HttpDelete("{attachmentId:long}")]
    public async Task<IActionResult> Delete(long attachmentId)
    {
        var a = await _attachmentService.GetByIdAsync(attachmentId);
        if (a == null) return NotFound(new { success = false, message = "附件不存在" });

        var minio = HttpContext.RequestServices.GetRequiredService<MinioService>();
        try { await minio.DeleteAsync(a.ObjectKey); } catch { /* MinIO 删除失败不阻塞 */ }

        await _attachmentService.DeleteAsync(attachmentId);
        return Ok(new { success = true, message = "附件已删除" });
    }
}
