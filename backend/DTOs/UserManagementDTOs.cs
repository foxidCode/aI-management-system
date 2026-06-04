using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public class CreateUserRequest
{
    [Required(ErrorMessage = "请输入用户名"), MinLength(3, ErrorMessage = "用户名至少3个字符"), MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "请输入邮箱"), EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? Gender { get; set; }

    [MaxLength(30)]
    public string? IdCard { get; set; }

    [MaxLength(50)]
    public string? EmployeeId { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }
}

public class UpdateUserRequest
{
    [Required(ErrorMessage = "请输入邮箱"), EmailAddress(ErrorMessage = "邮箱格式不正确")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? Gender { get; set; }

    [MaxLength(30)]
    public string? IdCard { get; set; }

    [MaxLength(50)]
    public string? EmployeeId { get; set; }

    [MaxLength(500)]
    public string? Remark { get; set; }
}

public class UserListResponse
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Gender { get; set; }
    public string? IdCard { get; set; }
    public string? EmployeeId { get; set; }
    public string? Remark { get; set; }
    public bool IsFrozen { get; set; }
    public bool IsOnline { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
    public List<int> RoleIds { get; set; } = new();
    public List<string> RoleNames { get; set; } = new();
}
