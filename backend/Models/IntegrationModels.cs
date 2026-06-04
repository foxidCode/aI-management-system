using System.ComponentModel.DataAnnotations;

namespace backend.Models;

/// <summary>
/// 集成平台 - HTTP API 连接配置（密码/Auth加密存储）
/// </summary>
public class IntegrationConnection
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required, MaxLength(500)]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>认证类型：None, Basic, Bearer, ApiKey, Chain</summary>
    [Required, MaxLength(50)]
    public string AuthType { get; set; } = "None";

    /// <summary>认证配置（JSON）。
    /// Basic: {"username":"xxx","password":"xxx"}
    /// Bearer: {"token":"xxx"} 或 {"loginUrl":"/api/auth/login","loginBody":"...","tokenField":"data.token"}
    /// ApiKey: {"key":"X-Api-Key","value":"xxx","in":"Header"}
    /// Chain: {"steps":[{"url":"...","method":"POST","body":"...","extractField":"data.xxx","saveAs":"var"}],"headerName":"X-Token","headerTemplate":"{{var}}"}</summary>
    [MaxLength(5000)]
    public string? AuthConfig { get; set; }

    /// <summary>默认请求头（JSON），如 {"X-Custom":"value"}</summary>
    [MaxLength(2000)]
    public string? DefaultHeaders { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 集成平台 - 数据同步任务
/// </summary>
public class IntegrationTask
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>数据来源连接ID（为空表示手动触发）</summary>
    public int? SourceConnectionId { get; set; }

    /// <summary>数据来源的API路径，如 /api/users</summary>
    [MaxLength(500)]
    public string? SourcePath { get; set; }

    /// <summary>来源请求方法：GET, POST</summary>
    [MaxLength(10)]
    public string SourceMethod { get; set; } = "GET";

    /// <summary>来源 Content-Type，默认 application/json，也支持 application/xml、text/xml 等</summary>
    [MaxLength(100)]
    public string SourceContentType { get; set; } = "application/json";

    /// <summary>来源请求体（POST时使用，JSON 或 XML 字符串）</summary>
    public string? SourceBody { get; set; }

    /// <summary>从响应中提取数据的 JSON 路径，如 data.rows、data.items；留空表示响应本身就是数组</summary>
    [MaxLength(200)]
    public string? ResponseDataPath { get; set; }

    /// <summary>目标类型：Api=推送到外部API, Database=存入本系统数据库</summary>
    [MaxLength(20)]
    public string TargetType { get; set; } = "Api";

    /// <summary>数据目标连接ID（TargetType=Api时使用）</summary>
    public int? TargetConnectionId { get; set; }

    /// <summary>数据目标的API路径</summary>
    [MaxLength(500)]
    public string? TargetPath { get; set; }

    /// <summary>目标 Content-Type，默认 application/json</summary>
    [MaxLength(100)]
    public string TargetContentType { get; set; } = "application/json";

    /// <summary>目标请求方法</summary>
    [MaxLength(10)]
    public string TargetMethod { get; set; } = "POST";

    /// <summary>目标数据库表名（TargetType=Database时使用）</summary>
    [MaxLength(200)]
    public string? DbTableName { get; set; }

    /// <summary>子表配置JSON（TargetType=Database时可选），格式：[{"tableName":"InboundOrderDetails","foreignKey":"InboundOrderId","sourceField":"details","mappings":"[...]"}]</summary>
    public string? DbChildConfig { get; set; }

    /// <summary>字段映射（JSON数组），简单映射在界面上配置</summary>
    public string? FieldMappings { get; set; }

    /// <summary>C# 代码处理器（复杂的取值/转换逻辑在代码中写）</summary>
    public string? CodeHandler { get; set; }

    /// <summary>调用前事件：C#代码，在拉取数据前执行，可修改请求参数</summary>
    public string? BeforeExecute { get; set; }

    /// <summary>调用后事件：C#代码，在推送数据后执行，可做后续处理</summary>
    public string? AfterExecute { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 集成平台 - 执行日志
/// </summary>
public class IntegrationLog
{
    [Key]
    public long Id { get; set; }

    public int TaskId { get; set; }

    /// <summary>Pull=拉取，Push=推送，Execute=完整执行</summary>
    [MaxLength(20)]
    public string Direction { get; set; } = string.Empty;

    /// <summary>Success, Fail</summary>
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? RequestUrl { get; set; }

    public string? RequestBody { get; set; }

    public string? ResponseData { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>请求头（JSON），方便排查认证和Header问题</summary>
    public string? RequestHeaders { get; set; }

    /// <summary>日志类型：Pull/Push/Execute/Token</summary>
    [MaxLength(20)]
    public string LogType { get; set; } = string.Empty;

    public long DurationMs { get; set; }

    public DateTime ExecutedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 链式认证配置 — AuthType="Chain" 时使用。
/// 按序执行多个 HTTP 步骤，每步可提取变量，后续步骤可用 {{var}} 引用。
/// 最后将提取的 token/签名等通过 headerName + headerTemplate 注入请求头。
/// </summary>
public class ChainAuthConfig
{
    public List<ChainAuthStep> Steps { get; set; } = new();
    public string? HeaderName { get; set; }
    public string? HeaderTemplate { get; set; }
}

public class ChainAuthStep
{
    public string? Url { get; set; }
    public string Method { get; set; } = "POST";
    public string? Body { get; set; }
    /// <summary>从响应 JSON 中提取值的路径，如 data.accessToken</summary>
    public string? ExtractField { get; set; }
    /// <summary>提取的值保存为变量名，供后续步骤模板替换</summary>
    public string? SaveAs { get; set; }
}

/// <summary>
/// 计划任务 — 按 Cron 表达式定时执行集成任务
/// </summary>
public class ScheduledTask
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>关联的集成任务 ID（0 表示不关联集成任务，直接用 CodeHandler 执行）</summary>
    public int IntegrationTaskId { get; set; }

    /// <summary>C# 自定义代码 — 不依赖集成任务，直接执行。代码中可用 db (AppDbContext) 访问数据库</summary>
    public string? CodeHandler { get; set; }

    /// <summary>执行器类全路径，如 backend.ScheduledTaskHandlers.CleanupLogsHandler。需实现 IScheduledTaskHandler 接口</summary>
    [MaxLength(500)]
    public string? HandlerClass { get; set; }

    /// <summary>执行器参数（JSON），如 {"days":"30","includeDetail":"true"}，执行时传给 HandlerClass</summary>
    [MaxLength(2000)]
    public string? HandlerParameters { get; set; }

    /// <summary>Cron 表达式（5 字段：分 时 日 月 周），如 "0 */2 * * *" 每 2 小时。仅执行一次时可为空</summary>
    [MaxLength(100)]
    public string CronExpression { get; set; } = "";

    /// <summary>仅执行一次的时间（UTC）。不为空时忽略 CronExpression，到达时间后自动执行并禁用</summary>
    public DateTime? RunOnceAt { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>上次执行时间</summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>下次执行时间</summary>
    public DateTime? NextRunAt { get; set; }

    /// <summary>上次执行结果（success / fail）</summary>
    [MaxLength(20)]
    public string? LastRunStatus { get; set; }

    /// <summary>上次执行耗时（毫秒）</summary>
    public long? LastRunDurationMs { get; set; }

    /// <summary>上次执行消息</summary>
    [MaxLength(500)]
    public string? LastRunMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
