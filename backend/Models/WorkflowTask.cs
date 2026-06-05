using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class WorkflowTask
{
    [Key]
    public int Id { get; set; }

    public int InstanceId { get; set; }

    [Required, MaxLength(50)]
    public string NodeId { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? NodeName { get; set; }

    /// <summary>节点类型 4=审批 5=并行网关 6=抄送 7=并行审批</summary>
    public int NodeType { get; set; }

    public int? AssigneeId { get; set; }

    /// <summary>审批人解析方式 1=成员 2=角色 3=主管 5=发起人 6=层级 7=层层 8=自选</summary>
    public int? AssigneeType { get; set; }

    /// <summary>approve | reject | transfer | copy | submit</summary>
    [MaxLength(20)]
    public string? ActionType { get; set; }

    /// <summary>Pending | Completed | Cancelled</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Pending";

    /// <summary>任务创建时表单快照 JSON</summary>
    public string? FormData { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? CompletedAt { get; set; }

    /// <summary>父任务 ID（并行审批子任务/网关分支时用）</summary>
    public int? ParentTaskId { get; set; }

    // Navigation
    public WorkflowInstance? Instance { get; set; }
    public User? Assignee { get; set; }
}
