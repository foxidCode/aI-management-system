using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Models;

namespace backend.Services;

public class AttachmentService
{
    private readonly AppDbContext _db;

    public AttachmentService(AppDbContext db) => _db = db;

    // ========== 模块名称 → 显示名称映射 ==========

    private static readonly Dictionary<string, string> ModuleDisplayMap = new()
    {
        ["InboundOrder"] = "入库单",
    };

    public static string GetModuleDisplay(string moduleName) =>
        ModuleDisplayMap.TryGetValue(moduleName, out var display) ? display : moduleName;

    // ========== 统一附件列表 ==========

    public async Task<UnifiedAttachmentListResponse> GetAllAsync(
        string? keyword, string? moduleName, int page = 1, int pageSize = 10)
    {
        var query = _db.Attachments.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(a => a.FileName.ToLower().Contains(kw)
                                     || (a.RelatedName != null && a.RelatedName.ToLower().Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            query = query.Where(a => a.ModuleName == moduleName);
        }

        var total = await query.CountAsync();

        var raw = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.ModuleName,
                a.RelatedId,
                a.RelatedName,
                a.FileName,
                a.ObjectKey,
                a.FileSize,
                a.ContentType,
                UploadedByName = a.Uploader.Username,
                a.CreatedAt,
            })
            .ToListAsync();

        var list = raw.Select(a => new UnifiedAttachmentResponse
        {
            Id = a.Id,
            ModuleName = a.ModuleName,
            ModuleDisplay = GetModuleDisplay(a.ModuleName),
            RelatedId = a.RelatedId,
            RelatedName = a.RelatedName,
            FileName = a.FileName,
            ObjectKey = a.ObjectKey,
            FileSize = a.FileSize,
            ContentType = a.ContentType,
            UploadedByName = a.UploadedByName,
            CreatedAt = a.CreatedAt,
        }).ToList();

        return new UnifiedAttachmentListResponse { List = list, Total = total };
    }

    // ========== 获取所有有附件的模块列表 ==========

    public async Task<List<AttachmentModuleOption>> GetModulesAsync()
    {
        var moduleNames = await _db.Attachments
            .Select(a => a.ModuleName)
            .Distinct()
            .ToListAsync();

        return moduleNames
            .Select(m => new AttachmentModuleOption
            {
                ModuleName = m,
                DisplayName = GetModuleDisplay(m),
            })
            .ToList();
    }

    // ========== 获取某个业务记录的所有附件（供 InboundOrder 等模块详情页调用） ==========

    public async Task<AttachmentListResponse> GetByRecordAsync(
        string moduleName, string relatedId, int page = 1, int pageSize = 20)
    {
        var q = _db.Attachments
            .Where(a => a.ModuleName == moduleName && a.RelatedId == relatedId);

        var total = await q.CountAsync();
        var list = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AttachmentResponse
            {
                Id = a.Id,
                FileName = a.FileName,
                ObjectKey = a.ObjectKey,
                FileSize = a.FileSize,
                ContentType = a.ContentType,
                CreatedAt = a.CreatedAt,
                DownloadUrl = null,
            })
            .ToListAsync();

        return new AttachmentListResponse { List = list, Total = total };
    }

    // ========== 上传附件 ==========

    public async Task<UnifiedAttachmentResponse> AddAsync(
        string moduleName, string relatedId, string? relatedName,
        string fileName, string objectKey, long fileSize,
        string? contentType, int uploadedBy)
    {
        var attachment = new Attachment
        {
            ModuleName = moduleName,
            RelatedId = relatedId,
            RelatedName = relatedName,
            FileName = fileName,
            ObjectKey = objectKey,
            FileSize = fileSize,
            ContentType = contentType,
            UploadedBy = uploadedBy,
            CreatedAt = DateTime.Now,
        };

        _db.Attachments.Add(attachment);
        await _db.SaveChangesAsync();

        var uploaderName = await _db.Users
            .Where(u => u.Id == uploadedBy)
            .Select(u => u.Username)
            .FirstOrDefaultAsync();

        return new UnifiedAttachmentResponse
        {
            Id = attachment.Id,
            ModuleName = attachment.ModuleName,
            ModuleDisplay = GetModuleDisplay(attachment.ModuleName),
            RelatedId = attachment.RelatedId,
            RelatedName = attachment.RelatedName,
            FileName = attachment.FileName,
            ObjectKey = attachment.ObjectKey,
            FileSize = attachment.FileSize,
            ContentType = attachment.ContentType,
            UploadedByName = uploaderName,
            CreatedAt = attachment.CreatedAt,
        };
    }

    // ========== 单个附件查询 ==========

    public async Task<Attachment?> GetByIdAsync(long id)
    {
        return await _db.Attachments.FirstOrDefaultAsync(a => a.Id == id);
    }

    // ========== 删除附件 ==========

    public async Task<bool> DeleteAsync(long id)
    {
        var a = await _db.Attachments.FindAsync(id);
        if (a == null) return false;
        _db.Attachments.Remove(a);
        await _db.SaveChangesAsync();
        return true;
    }
}
