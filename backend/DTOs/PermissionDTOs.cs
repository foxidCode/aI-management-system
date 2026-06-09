using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

// ========== 角色 DTO ==========
public class CreateRoleRequest
{
    [Required(ErrorMessage = "请输入角色名称"), MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    public List<int> PermissionIds { get; set; } = new();
}

public class UpdateRoleRequest
{
    [Required(ErrorMessage = "请输入角色名称"), MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    public List<int> PermissionIds { get; set; } = new();
}

public class RoleResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<int> PermissionIds { get; set; } = new();
}

public class RoleListResponse
{
    public List<RoleResponse> List { get; set; } = new();
    public int Total { get; set; }
}

// ========== 权限 DTO ==========
public class PermissionResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}

// ========== 菜单 DTO ==========
public class MenuResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string? Icon { get; set; }
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
    public string? PermissionCode { get; set; }
    public string? Component { get; set; }
    public string? OpenType { get; set; } = "self";
    public bool IsVisible { get; set; } = true;
    public bool IsBuiltIn { get; set; } = false;
    public List<MenuResponse> Children { get; set; } = new();
}

// ========== 菜单管理 DTO ==========
public class CreateMenuRequest
{
    [Required(ErrorMessage = "请输入菜单名称"), MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Path { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; }

    public int? ParentId { get; set; }

    public int SortOrder { get; set; }

    [MaxLength(100)]
    public string? PermissionCode { get; set; }

    [MaxLength(50)]
    public string? Component { get; set; }

    [MaxLength(20)]
    public string? OpenType { get; set; } = "self";

    public bool IsVisible { get; set; } = true;
}

public class UpdateMenuRequest
{
    [Required(ErrorMessage = "请输入菜单名称"), MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Path { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; }

    public int? ParentId { get; set; }

    public int SortOrder { get; set; }

    [MaxLength(100)]
    public string? PermissionCode { get; set; }

    [MaxLength(50)]
    public string? Component { get; set; }

    [MaxLength(20)]
    public string? OpenType { get; set; } = "self";

    public bool IsVisible { get; set; } = true;
}

public class BatchUpdateMenusRequest
{
    public List<MenuSortItem> Menus { get; set; } = new();
}

public class MenuSortItem
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
}

// ========== 用户分配角色 DTO ==========
public class AssignUserRolesRequest
{
    [Required]
    public List<int> RoleIds { get; set; } = new();
}

public class UserWithRolesResponse
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<int> RoleIds { get; set; } = new();
    public List<string> RoleNames { get; set; } = new();
}

// ========== 权限树节点 ==========
public class PermissionTreeNode
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Code { get; set; }
    public List<PermissionTreeNode> Children { get; set; } = new();
}
