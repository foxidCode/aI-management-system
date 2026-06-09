using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

// ========== 权限管理 DTO ==========

/// <summary>新增权限请求</summary>
public class CreatePermissionRequest
{
    [Required(ErrorMessage = "请输入权限名称"), MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入权限编码"), MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }
}

/// <summary>编辑权限请求</summary>
public class UpdatePermissionRequest
{
    [Required(ErrorMessage = "请输入权限名称"), MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入权限编码"), MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }
}

/// <summary>分页权限响应</summary>
public class PermissionPaginatedResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>关联的角色数量</summary>
    public int RoleCount { get; set; }

    /// <summary>直接授权的用户数量</summary>
    public int UserCount { get; set; }
}

// ========== 直接用户授权 DTO ==========

/// <summary>授予用户直接权限请求</summary>
public class GrantUserPermissionRequest
{
    [Required]
    public int UserId { get; set; }

    [Required]
    public List<int> PermissionIds { get; set; } = new();
}

/// <summary>撤销用户直接权限请求</summary>
public class RevokeUserPermissionRequest
{
    [Required]
    public int UserId { get; set; }

    [Required]
    public List<int> PermissionIds { get; set; } = new();
}

/// <summary>用户权限汇总（角色权限 + 直接权限）</summary>
public class UserPermissionSummaryResponse
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;

    /// <summary>通过角色获得的权限 ID 列表</summary>
    public List<int> RolePermissionIds { get; set; } = new();

    /// <summary>直接授予的权限 ID 列表</summary>
    public List<int> DirectPermissionIds { get; set; } = new();

    /// <summary>所有权限 ID（合并去重）</summary>
    public List<int> AllPermissionIds { get; set; } = new();
}
