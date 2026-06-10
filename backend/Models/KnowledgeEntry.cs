using System.ComponentModel.DataAnnotations;

namespace backend.Models;

/// <summary>
/// 知识库条目：AI 回答前检索的知识来源
/// </summary>
public class KnowledgeEntry
{
    [Key]
    public int Id { get; set; }

    /// <summary>标题</summary>
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>内容</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>分类：getting-started / faq / feature-guide 等</summary>
    [MaxLength(50)]
    public string Category { get; set; } = "general";

    /// <summary>来源：system=系统初始 / summary:{id}=来自总结审批</summary>
    [MaxLength(50)]
    public string Source { get; set; } = "system";

    /// <summary>是否启用</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
