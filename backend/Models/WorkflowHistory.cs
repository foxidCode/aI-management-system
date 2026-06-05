using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class WorkflowHistory
{
    [Key]
    public int Id { get; set; }

    public int InstanceId { get; set; }

    public int? TaskId { get; set; }

    /// <summary>submitted | approved | rejected | transferred | cancelled | copied</summary>
    [Required, MaxLength(20)]
    public string ActionType { get; set; } = string.Empty;

    public int ActorId { get; set; }

    [MaxLength(50)]
    public string? ActorName { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    /// <summary>操作时表单快照 JSON</summary>
    public string? FormDataSnapshot { get; set; }

    [MaxLength(50)]
    public string? FromNodeId { get; set; }

    [MaxLength(50)]
    public string? ToNodeId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation
    public WorkflowInstance? Instance { get; set; }
    public WorkflowTask? Task { get; set; }
    public User? Actor { get; set; }
}
