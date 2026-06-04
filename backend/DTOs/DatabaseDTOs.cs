using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

// ========== 连接配置 DTO ==========

public class DbConnectionRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string DbType { get; set; } = string.Empty;

    [MaxLength(200)] public string? Host { get; set; }
    public int? Port { get; set; }
    [MaxLength(200)] public string? DatabaseName { get; set; }
    [MaxLength(100)] public string? Username { get; set; }
    [MaxLength(200)] public string? Password { get; set; }
    [MaxLength(500)] public string? ExtraParams { get; set; }
}

public class DbConnectionResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DbType { get; set; } = string.Empty;
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? DatabaseName { get; set; }
    public string? Username { get; set; }
    public string? ExtraParams { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ========== 表信息 DTO ==========

public class TableInfo
{
    public string TableName { get; set; } = string.Empty;
    public string? TableComment { get; set; }
}

public class ColumnInfo
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public string? DefaultValue { get; set; }
    public string? Comment { get; set; }
    public int? MaxLength { get; set; }
}

// ========== SQL 执行 DTO ==========

public class ExecuteSqlRequest
{
    [Required]
    public string Sql { get; set; } = string.Empty;
}

public class ExecuteSqlResponse
{
    public List<string> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int RowCount { get; set; }
    public long ElapsedMs { get; set; }
}
