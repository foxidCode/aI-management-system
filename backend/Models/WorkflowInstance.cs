using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class WorkflowInstance
{
    [Key]
    public int Id { get; set; }

    public int DefinitionId { get; set; }

    [MaxLength(50)]
    public string DefinitionName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Version { get; set; }

    public int ApplicantId { get; set; }

    /// <summary>用户填写的表单数据 JSON</summary>
    public string? FormData { get; set; }

    /// <summary>Running | Completed | Rejected | Cancelled</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Running";

    /// <summary>当前活跃节点 ID</summary>
    [MaxLength(50)]
    public string? CurrentNodeId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public DateTime? CompletedAt { get; set; }

    // Navigation
    public WorkflowDefinition? Definition { get; set; }
    public User? Applicant { get; set; }
    public ICollection<WorkflowTask> Tasks { get; set; } = new List<WorkflowTask>();
    public ICollection<WorkflowHistory> Histories { get; set; } = new List<WorkflowHistory>();
}
