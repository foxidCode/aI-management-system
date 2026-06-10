using System.ComponentModel.DataAnnotations;

namespace backend.Models;

/// <summary>
/// 每日 AI 对话总结：由 AI 自动生成，需管理员审批
/// </summary>
public class DailySummary
{
    [Key]
    public int Id { get; set; }

    /// <summary>总结日期（yyyy-MM-dd）</summary>
    [Required, MaxLength(10)]
    public string SummaryDate { get; set; } = string.Empty;

    /// <summary>AI 生成总结内容</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>当天会话数</summary>
    public int SessionCount { get; set; } = 0;

    /// <summary>当天消息总数</summary>
    public int MessageCount { get; set; } = 0;

    /// <summary>状态：pending / approved / rejected</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "pending";

    /// <summary>审批人</summary>
    public int? ReviewedBy { get; set; }

    /// <summary>审批意见</summary>
    public string? ReviewComment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? ReviewedAt { get; set; }
}
