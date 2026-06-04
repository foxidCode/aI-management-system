using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class SsoToken
{
    [Key]
    public int Id { get; set; }

    [MaxLength(128)]
    public string? Token { get; set; }

    [MaxLength(16)]
    public string? AuthCode { get; set; }

    [MaxLength(10)]
    public string Type { get; set; } = "link";

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int CreatedBy { get; set; }
    public User CreatedByAdmin { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UsedAt { get; set; }
}
