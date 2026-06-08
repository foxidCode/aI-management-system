using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

// ===== WorkflowDefinition =====

public class CreateWorkflowDefinitionRequest
{
    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? GroupId { get; set; }

    [MaxLength(50)]
    public string? FlowCode { get; set; }

    public int FrmType { get; set; } = 1;
    public string? FrmValue { get; set; }

    [MaxLength(500)]
    public string? FrmUrl { get; set; }

    public int DistinctType { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    [MaxLength(20)]
    public string? Version { get; set; } = "1.0";

    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary>流程节点 JSON 数组</summary>
    public string? Nodes { get; set; }
}

public class UpdateWorkflowDefinitionRequest
{
    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? GroupId { get; set; }

    public int FrmType { get; set; } = 1;
    public string? FrmValue { get; set; }

    [MaxLength(500)]
    public string? FrmUrl { get; set; }

    public int DistinctType { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Remark { get; set; }

    public string? Nodes { get; set; }
}

public class WorkflowDefinitionResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? GroupId { get; set; }
    public string? FlowCode { get; set; }
    public int FrmType { get; set; }
    public string? FrmValue { get; set; }
    public string? FrmUrl { get; set; }
    public int DistinctType { get; set; }
    public bool IsActive { get; set; }
    public string? Version { get; set; }
    public string? Remark { get; set; }
    public string? Nodes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WorkflowDefinitionListResponse
{
    public List<WorkflowDefinitionResponse> List { get; set; } = new();
    public int Total { get; set; }
}

// ===== WorkflowInstance =====

public class SubmitWorkflowInstanceRequest
{
    [Required]
    public int DefinitionId { get; set; }

    /// <summary>用户填写的表单数据 JSON</summary>
    public string? FormData { get; set; }
}

public class WorkflowInstanceResponse
{
    public int Id { get; set; }
    public int DefinitionId { get; set; }
    public string DefinitionName { get; set; } = string.Empty;
    public string? Version { get; set; }
    public int ApplicantId { get; set; }
    public string? ApplicantName { get; set; }
    public string? FormData { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CurrentNodeId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<WorkflowTaskResponse> Tasks { get; set; } = new();
    public List<WorkflowHistoryResponse> Histories { get; set; } = new();
}

public class WorkflowInstanceListResponse
{
    public List<WorkflowInstanceResponse> List { get; set; } = new();
    public int Total { get; set; }
}

// ===== WorkflowTask =====

public class WorkflowTaskResponse
{
    public int Id { get; set; }
    public int InstanceId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string? NodeName { get; set; }
    public int NodeType { get; set; }
    public int? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public int? AssigneeType { get; set; }
    public string? ActionType { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FormData { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? ParentTaskId { get; set; }
    /// <summary>关联的流程实例摘要</summary>
    public TaskInstanceBrief? Instance { get; set; }
}

/// <summary>任务列表中使用的流程实例摘要</summary>
public class TaskInstanceBrief
{
    public int Id { get; set; }
    public int DefinitionId { get; set; }
    public string? DefinitionName { get; set; }
    public string? ApplicantName { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class WorkflowTaskListResponse
{
    public List<WorkflowTaskResponse> List { get; set; } = new();
    public int Total { get; set; }
}

public class ApproveTaskRequest
{
    [MaxLength(1000)]
    public string? Comment { get; set; }

    /// <summary>审批时可修改的表单数据 JSON</summary>
    public string? FormData { get; set; }
}

public class RejectTaskRequest
{
    [MaxLength(1000)]
    public string? Comment { get; set; }
}

public class TransferTaskRequest
{
    [Required]
    public int ToUserId { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }
}

// ===== WorkflowHistory =====

public class WorkflowHistoryResponse
{
    public int Id { get; set; }
    public int InstanceId { get; set; }
    public int? TaskId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public int ActorId { get; set; }
    public string? ActorName { get; set; }
    public string? Comment { get; set; }
    public string? FormDataSnapshot { get; set; }
    public string? FromNodeId { get; set; }
    public string? ToNodeId { get; set; }
    public DateTime CreatedAt { get; set; }
}
