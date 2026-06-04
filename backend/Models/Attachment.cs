using System.ComponentModel.DataAnnotations;

namespace backend.Models;

/// <summary>
/// 统一附件管理 — 所有模块的附件共用此表
/// </summary>
public class Attachment
{
    [Key]
    public long Id { get; set; }

    /// <summary>模块名称（如 InboundOrder, Contract, Quality 等）</summary>
    [Required, MaxLength(100)]
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>关联的业务记录ID（字符串类型，兼容各种ID格式）</summary>
    [Required, MaxLength(100)]
    public string RelatedId { get; set; } = string.Empty;

    /// <summary>关联记录的可读名称（如入库单编码），用于列表展示，避免JOIN</summary>
    [MaxLength(200)]
    public string? RelatedName { get; set; }

    /// <summary>原始文件名</summary>
    [Required, MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>MinIO 对象名（bucket 内唯一 key）</summary>
    [Required, MaxLength(500)]
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>文件大小（字节）</summary>
    public long FileSize { get; set; }

    /// <summary>MIME 类型</summary>
    [MaxLength(200)]
    public string? ContentType { get; set; }

    /// <summary>上传人ID</summary>
    public int UploadedBy { get; set; }
    public User Uploader { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
