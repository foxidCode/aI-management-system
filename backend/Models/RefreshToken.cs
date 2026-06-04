using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class RefreshToken
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// SHA256 哈希后的刷新令牌（唯一）
    /// </summary>
    [Required, MaxLength(256)]
    public string Token { get; set; } = string.Empty;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int ClientId { get; set; }
    public OAuthClient Client { get; set; } = null!;

    [MaxLength(500)]
    public string Scopes { get; set; } = "openid";

    public bool IsRevoked { get; set; } = false;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? RevokedAt { get; set; }
}
