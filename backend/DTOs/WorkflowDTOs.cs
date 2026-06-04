using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

// ========== WorkflowDefinition DTOs ==========

public class CreateWorkflowRequest
{
    [Required(ErrorMessage = "流程名称不能为空"), MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    public string NodeData { get; set; } = "{\"nodes\":[],\"edges\":[]}";
}

public class UpdateWorkflowRequest
{
    [Required(ErrorMessage = "流程名称不能为空"), MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    public string NodeData { get; set; } = "{\"nodes\":[],\"edges\":[]}";
}

public class WorkflowDefinitionResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public int Version { get; set; }
    public string Status { get; set; } = "";
    public string NodeData { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    /// <summary>运行中的实例数</summary>
    public int RunningInstanceCount { get; set; }
}

// ========== WorkflowInstance DTOs ==========

public class StartWorkflowRequest
{
    [Required(ErrorMessage = "请选择流程定义")]
    public int DefinitionId { get; set; }

    [Required(ErrorMessage = "模块名不能为空"), MaxLength(50)]
    public string ModuleName { get; set; } = string.Empty;

    [Required(ErrorMessage = "关联实体ID不能为空"), MaxLength(50)]
    public string RelatedId { get; set; } = string.Empty;
}

public class ApprovalRequest
{
    [Required(ErrorMessage = "节点ID不能为空")]
    public string NodeId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Comment { get; set; }
}

public class WorkflowInstanceResponse
{
    public int Id { get; set; }
    public int DefinitionId { get; set; }
    public string DefinitionName { get; set; } = "";
    public int DefinitionVersion { get; set; }
    public string ModuleName { get; set; } = "";
    public string RelatedId { get; set; } = "";
    public string CurrentNodeIds { get; set; } = "";
    public string Status { get; set; } = "";
    public string StartedByName { get; set; } = "";
    public string StartedAt { get; set; } = "";
    public string? CompletedAt { get; set; }
    public string NodeData { get; set; } = "";
    public List<WorkflowTaskResponse> Tasks { get; set; } = new();
}

// ========== WorkflowTask DTOs ==========

public class WorkflowTaskResponse
{
    public int Id { get; set; }
    public int InstanceId { get; set; }
    public string NodeId { get; set; } = "";
    public string NodeName { get; set; } = "";
    public string TaskType { get; set; } = "";
    public int AssigneeId { get; set; }
    public string AssigneeName { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Comment { get; set; }
    public bool IsRead { get; set; }
    public string CreatedAt { get; set; } = "";
    public string? CompletedAt { get; set; }
    // 来自实例的冗余字段，方便待办列表展示
    public string InstanceModuleName { get; set; } = "";
    public string InstanceRelatedId { get; set; } = "";
    public string DefinitionName { get; set; } = "";
}

// ========== 统计 DTOs ==========

public class WorkflowStatsResponse
{
    public int TotalDefinitions { get; set; }
    public int PublishedDefinitions { get; set; }
    public int RunningInstances { get; set; }
    public int CompletedToday { get; set; }
    public int MyPendingTasks { get; set; }
}
