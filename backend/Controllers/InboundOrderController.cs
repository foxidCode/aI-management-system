using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.DTOs;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InboundOrderController : ControllerBase
{
    private readonly InboundOrderService _svc;

    public InboundOrderController(InboundOrderService svc) => _svc = svc;

    private int UserId => int.Parse(User.Claims.First(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier").Value);

    // ========== 入库单 CRUD ==========

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortField = null,
        [FromQuery] string? sortOrder = null)
    {
        var result = await _svc.GetOrdersAsync(keyword, page, pageSize, sortField, sortOrder);
        return Ok(new { success = true, data = result.List, total = result.Total });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var o = await _svc.GetOrderByIdAsync(id);
        if (o == null) return NotFound(new { success = false, message = "入库单不存在" });
        return Ok(new { success = true, data = o });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInboundOrderRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = GetModelError() });
        try
        {
            var o = await _svc.CreateOrderAsync(req, UserId);
            return Ok(new { success = true, data = o });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateInboundOrderRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = GetModelError() });
        var o = await _svc.UpdateOrderAsync(id, req);
        if (o == null) return NotFound(new { success = false, message = "入库单不存在" });
        return Ok(new { success = true, data = o });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _svc.DeleteOrderAsync(id);
        if (!ok) return NotFound(new { success = false, message = "入库单不存在" });
        return Ok(new { success = true, message = "入库单已删除" });
    }

    // ========== 明细管理 ==========

    [HttpPut("{orderId}/details/sync")]
    public async Task<IActionResult> SyncDetails(int orderId, [FromBody] List<CreateInboundDetailRequest> details)
    {
        try
        {
            await _svc.SyncDetailsAsync(orderId, details);
            return Ok(new { success = true, message = "明细已同步" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("{orderId:int}/details")]
    public async Task<IActionResult> GetDetails(int orderId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _svc.GetDetailsAsync(orderId, page, pageSize);
        return Ok(new { success = true, data = result.List, total = result.Total });
    }

    [HttpPost("{orderId}/details")]
    public async Task<IActionResult> AddDetail(int orderId, [FromBody] CreateInboundDetailRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(new { success = false, message = GetModelError() });
        try
        {
            var d = await _svc.AddDetailAsync(orderId, req);
            return Ok(new { success = true, data = d });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("details/{detailId:long}")]
    public async Task<IActionResult> UpdateDetail(long detailId, [FromBody] UpdateInboundDetailRequest req)
    {
        var d = await _svc.UpdateDetailAsync(detailId, req);
        if (d == null) return NotFound(new { success = false, message = "明细不存在" });
        return Ok(new { success = true, data = d });
    }

    [HttpDelete("details/{detailId:long}")]
    public async Task<IActionResult> DeleteDetail(long detailId)
    {
        var ok = await _svc.DeleteDetailAsync(detailId);
        if (!ok) return NotFound(new { success = false, message = "明细不存在" });
        return Ok(new { success = true, message = "明细已删除" });
    }

    [HttpPost("details/batch-delete")]
    public async Task<IActionResult> BatchDeleteDetails([FromBody] List<long> detailIds)
    {
        var count = 0;
        foreach (var id in detailIds)
        {
            if (await _svc.DeleteDetailAsync(id)) count++;
        }
        return Ok(new { success = true, message = $"已删除 {count} 条明细" });
    }

    // ========== 附件管理 ==========

    [HttpGet("{orderId}/attachments")]
    public async Task<IActionResult> GetAttachments(int orderId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _svc.GetAttachmentsAsync(orderId, page, pageSize);
        return Ok(new { success = true, data = result.List, total = result.Total });
    }

    [HttpPost("{orderId}/attachments")]
    public async Task<IActionResult> UploadAttachments(int orderId, [FromForm] List<IFormFile> files)
    {
        var minio = HttpContext.RequestServices.GetRequiredService<MinioService>();
        var results = new List<AttachmentResponse>();
        foreach (var file in files)
        {
            var objectKey = $"inbound/{orderId}/{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            using var stream = file.OpenReadStream();
            await minio.UploadAsync(stream, objectKey, file.ContentType ?? "application/octet-stream", file.Length);
            var attachment = await _svc.AddAttachmentAsync(orderId, file.FileName, objectKey, file.Length, file.ContentType, UserId);
            results.Add(attachment);
        }
        return Ok(new { success = true, data = results, message = $"已上传 {results.Count} 个附件" });
    }

    [HttpGet("attachments/{attachmentId:long}/download")]
    public async Task<IActionResult> DownloadAttachment(long attachmentId)
    {
        var a = await _svc.GetAttachmentAsync(attachmentId);
        if (a == null) return NotFound(new { success = false, message = "附件不存在" });
        var minio = HttpContext.RequestServices.GetRequiredService<MinioService>();
        var stream = await minio.DownloadAsync(a.ObjectKey);
        return File(stream, a.ContentType ?? "application/octet-stream", a.FileName);
    }

    [HttpDelete("attachments/{attachmentId:long}")]
    public async Task<IActionResult> DeleteAttachment(long attachmentId)
    {
        var a = await _svc.GetAttachmentAsync(attachmentId);
        if (a == null) return NotFound(new { success = false, message = "附件不存在" });
        var minio = HttpContext.RequestServices.GetRequiredService<MinioService>();
        try { await minio.DeleteAsync(a.ObjectKey); } catch { /* MinIO 删除失败不阻塞 */ }
        await _svc.DeleteAttachmentAsync(attachmentId);
        return Ok(new { success = true, message = "附件已删除" });
    }

    // ========== 统计 ==========

    [HttpGet("daily-stats")]
    public async Task<IActionResult> GetDailyStats([FromQuery] int days = 7)
    {
        var data = await _svc.GetDailyAmountsAsync(days);
        return Ok(new { success = true, data });
    }

    // ========== 导出 CSV ==========

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? keyword,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 0)
    {
        var list = await _svc.GetAllOrdersForExportAsync(keyword, page, pageSize);
        var sb = new StringBuilder();
        sb.AppendLine("﻿单据编码,库房名称,供应商,合同,含税金额,计成本金额,税额,税率(%),备注,创建人,创建日期,更新日期");
        foreach (var o in list)
        {
            sb.AppendLine($"{o.OrderCode},{o.WarehouseName},{o.Supplier},{o.Contract}," +
                $"{o.TotalTaxIncludedAmount:F2},{o.TotalCostAmount:F2},{o.TotalTaxAmount:F2}," +
                $"{o.TaxRate},{o.Remark},{o.CreatedByName},{o.CreatedAt:yyyy-MM-dd HH:mm},{o.UpdatedAt:yyyy-MM-dd HH:mm}");
        }
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"入库单_{DateTime.Now:yyyyMMddHHmmss}.csv");
    }

    private string GetModelError()
    {
        var e = ModelState.Values.SelectMany(v => v.Errors).Select(x => x.ErrorMessage).Where(m => !string.IsNullOrEmpty(m));
        return string.Join("；", e);
    }
}
