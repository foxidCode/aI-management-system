using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public class RegisterRequest
{
    [Required(ErrorMessage = "请输入用户名"), MinLength(3, ErrorMessage = "用户名至少3个字符"), MaxLength(50, ErrorMessage = "用户名最多50个字符")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入密码"), MinLength(6, ErrorMessage = "密码至少6个字符")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入邮箱"), EmailAddress(ErrorMessage = "邮箱格式不正确"), MaxLength(100)]
    public string Email { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required(ErrorMessage = "请输入用户名")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入密码")]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
    public List<int> RoleIds { get; set; } = new();
}
