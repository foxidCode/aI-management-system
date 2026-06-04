using System.ComponentModel.DataAnnotations;

namespace backend.Models;

/// <summary>
/// 数据库连接配置（密码加密存储）
/// </summary>
public class DbConnectionConfig
{
    [Key]
    public int Id { get; set; }

    /// <summary>连接名称（用户自定义）</summary>
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>数据库类型：MySQL, SqlServer, SQLite, Oracle, PostgreSQL</summary>
    [Required, MaxLength(50)]
    public string DbType { get; set; } = string.Empty;

    /// <summary>主机地址</summary>
    [MaxLength(200)]
    public string? Host { get; set; }

    /// <summary>端口</summary>
    public int Port { get; set; }

    /// <summary>数据库名/服务名</summary>
    [MaxLength(200)]
    public string? DatabaseName { get; set; }

    /// <summary>用户名</summary>
    [MaxLength(100)]
    public string? Username { get; set; }

    /// <summary>加密后的密码</summary>
    [MaxLength(500)]
    public string? EncryptedPassword { get; set; }

    /// <summary>额外连接参数</summary>
    [MaxLength(500)]
    public string? ExtraParams { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
