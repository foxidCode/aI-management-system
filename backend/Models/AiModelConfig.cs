using System.ComponentModel.DataAnnotations;

namespace backend.Models;

/// <summary>
/// AI 模型配置：存储可接入的 AI 模型（DeepSeek、ChatGPT、Claude 等）
/// </summary>
public class AiModelConfig
{
    [Key]
    public int Id { get; set; }

    /// <summary>显示名称，如 "DeepSeek V3"</summary>
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>提供商类型：openai / anthropic</summary>
    [Required, MaxLength(50)]
    public string Provider { get; set; } = "openai";

    /// <summary>API 基础地址</summary>
    [Required, MaxLength(500)]
    public string ApiEndpoint { get; set; } = string.Empty;

    /// <summary>API Key（建议加密存储）</summary>
    [MaxLength(500)]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>模型名称，如 "deepseek-chat", "gpt-4o", "claude-sonnet-4-6"</summary>
    [Required, MaxLength(100)]
    public string ModelName { get; set; } = string.Empty;

    /// <summary>最大 Token 数</summary>
    public int MaxTokens { get; set; } = 131072;

    /// <summary>温度（0-2），控制随机性</summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>是否启用（同时只能启用一个）</summary>
    public bool IsActive { get; set; } = false;

    /// <summary>自定义系统提示词（可选）</summary>
    public string? SystemPrompt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
