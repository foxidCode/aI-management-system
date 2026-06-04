using System.ComponentModel.DataAnnotations;

namespace backend.Models;

/// <summary>
/// 材料字典
/// </summary>
public class MaterialDictionary
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Specification { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    [MaxLength(20)]
    public string? Unit { get; set; }

    [MaxLength(200)]
    public string? Remark { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
