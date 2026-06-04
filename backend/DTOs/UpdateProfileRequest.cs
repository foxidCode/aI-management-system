using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public class UpdateProfileRequest
{
    [Required(ErrorMessage = "请输入邮箱"), EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;

    [MinLength(6, ErrorMessage = "新密码至少6个字符")]
    public string? NewPassword { get; set; }

    [Required(ErrorMessage = "请输入当前密码以确认修改")]
    public string CurrentPassword { get; set; } = string.Empty;
}

public class UserProfileResponse
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

public class HomeConfigRequest
{
    public string Config { get; set; } = string.Empty;
}
