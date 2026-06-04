using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class OAuthClient
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string ClientId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ClientSecret { get; set; }

    [Required, MaxLength(100)]
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// JSON 数组存储重定向 URI，如 ["http://localhost:5173/callback"]
    /// </summary>
    [Required]
    public string RedirectUris { get; set; } = "[]";

    /// <summary>
    /// 空格分隔的允许作用域
    /// </summary>
    [MaxLength(500)]
    public string AllowedScopes { get; set; } = "openid";

    /// <summary>
    /// 空格分隔的允许授权类型
    /// </summary>
    [MaxLength(200)]
    public string AllowedGrantTypes { get; set; } = "authorization_code";

    public bool IsFirstParty { get; set; } = false;

    public bool RequirePkce { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // Navigation
    public ICollection<AuthorizationCode> AuthorizationCodes { get; set; } = new List<AuthorizationCode>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
