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

    /// <summary>
    /// 打开方式: self=当前页, blank=新标签, iframe=内嵌
    /// </summary>
    [MaxLength(20)]
    public string OpenType { get; set; } = "self";

    /// <summary>
    /// 是否在侧边栏显示
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// 是否为内置菜单（内置菜单不可删除，仅可调整顺序和层级）
    /// </summary>
    public bool IsBuiltIn { get; set; } = false;
}
