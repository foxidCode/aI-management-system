using System.ComponentModel.DataAnnotations;

namespace backend.Models;

/// <summary>
/// 直接用户权限（绕过角色，直接给用户分配权限）
/// </summary>
public class UserPermission
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;

    /// <summary>授予者 ID</summary>
    public int GrantedBy { get; set; }

    public DateTime GrantedAt { get; set; } = DateTime.Now;
}
