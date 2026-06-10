using System.ComponentModel.DataAnnotations;

namespace backend.Models;

/// <summary>
/// AI 对话会话：一次完整对话
/// </summary>
public class ChatSession
{
    [Key]
    public int Id { get; set; }

    /// <summary>所属用户</summary>
    public int UserId { get; set; }

    /// <summary>会话标题（从第一条消息自动生成）</summary>
    [MaxLength(200)]
    public string Title { get; set; } = "新对话";

    /// <summary>使用的模型配置</summary>
    public int? ModelConfigId { get; set; }

    /// <summary>消息数量（冗余，方便列表显示）</summary>
    public int MessageCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
