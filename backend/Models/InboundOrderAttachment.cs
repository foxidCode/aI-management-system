using System.ComponentModel.DataAnnotations;

namespace backend.Models;

/// <summary>
/// 入库单附件
/// </summary>
public class InboundOrderAttachment
{
    [Key]
    public long Id { get; set; }

    public int InboundOrderId { get; set; }
    public InboundOrder InboundOrder { get; set; } = null!;

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

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
