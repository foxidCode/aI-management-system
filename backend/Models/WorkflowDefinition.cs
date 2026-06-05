using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class WorkflowDefinition
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? GroupId { get; set; }

    [MaxLength(50)]
    public string? FlowCode { get; set; }

    /// <summary>表单类型 1=低代码自定义表单 2=外链表单</summary>
    public int FrmType { get; set; } = 1;

    /// <summary>VForm3 表单设计器 JSON（widgetList + formConfig）</summary>
    public string? FrmValue { get; set; }

    /// <summary>外链表单 URL（FrmType != 1 时使用）</summary>
    [MaxLength(500)]
    public string? FrmUrl { get; set; }

    /// <summary>去重类型 0=不去重 1=前去重 2=后去重</summary>
    public int DistinctType { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    [MaxLength(20)]
    public string? Version { get; set; } = "1.0";

    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary>流程节点 JSON 数组（formatCommitData 扁平化输出）</summary>
    public string? Nodes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
