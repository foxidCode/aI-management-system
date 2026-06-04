using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class User
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? Gender { get; set; }

    [MaxLength(30)]
    public string? IdCard { get; set; }

    [MaxLength(50)]
    public string? EmployeeId { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }

    /// <summary>主页卡片配置（JSON）</summary>
    [MaxLength(4000)]
    public string? HomeConfig { get; set; }

    public bool IsFrozen { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
