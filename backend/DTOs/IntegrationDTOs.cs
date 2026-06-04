using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

// ========== 连接配置 ==========

public class IntegrationConnectionRequest
{
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    [Required, MaxLength(500)] public string BaseUrl { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string AuthType { get; set; } = "None";
    [MaxLength(2000)] public string? AuthConfig { get; set; }
    [MaxLength(2000)] public string? DefaultHeaders { get; set; }
}

public class IntegrationConnectionResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string AuthType { get; set; } = string.Empty;
    public string? DefaultHeaders { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ========== 任务 ==========

public class IntegrationTaskRequest
{
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    public int? SourceConnectionId { get; set; }
    [MaxLength(500)] public string? SourcePath { get; set; }
    [MaxLength(10)] public string SourceMethod { get; set; } = "GET";
    [MaxLength(100)] public string SourceContentType { get; set; } = "application/json";
    public string? SourceBody { get; set; }
    [MaxLength(200)] public string? ResponseDataPath { get; set; }
    [MaxLength(20)] public string TargetType { get; set; } = "Api";
    public int? TargetConnectionId { get; set; }
    [MaxLength(500)] public string? TargetPath { get; set; }
    [MaxLength(100)] public string TargetContentType { get; set; } = "application/json";
    [MaxLength(10)] public string TargetMethod { get; set; } = "POST";
    [MaxLength(200)] public string? DbTableName { get; set; }
    public string? DbChildConfig { get; set; }
    public string? FieldMappings { get; set; }
    public string? CodeHandler { get; set; }
    public string? BeforeExecute { get; set; }
    public string? AfterExecute { get; set; }
    public bool IsActive { get; set; } = true;
}

public class IntegrationTaskResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? SourceConnectionId { get; set; }
    public string? SourceConnectionName { get; set; }
    public string? SourcePath { get; set; }
    public string SourceMethod { get; set; } = "GET";
    public string SourceContentType { get; set; } = "application/json";
    public string? SourceBody { get; set; }
    public string? ResponseDataPath { get; set; }
    public string TargetType { get; set; } = "Api";
    public int? TargetConnectionId { get; set; }
    public string? TargetConnectionName { get; set; }
    public string? TargetPath { get; set; }
    public string TargetContentType { get; set; } = "application/json";
    public string TargetMethod { get; set; } = "POST";
    public string? DbTableName { get; set; }
    public string? DbChildConfig { get; set; }
    public string? FieldMappings { get; set; }
    public string? CodeHandler { get; set; }
    public string? BeforeExecute { get; set; }
    public string? AfterExecute { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ========== 子表配置 ==========

public class DbChildTableConfig
{
    public string TableName { get; set; } = string.Empty;
    public string ForeignKey { get; set; } = string.Empty;
    public string SourceField { get; set; } = string.Empty;
    public string Mappings { get; set; } = "[]";
}

// ========== 字段映射定义 ==========

public class FieldMappingDef
{
    public string SourceField { get; set; } = string.Empty;
    public string TargetField { get; set; } = string.Empty;
    public string Transform { get; set; } = "none";
    public string? DefaultValue { get; set; }
    public string? Format { get; set; }
}

// ========== 执行 ==========

public class ExecuteTaskResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? PullResponse { get; set; }
    public string? PushResponse { get; set; }
    public string? TransformedData { get; set; }
    public long DurationMs { get; set; }
}

// ========== 日志 ==========

public class IntegrationLogResponse
{
    public long Id { get; set; }
    public int TaskId { get; set; }
    public string? TaskName { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RequestUrl { get; set; }
    public string? RequestBody { get; set; }
    public string? ResponseData { get; set; }
    public string? RequestHeaders { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }
    public DateTime ExecutedAt { get; set; }
}

public class IntegrationLogListResponse
{
    public List<IntegrationLogResponse> List { get; set; } = new();
    public int Total { get; set; }
}

// ========== 计划任务 DTO ==========

public class ScheduledTaskRequest
{
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    /// <summary>关联的集成任务 ID，0 表示纯代码模式</summary>
    public int IntegrationTaskId { get; set; }
    /// <summary>执行模式：cron = 定时重复，once = 仅执行一次</summary>
    [MaxLength(20)] public string RunMode { get; set; } = "cron";
    [MaxLength(100)] public string? CronExpression { get; set; }
    /// <summary>仅执行一次的时间（本地时间）</summary>
    public DateTime? RunOnceAt { get; set; }
    /// <summary>C# 自定义脚本，可直接写代码执行（不依赖集成任务）</summary>
    public string? CodeHandler { get; set; }
    /// <summary>执行器类全路径</summary>
    [MaxLength(500)] public string? HandlerClass { get; set; }
    /// <summary>执行器参数（JSON Key-Value），如 {"days":"30"}</summary>
    [MaxLength(2000)] public string? HandlerParameters { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class ScheduledTaskResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int IntegrationTaskId { get; set; }
    public string? IntegrationTaskName { get; set; }
    public string RunMode { get; set; } = "cron";
    public string? CronExpression { get; set; }
    public DateTime? RunOnceAt { get; set; }
    public string? CodeHandler { get; set; }
    public string? HandlerClass { get; set; }
    public string? HandlerParameters { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public string? LastRunStatus { get; set; }
    public long? LastRunDurationMs { get; set; }
    public string? LastRunMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Cron 的人类可读描述</summary>
    public string CronDescription { get; set; } = string.Empty;
}
