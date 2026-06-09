namespace backend.DTOs;

// ========== 操作日志 DTO ==========

/// <summary>操作日志查询请求</summary>
public class OperationLogQueryRequest
{
    public string? Keyword { get; set; }
    public string? Action { get; set; }
    public int? UserId { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>操作日志响应</summary>
public class OperationLogResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? TargetName { get; set; }
    public string? Detail { get; set; }
    public bool IsSensitive { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

/// <summary>操作日志列表响应</summary>
public class OperationLogListResponse
{
    public List<OperationLogResponse> List { get; set; } = new();
    public int Total { get; set; }
}

// ========== 权限变更日志 DTO ==========

/// <summary>权限变更日志查询请求</summary>
public class PermissionChangeLogQueryRequest
{
    public string? Keyword { get; set; }
    public int? TargetUserId { get; set; }
    public string? ChangeType { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>权限变更日志响应</summary>
public class PermissionChangeLogResponse
{
    public int Id { get; set; }
    public int? TargetUserId { get; set; }
    public string? TargetUsername { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string PermissionCode { get; set; } = string.Empty;
    public string? PermissionName { get; set; }
    public int OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public string? OperatorIp { get; set; }
    public string? Detail { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

/// <summary>权限变更日志列表响应</summary>
public class PermissionChangeLogListResponse
{
    public List<PermissionChangeLogResponse> List { get; set; } = new();
    public int Total { get; set; }
}

// ========== 操作统计 DTO ==========

/// <summary>操作统计响应</summary>
public class OperationStatsResponse
{
    /// <summary>总操作数</summary>
    public int TotalOperations { get; set; }

    /// <summary>敏感操作数</summary>
    public int SensitiveOperations { get; set; }

    /// <summary>今日操作数</summary>
    public int TodayOperations { get; set; }

    /// <summary>异常告警数</summary>
    public int AnomalyCount { get; set; }

    /// <summary>按操作类型的分布</summary>
    public List<ActionDistribution> ActionDistribution { get; set; } = new();
}

public class ActionDistribution
{
    public string Action { get; set; } = string.Empty;
    public int Count { get; set; }
}

// ========== 异常告警 DTO ==========

/// <summary>异常告警项</summary>
public class AnomalyAlertResponse
{
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning";
    public string Message { get; set; } = string.Empty;
    public int? RelatedUserId { get; set; }
    public string? RelatedUsername { get; set; }
    public int? RelatedLogId { get; set; }
    public string DetectedAt { get; set; } = string.Empty;
}
