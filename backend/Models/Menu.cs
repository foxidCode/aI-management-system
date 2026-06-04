using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class Menu
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Path { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; }

    public int? ParentId { get; set; }

    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// 关联的权限编码，为空表示无需权限即可访问
    /// </summary>
    [MaxLength(100)]
    public string? PermissionCode { get; set; }

    /// <summary>
    /// 菜单类型: menu=菜单项, button=按钮
    /// </summary>
    [MaxLength(20)]
    public string MenuType { get; set; } = "menu";

    [MaxLength(50)]
    public string? Component { get; set; }
}
