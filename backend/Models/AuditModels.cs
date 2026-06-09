using System.ComponentModel.DataAnnotations;

namespace backend.Models;

/// <summary>
/// 操作日志（审计追踪）
/// </summary>
public class OperationLog
{
    [Key]
    public int Id { get; set; }

    /// <summary>操作者用户 ID</summary>
    public int UserId { get; set; }

    /// <summary>操作者用户名（冗余，便于查询）</summary>
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    /// <summary>操作者 IP 地址</summary>
    [MaxLength(50)]
    public string? IpAddress { get; set; }

    /// <summary>操作类型（如 role:create, user:delete, permission:grant, export 等）</summary>
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    /// <summary>目标资源类型（如 User, Role, Permission, InboundOrder 等）</summary>
    [MaxLength(50)]
    public string? TargetType { get; set; }

    /// <summary>目标资源 ID</summary>
    [MaxLength(50)]
    public string? TargetId { get; set; }

    /// <summary>目标资源名称（冗余，便于查看）</summary>
    [MaxLength(200)]
    public string? TargetName { get; set; }

    /// <summary>操作详情（JSON 或描述文本）</summary>
    [MaxLength(2000)]
    public string? Detail { get; set; }

    /// <summary>是否为敏感操作</summary>
    public bool IsSensitive { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // 导航属性
    public User User { get; set; } = null!;
}

/// <summary>
/// 权限变更日志（记录用户权限的授予、修改、撤销历史）
/// </summary>
public class PermissionChangeLog
{
    [Key]
    public int Id { get; set; }

    /// <summary>目标用户 ID（被变更权限的用户）</summary>
    public int? TargetUserId { get; set; }

    /// <summary>目标用户名</summary>
    [MaxLength(50)]
    public string? TargetUsername { get; set; }

    /// <summary>变更类型：Grant, Revoke, Modify</summary>
    [MaxLength(20)]
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>权限编码</summary>
    [MaxLength(100)]
    public string PermissionCode { get; set; } = string.Empty;

    /// <summary>权限名称</summary>
    [MaxLength(100)]
    public string? PermissionName { get; set; }

    /// <summary>操作者 ID</summary>
    public int OperatorId { get; set; }

    /// <summary>操作者用户名</summary>
    [MaxLength(50)]
    public string OperatorName { get; set; } = string.Empty;

    /// <summary>操作者 IP</summary>
    [MaxLength(50)]
    public string? OperatorIp { get; set; }

    /// <summary>变更详情</summary>
    [MaxLength(500)]
    public string? Detail { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // 导航属性
    public User? TargetUser { get; set; }
    public User Operator { get; set; } = null!;
}
