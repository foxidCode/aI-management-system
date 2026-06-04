using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public class CreateSsoTokenRequest
{
    [Required(ErrorMessage = "请选择用户")]
    public int UserId { get; set; }

    [Range(1, 720, ErrorMessage = "过期时间需在1~720小时之间")]
    public int ExpireHours { get; set; } = 24;
}

public class CreateAuthCodeRequest
{
    [Required(ErrorMessage = "请选择用户")]
    public int UserId { get; set; }

    [Range(1, 1440, ErrorMessage = "过期时间需在1~1440分钟之间")]
    public int ExpireMinutes { get; set; } = 30;
}

public class SsoLoginRequest
{
    [Required(ErrorMessage = "Token不能为空")]
    public string Token { get; set; } = string.Empty;
}

public class AuthCodeLoginRequest
{
    [Required(ErrorMessage = "授权码不能为空")]
    [StringLength(8, MinimumLength = 8, ErrorMessage = "授权码为8位字符")]
    public string Code { get; set; } = string.Empty;
}

public class SsoTokenResponse
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string? AuthCode { get; set; }
    public string Type { get; set; } = "link";
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string LoginLink { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByAdmin { get; set; } = string.Empty;
}
