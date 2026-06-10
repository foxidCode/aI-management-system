namespace backend.DTOs;

// ========== AI Model Config DTOs ==========

public class AiModelConfigRequest
{
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = "openai";
    public string ApiEndpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int MaxTokens { get; set; } = 131072;
    public double Temperature { get; set; } = 0.7;
    public bool IsActive { get; set; } = false;
    public string? SystemPrompt { get; set; }
}

public class AiModelConfigResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = "openai";
    public string ApiEndpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int MaxTokens { get; set; }
    public double Temperature { get; set; }
    public bool IsActive { get; set; }
    public string? SystemPrompt { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

public class AiModelTestRequest
{
    public int? ConfigId { get; set; }
    public string Provider { get; set; } = "openai";
    public string ApiEndpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
}

// ========== Chat DTOs ==========

public class CreateSessionRequest
{
    public string? Title { get; set; }
    public int? ModelConfigId { get; set; }
}

public class ChatSessionResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? ModelConfigId { get; set; }
    public string? ModelName { get; set; }
    public int MessageCount { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

public class SendMessageRequest
{
    public int SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ChatMessageResponse
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

public class ChatMessageListResponse
{
    public List<ChatMessageResponse> Messages { get; set; } = new();
    public string? ModelName { get; set; }
}

// ========== Daily Summary DTOs ==========

public class DailySummaryResponse
{
    public int Id { get; set; }
    public string SummaryDate { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int SessionCount { get; set; }
    public int MessageCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? ReviewedBy { get; set; }
    public string? ReviewerName { get; set; }
    public string? ReviewComment { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string? ReviewedAt { get; set; }
}

public class ReviewSummaryRequest
{
    public string Status { get; set; } = "approved"; // "approved" or "rejected"
    public string? ReviewComment { get; set; }
}

public class DailySummaryListResponse
{
    public List<DailySummaryResponse> List { get; set; } = new();
    public int Total { get; set; }
}

// ========== Knowledge Base DTOs ==========

public class KnowledgeEntryRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = "general";
    public bool IsActive { get; set; } = true;
}

public class KnowledgeEntryResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

public class KnowledgeListResponse
{
    public List<KnowledgeEntryResponse> List { get; set; } = new();
    public int Total { get; set; }
}

public class KnowledgeSearchRequest
{
    public string Keyword { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
}

// ========== Admin Session Management DTOs ==========

public class AdminSessionFilterRequest
{
    public int? UserId { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class AdminSessionResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public int MessageCount { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

public class AdminSessionListResponse
{
    public List<AdminSessionResponse> List { get; set; } = new();
    public int Total { get; set; }
}

public class BatchDeleteRequest
{
    public List<int> Ids { get; set; } = new();
}
