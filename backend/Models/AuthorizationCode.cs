using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class AuthorizationCode
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// SHA256 哈希后的授权码（唯一）
    /// </summary>
    [Required, MaxLength(256)]
    public string Code { get; set; } = string.Empty;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int ClientId { get; set; }
    public OAuthClient Client { get; set; } = null!;

    [Required, MaxLength(500)]
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// PKCE S256 Code Challenge
    /// </summary>
    [MaxLength(128)]
    public string? CodeChallenge { get; set; }

    [MaxLength(10)]
    public string? CodeChallengeMethod { get; set; }

    /// <summary>
    /// 空格分隔的授权作用域
    /// </summary>
    [MaxLength(500)]
    public string Scopes { get; set; } = "openid";

    /// <summary>
    /// OIDC Nonce
    /// </summary>
    [MaxLength(256)]
    public string? Nonce { get; set; }

    public bool IsUsed { get; set; } = false;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UsedAt { get; set; }
}
