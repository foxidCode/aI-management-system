using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public class SendResetCodeRequest
{
    [Required(ErrorMessage = "请输入邮箱"), EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "请输入邮箱"), EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入验证码")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入新密码"), MinLength(6, ErrorMessage = "密码至少6个字符")]
    public string NewPassword { get; set; } = string.Empty;
}
