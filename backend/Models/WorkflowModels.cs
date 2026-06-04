using System.ComponentModel.DataAnnotations;

namespace backend.Models;

/// <summary>
/// 流程定义 — 可发布的审批流程模板，支持版本管理
/// </summary>
public class WorkflowDefinition
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>分类：InboundOrder / AccountRequest 等</summary>
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>版本号，每次发布后自增。同 Name+Category 最高版本的为当前生效版本</summary>
    public int Version { get; set; } = 1;

    /// <summary>状态：draft / published / archived</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "draft";

    /// <summary>节点与连线的完整 JSON（nodes + edges）</summary>
    public string NodeData { get; set; } = "{\"nodes\":[],\"edges\":[]}";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public ICollection<WorkflowInstance> Instances { get; set; } = new List<WorkflowInstance>();
}

/// <summary>
/// 流程实例 — 每次发起审批时创建，快照当时流程定义
/// </summary>
public class WorkflowInstance
{
    [Key]
    public int Id { get; set; }

    /// <summary>关联的流程定义</summary>
    public int DefinitionId { get; set; }
    public WorkflowDefinition? Definition { get; set; }

    /// <summary>发起时的流程版本号（快照，不受后续修改影响）</summary>
    public int DefinitionVersion { get; set; }

    /// <summary>业务模块名：InboundOrder</summary>
    [MaxLength(50)]
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>关联的业务实体 ID（字符串，可跨模块）</summary>
    [MaxLength(50)]
    public string RelatedId { get; set; } = string.Empty;

    /// <summary>当前活跃节点的 JSON 数组，如 ["node_2","node_3"]</summary>
    [MaxLength(500)]
    public string CurrentNodeIds { get; set; } = "[]";

    /// <summary>实例状态：running / approved / rejected / recalled</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "running";

    public int StartedBy { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }

    /// <summary>实例化时的节点数据快照（JSON）</summary>
    public string NodeData { get; set; } = "{\"nodes\":[],\"edges\":[]}";

    public ICollection<WorkflowTask> Tasks { get; set; } = new List<WorkflowTask>();
}

/// <summary>
/// 审批任务 — 每个节点的每个审批人一条记录
/// </summary>
public class WorkflowTask
{
    [Key]
    public int Id { get; set; }

    public int InstanceId { get; set; }
    public WorkflowInstance? Instance { get; set; }

    /// <summary>对应的节点 ID（WorkflowNode.Id）</summary>
    [MaxLength(50)]
    public string NodeId { get; set; } = string.Empty;

    /// <summary>节点名称（冗余，方便查询）</summary>
    [MaxLength(100)]
    public string NodeName { get; set; } = string.Empty;

    /// <summary>任务类型：approval / cc</summary>
    [MaxLength(20)]
    public string TaskType { get; set; } = "approval";

    /// <summary>审批人 / 抄送人 UserId</summary>
    public int AssigneeId { get; set; }

    /// <summary>状态：pending / approved / rejected / transferred</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "pending";

    [MaxLength(500)]
    public string? Comment { get; set; }

    /// <summary>是否已读（用于待办角标）</summary>
    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
}
