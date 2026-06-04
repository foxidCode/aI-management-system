using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

// ========== Token 端点 ==========

public class TokenRequest
{
    [Required]
    public string GrantType { get; set; } = string.Empty; // "authorization_code" | "refresh_token"

    public string? Code { get; set; }
    public string? CodeVerifier { get; set; }
    public string? RedirectUri { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RefreshToken { get; set; }
}

public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public string? IdToken { get; set; }
    public string? RefreshToken { get; set; }
}

// ========== 授权查询参数 ==========

public class AuthorizationRequest
{
    [Required]
    public string ResponseType { get; set; } = "code";

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string RedirectUri { get; set; } = string.Empty;

    public string? Scope { get; set; }
    public string? State { get; set; }
    public string? CodeChallenge { get; set; }
    public string? CodeChallengeMethod { get; set; }
    public string? Nonce { get; set; }
}

// ========== UserInfo ==========

public class UserInfoResponse
{
    public string Sub { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? PreferredUsername { get; set; }
    public string? Email { get; set; }
    public bool EmailVerified { get; set; } = false;
    public List<string> Permissions { get; set; } = new();
    public List<int> RoleIds { get; set; } = new();
}

// ========== Revoke ==========

public class RevokeRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;
    public string? TokenTypeHint { get; set; }
}

// ========== OAuth 客户端管理 ==========

public class CreateOAuthClientRequest
{
    [Required, MaxLength(100)]
    public string ClientId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ClientSecret { get; set; }

    [Required, MaxLength(100)]
    public string ClientName { get; set; } = string.Empty;

    [Required]
    public List<string> RedirectUris { get; set; } = new();

    public List<string> AllowedScopes { get; set; } = new() { "openid" };

    public List<string> AllowedGrantTypes { get; set; } = new() { "authorization_code" };

    public bool IsFirstParty { get; set; } = false;

    public bool RequirePkce { get; set; } = true;
}

public class UpdateOAuthClientRequest
{
    [MaxLength(200)]
    public string? ClientSecret { get; set; }

    [MaxLength(100)]
    public string? ClientName { get; set; }

    public List<string>? RedirectUris { get; set; }
    public List<string>? AllowedScopes { get; set; }
    public List<string>? AllowedGrantTypes { get; set; }
    public bool? IsFirstParty { get; set; }
    public bool? RequirePkce { get; set; }
    public bool? IsActive { get; set; }
}

public class OAuthClientResponse
{
    public int Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public List<string> RedirectUris { get; set; } = new();
    public List<string> AllowedScopes { get; set; } = new();
    public List<string> AllowedGrantTypes { get; set; } = new();
    public bool IsFirstParty { get; set; }
    public bool RequirePkce { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // 仅在创建时返回完整 Secret
    public string? ClientSecretPlain { get; set; }
}
