using System.ComponentModel.DataAnnotations;

namespace backend.Models;

/// <summary>
/// AI 对话消息：单条聊天记录
/// </summary>
public class ChatMessage
{
    [Key]
    public int Id { get; set; }

    /// <summary>所属会话</summary>
    public int SessionId { get; set; }

    /// <summary>角色：user / assistant / system</summary>
    [Required, MaxLength(20)]
    public string Role { get; set; } = "user";

    /// <summary>消息内容</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>Token 估算数</summary>
    public int TokenCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
