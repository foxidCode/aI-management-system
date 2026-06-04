using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using backend.Data;
using backend.DTOs;
using backend.Models;

namespace backend.Services;

public class DatabaseService
{
    private readonly AppDbContext _db;

    // AES 加密密钥（生产环境应从配置读取）
    private static readonly byte[] AesKey = SHA256.HashData(Encoding.UTF8.GetBytes("DbManagement@2026SecretKey!"));
    private static readonly byte[] AesIv = MD5.HashData(Encoding.UTF8.GetBytes("DbManagementIV"));

    // ========== 本系统 SQLite 数据库的表和字段说明 ==========

    private static readonly Dictionary<string, string> SqliteTableComments = new()
    {
        ["Users"] = "用户表：存储系统所有用户账号信息",
        ["Roles"] = "角色表：定义系统角色（超级管理员、普通用户、只读用户等）",
        ["Permissions"] = "权限表：定义系统所有权限项（如 user:view、role:manage 等）",
        ["RolePermissions"] = "角色权限关联表：角色与权限的多对多关系",
        ["UserRoles"] = "用户角色关联表：用户与角色的多对多关系",
        ["Menus"] = "菜单表：系统侧边栏菜单树，支持父子层级和权限控制",
        ["PasswordResetTokens"] = "密码重置令牌表：用于忘记密码流程的临时令牌",
        ["MaterialDictionaries"] = "材料字典表：存储材料编码、名称、规格型号等基础数据",
        ["InboundOrders"] = "入库单主表：入库单的基本信息（库房、供应商、金额汇总等）",
        ["InboundOrderDetails"] = "入库单明细表：入库单的材料明细行（材料编码、数量、单价、金额）",
        ["InboundOrderAttachments"] = "入库单附件表：入库单关联的附件文件（已迁移到统一附件表）",
        ["SsoTokens"] = "SSO令牌表：单点登录（SSO）的一次性令牌和授权码",
        ["OAuthClients"] = "OAuth客户端表：OAuth 2.0 / OIDC 已注册的客户端应用",
        ["AuthorizationCodes"] = "授权码表：OAuth 2.0 授权码流程中的临时授权码",
        ["RefreshTokens"] = "刷新令牌表：OAuth 2.0 的 Refresh Token，用于刷新 Access Token",
        ["Attachments"] = "统一附件表：所有模块的附件统一存储（模块名+关联ID+文件信息）",
        ["DbConnectionConfigs"] = "数据库连接配置表：外部数据库连接信息（密码加密存储）",
    };

    private static readonly Dictionary<string, Dictionary<string, string>> SqliteColumnComments = new()
    {
        ["Users"] = new()
        {
            ["Id"] = "用户ID（主键，自增）",
            ["Username"] = "用户名（唯一，用于登录）",
            ["PasswordHash"] = "密码哈希值（BCrypt加密）",
            ["Email"] = "邮箱地址（唯一）",
            ["Gender"] = "性别",
            ["IdCard"] = "身份证号",
            ["EmployeeId"] = "工号",
            ["Remark"] = "备注信息",
            ["IsFrozen"] = "是否冻结（0=正常，1=已冻结）",
            ["HomeConfig"] = "主页卡片配置（JSON格式）",
            ["CreatedAt"] = "创建时间",
            ["UpdatedAt"] = "最后更新时间",
        },
        ["Roles"] = new()
        {
            ["Id"] = "角色ID（主键，自增）",
            ["Name"] = "角色名称（唯一，如：超级管理员、普通用户）",
            ["Description"] = "角色描述",
            ["CreatedAt"] = "创建时间",
        },
        ["Permissions"] = new()
        {
            ["Id"] = "权限ID（主键，自增）",
            ["Name"] = "权限名称（中文，如：用户查看）",
            ["Code"] = "权限编码（唯一，如：user:view）",
            ["Description"] = "权限描述",
        },
        ["RolePermissions"] = new()
        {
            ["RoleId"] = "角色ID（复合主键）",
            ["PermissionId"] = "权限ID（复合主键）",
        },
        ["UserRoles"] = new()
        {
            ["UserId"] = "用户ID（复合主键）",
            ["RoleId"] = "角色ID（复合主键）",
        },
        ["Menus"] = new()
        {
            ["Id"] = "菜单ID（主键，自增）",
            ["Name"] = "菜单名称",
            ["Path"] = "前端路由路径（如 /dashboard/users）",
            ["Icon"] = "图标名称（Element Plus 图标组件名）",
            ["ParentId"] = "父菜单ID（null=顶层菜单）",
            ["SortOrder"] = "排序序号（越小越靠前）",
            ["PermissionCode"] = "关联的权限编码（为空表示无需权限）",
            ["MenuType"] = "菜单类型（menu=菜单项，button=按钮）",
            ["Component"] = "对应的Vue组件名",
        },
        ["PasswordResetTokens"] = new()
        {
            ["Id"] = "令牌ID（主键，自增）",
            ["Email"] = "用户邮箱",
            ["Token"] = "重置令牌（随机生成的唯一字符串）",
            ["ExpiresAt"] = "过期时间",
            ["IsUsed"] = "是否已使用（0=未使用，1=已使用）",
        },
        ["MaterialDictionaries"] = new()
        {
            ["Id"] = "材料ID（主键，自增）",
            ["Code"] = "材料编码（唯一）",
            ["Name"] = "材料名称",
            ["Specification"] = "规格",
            ["Model"] = "型号",
            ["Unit"] = "单位（如：吨、千克、米）",
            ["Remark"] = "备注",
            ["CreatedAt"] = "创建时间",
            ["UpdatedAt"] = "最后更新时间",
        },
        ["InboundOrders"] = new()
        {
            ["Id"] = "入库单ID（主键，自增）",
            ["OrderCode"] = "单据编码（自动生成，如 RK-20260602-0001）",
            ["WarehouseName"] = "库房名称",
            ["Supplier"] = "供应商名称",
            ["Contract"] = "合同编号",
            ["TotalTaxIncludedAmount"] = "含税金额合计",
            ["TotalCostAmount"] = "计成本金额合计",
            ["TotalTaxAmount"] = "税额合计",
            ["TaxRate"] = "税率（如 13 表示 13%）",
            ["Remark"] = "备注",
            ["CreatedBy"] = "创建人用户ID",
            ["CreatedAt"] = "创建时间",
            ["UpdatedAt"] = "最后更新时间",
            ["IsDeleted"] = "是否软删除（0=正常，1=已删除）",
        },
        ["InboundOrderDetails"] = new()
        {
            ["Id"] = "明细ID（主键，自增）",
            ["InboundOrderId"] = "所属入库单ID",
            ["MaterialCode"] = "材料编码",
            ["MaterialName"] = "材料名称",
            ["Specification"] = "规格",
            ["Model"] = "型号",
            ["Unit"] = "单位",
            ["Quantity"] = "入库数量",
            ["UnitPrice"] = "含税单价",
            ["TaxIncludedAmount"] = "含税金额（数量×单价）",
            ["TaxAmount"] = "税额",
            ["CostAmount"] = "计成本金额（含税金额-税额）",
            ["TaxRate"] = "税率",
            ["Remark"] = "备注",
        },
        ["InboundOrderAttachments"] = new()
        {
            ["Id"] = "附件ID（主键）",
            ["InboundOrderId"] = "所属入库单ID",
            ["FileName"] = "原始文件名",
            ["ObjectKey"] = "MinIO对象键（存储路径）",
            ["FileSize"] = "文件大小（字节）",
            ["ContentType"] = "MIME类型",
            ["CreatedAt"] = "上传时间",
        },
        ["SsoTokens"] = new()
        {
            ["Id"] = "令牌ID（主键，自增）",
            ["Token"] = "SSO Token（唯一，用于一键登录）",
            ["UserId"] = "目标用户ID（此Token代表哪个用户登录）",
            ["CreatedBy"] = "创建人用户ID（哪个管理员生成的）",
            ["ExpiresAt"] = "过期时间",
            ["IsUsed"] = "是否已使用（0=未使用，1=已使用）",
            ["CreatedAt"] = "创建时间",
            ["UsedAt"] = "使用时间",
            ["AuthCode"] = "授权码（OAuth 授权码流程使用）",
            ["Type"] = "令牌类型（link=一键链接，code=授权码）",
        },
        ["OAuthClients"] = new()
        {
            ["Id"] = "客户端ID（主键，自增）",
            ["ClientId"] = "客户端标识符（唯一，如 vue-spa）",
            ["ClientSecret"] = "客户端密钥（机密客户端使用）",
            ["ClientName"] = "客户端名称",
            ["RedirectUris"] = "回调地址列表（JSON数组）",
            ["AllowedScopes"] = "允许的作用域（空格分隔）",
            ["AllowedGrantTypes"] = "允许的授权类型（空格分隔）",
            ["IsFirstParty"] = "是否第一方应用（0=第三方，1=本系统）",
            ["RequirePkce"] = "是否要求PKCE（0=否，1=是）",
            ["IsActive"] = "是否启用（0=禁用，1=启用）",
            ["CreatedAt"] = "创建时间",
            ["UpdatedAt"] = "最后更新时间",
        },
        ["AuthorizationCodes"] = new()
        {
            ["Id"] = "授权码ID（主键，自增）",
            ["Code"] = "授权码（唯一，临时使用）",
            ["UserId"] = "授权用户ID",
            ["ClientId"] = "客户端ID",
            ["RedirectUri"] = "回调地址",
            ["CodeChallenge"] = "PKCE Code Challenge",
            ["CodeChallengeMethod"] = "PKCE 方法（S256 或 plain）",
            ["Scopes"] = "授权的作用域",
            ["Nonce"] = "OIDC Nonce值",
            ["IsUsed"] = "是否已使用",
            ["ExpiresAt"] = "过期时间",
            ["CreatedAt"] = "创建时间",
            ["UsedAt"] = "使用时间",
        },
        ["RefreshTokens"] = new()
        {
            ["Id"] = "令牌ID（主键，自增）",
            ["Token"] = "刷新令牌（唯一）",
            ["UserId"] = "所属用户ID",
            ["ClientId"] = "所属客户端ID",
            ["Scopes"] = "授权的作用域",
            ["IsRevoked"] = "是否已撤销",
            ["ExpiresAt"] = "过期时间",
            ["CreatedAt"] = "创建时间",
            ["RevokedAt"] = "撤销时间",
        },
        ["Attachments"] = new()
        {
            ["Id"] = "附件ID（主键，自增）",
            ["ModuleName"] = "模块名称（如 InboundOrder）",
            ["RelatedId"] = "关联的业务记录ID",
            ["RelatedName"] = "关联记录的可读名称（如入库单编码）",
            ["FileName"] = "原始文件名",
            ["ObjectKey"] = "MinIO对象键（存储路径）",
            ["FileSize"] = "文件大小（字节）",
            ["ContentType"] = "MIME类型",
            ["UploadedBy"] = "上传人用户ID",
            ["CreatedAt"] = "上传时间",
        },
        ["DbConnectionConfigs"] = new()
        {
            ["Id"] = "配置ID（主键，自增）",
            ["Name"] = "连接名称（用户自定义）",
            ["DbType"] = "数据库类型（MySQL/SqlServer/SQLite/Oracle/PostgreSQL）",
            ["Host"] = "主机地址",
            ["Port"] = "端口号",
            ["DatabaseName"] = "数据库名/服务名",
            ["Username"] = "用户名",
            ["EncryptedPassword"] = "加密后的密码（AES加密）",
            ["ExtraParams"] = "额外连接参数",
            ["CreatedAt"] = "创建时间",
        },
    };

    public DatabaseService(AppDbContext db) => _db = db;

    // ========== 连接配置 CRUD ==========

    public async Task<List<DbConnectionResponse>> GetConnectionsAsync()
    {
        return await _db.DbConnectionConfigs
            .OrderBy(c => c.Id)
            .Select(c => new DbConnectionResponse
            {
                Id = c.Id,
                Name = c.Name,
                DbType = c.DbType,
                Host = c.Host,
                Port = c.Port,
                DatabaseName = c.DatabaseName,
                Username = c.Username,
                ExtraParams = c.ExtraParams,
                CreatedAt = c.CreatedAt,
            })
            .ToListAsync();
    }

    public async Task<DbConnectionConfig?> GetConnectionByIdAsync(int id)
    {
        return await _db.DbConnectionConfigs.FindAsync(id);
    }

    public async Task<DbConnectionResponse> CreateConnectionAsync(DbConnectionRequest req)
    {
        var config = new DbConnectionConfig
        {
            Name = req.Name,
            DbType = req.DbType,
            Host = req.Host,
            Port = req.Port ?? 0,
            DatabaseName = req.DatabaseName,
            Username = req.Username,
            EncryptedPassword = !string.IsNullOrEmpty(req.Password) ? Encrypt(req.Password) : null,
            ExtraParams = req.ExtraParams,
            CreatedAt = DateTime.Now,
        };
        _db.DbConnectionConfigs.Add(config);
        await _db.SaveChangesAsync();
        return Map(config);
    }

    public async Task<DbConnectionResponse?> UpdateConnectionAsync(int id, DbConnectionRequest req)
    {
        var config = await _db.DbConnectionConfigs.FindAsync(id);
        if (config == null) return null;

        config.Name = req.Name;
        config.DbType = req.DbType;
        config.Host = req.Host;
        config.Port = req.Port ?? 0;
        config.DatabaseName = req.DatabaseName;
        config.Username = req.Username;
        if (!string.IsNullOrEmpty(req.Password))
            config.EncryptedPassword = Encrypt(req.Password);
        config.ExtraParams = req.ExtraParams;
        await _db.SaveChangesAsync();
        return Map(config);
    }

    public async Task<bool> DeleteConnectionAsync(int id)
    {
        var config = await _db.DbConnectionConfigs.FindAsync(id);
        if (config == null) return false;
        _db.DbConnectionConfigs.Remove(config);
        await _db.SaveChangesAsync();
        return true;
    }

    // ========== 密码加解密 ==========

    public static string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = AesKey;
        aes.IV = AesIv;
        var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(cipherBytes);
    }

    public static string Decrypt(string cipherText)
    {
        using var aes = Aes.Create();
        aes.Key = AesKey;
        aes.IV = AesIv;
        var decryptor = aes.CreateDecryptor();
        var cipherBytes = Convert.FromBase64String(cipherText);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    // ========== 构建连接字符串 ==========

    public static string BuildConnectionString(DbConnectionConfig config)
    {
        var password = !string.IsNullOrEmpty(config.EncryptedPassword) ? Decrypt(config.EncryptedPassword) : "";

        return config.DbType switch
        {
            "MySQL" => $"Server={config.Host};Port={config.Port};Database={config.DatabaseName};User={config.Username};Password={password};{config.ExtraParams}",
            "SqlServer" => $"Server={config.Host},{config.Port};Database={config.DatabaseName};User Id={config.Username};Password={password};TrustServerCertificate=True;{config.ExtraParams}",
            "SQLite" => $"Data Source={config.DatabaseName};{config.ExtraParams}",
            "Oracle" => $"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={config.Host})(PORT={config.Port}))(CONNECT_DATA=(SERVICE_NAME={config.DatabaseName})));User Id={config.Username};Password={password};{config.ExtraParams}",
            "PostgreSQL" => $"Host={config.Host};Port={config.Port};Database={config.DatabaseName};Username={config.Username};Password={password};{config.ExtraParams}",
            _ => throw new InvalidOperationException($"不支持的数据库类型: {config.DbType}"),
        };
    }

    private static DbConnection CreateConnection(string dbType, string connStr)
    {
        return dbType switch
        {
            "MySQL" => new MySqlConnection(connStr),
            "SqlServer" => new SqlConnection(connStr),
            "SQLite" => new SqliteConnection(connStr),
            "Oracle" => new OracleConnection(connStr),
            "PostgreSQL" => new NpgsqlConnection(connStr),
            _ => throw new InvalidOperationException($"不支持的数据库类型: {dbType}"),
        };
    }

    // ========== 测试连接 ==========

    public async Task<bool> TestConnectionAsync(DbConnectionRequest req)
    {
        var tempConfig = new DbConnectionConfig
        {
            DbType = req.DbType,
            Host = req.Host,
            Port = req.Port ?? 0,
            DatabaseName = req.DatabaseName,
            Username = req.Username,
            EncryptedPassword = !string.IsNullOrEmpty(req.Password) ? Encrypt(req.Password) : null,
            ExtraParams = req.ExtraParams,
        };
        var connStr = BuildConnectionString(tempConfig);
        using var conn = CreateConnection(req.DbType, connStr);
        await conn.OpenAsync();
        return true;
    }

    // ========== 获取表列表 ==========

    public async Task<List<TableInfo>> GetTablesAsync(int connectionId)
    {
        var config = await GetConnectionByIdAsync(connectionId)
            ?? throw new InvalidOperationException("连接配置不存在");

        var connStr = BuildConnectionString(config);
        using var conn = CreateConnection(config.DbType, connStr);
        await conn.OpenAsync();

        var sql = config.DbType switch
        {
            "MySQL" => "SELECT TABLE_NAME, TABLE_COMMENT FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @db ORDER BY TABLE_NAME",
            "SqlServer" => "SELECT TABLE_NAME, CAST(ISNULL(ep.value, '') AS NVARCHAR(500)) AS TABLE_COMMENT FROM INFORMATION_SCHEMA.TABLES t LEFT JOIN sys.extended_properties ep ON ep.major_id = OBJECT_ID(t.TABLE_SCHEMA + '.' + t.TABLE_NAME) AND ep.name = 'MS_Description' AND ep.minor_id = 0 WHERE t.TABLE_TYPE = 'BASE TABLE' ORDER BY t.TABLE_NAME",
            "SQLite" => "SELECT name AS TABLE_NAME, '' AS TABLE_COMMENT FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name",
            "Oracle" => "SELECT TABLE_NAME, CAST(COMMENTS AS NVARCHAR2(500)) AS TABLE_COMMENT FROM USER_TAB_COMMENTS ORDER BY TABLE_NAME",
            "PostgreSQL" => "SELECT tablename AS TABLE_NAME, CAST(obj_description(relfilenode, 'pg_class') AS VARCHAR) AS TABLE_COMMENT FROM pg_catalog.pg_tables WHERE schemaname = 'public' ORDER BY tablename",
            _ => throw new InvalidOperationException($"不支持的数据库类型: {config.DbType}"),
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (config.DbType == "MySQL")
        {
            var p = cmd.CreateParameter();
            p.ParameterName = "@db";
            p.Value = config.DatabaseName;
            cmd.Parameters.Add(p);
        }

        var tables = new List<TableInfo>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var tableName = reader.GetString(0);
            var comment = reader.IsDBNull(1) ? null : reader.GetString(1);

            // SQLite：从本系统预置说明中获取表注释
            if (config.DbType == "SQLite" && string.IsNullOrEmpty(comment))
                comment = SqliteTableComments.GetValueOrDefault(tableName);

            tables.Add(new TableInfo
            {
                TableName = tableName,
                TableComment = comment,
            });
        }
        return tables;
    }

    // ========== 获取表结构 ==========

    public async Task<List<ColumnInfo>> GetTableSchemaAsync(int connectionId, string tableName)
    {
        var config = await GetConnectionByIdAsync(connectionId)
            ?? throw new InvalidOperationException("连接配置不存在");

        var connStr = BuildConnectionString(config);
        using var conn = CreateConnection(config.DbType, connStr);
        await conn.OpenAsync();

        string sql;
        switch (config.DbType)
        {
            case "MySQL":
                sql = @"SELECT c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE,
                               CASE WHEN c.COLUMN_KEY = 'PRI' THEN 1 ELSE 0 END,
                               c.COLUMN_DEFAULT, c.COLUMN_COMMENT, c.CHARACTER_MAXIMUM_LENGTH
                        FROM INFORMATION_SCHEMA.COLUMNS c
                        WHERE c.TABLE_SCHEMA = @db AND c.TABLE_NAME = @table
                        ORDER BY c.ORDINAL_POSITION";
                break;
            case "SqlServer":
                sql = @"SELECT c.COLUMN_NAME, c.DATA_TYPE,
                               CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END,
                               CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END,
                               c.COLUMN_DEFAULT, CAST(ISNULL(ep.value, '') AS NVARCHAR(500)), c.CHARACTER_MAXIMUM_LENGTH
                        FROM INFORMATION_SCHEMA.COLUMNS c
                        LEFT JOIN (SELECT ku.TABLE_NAME, ku.COLUMN_NAME
                                   FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                                   JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                                   WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY') pk
                             ON c.TABLE_NAME = pk.TABLE_NAME AND c.COLUMN_NAME = pk.COLUMN_NAME
                        LEFT JOIN sys.extended_properties ep ON ep.major_id = OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME)
                             AND ep.minor_id = c.ORDINAL_POSITION AND ep.name = 'MS_Description'
                        WHERE c.TABLE_NAME = @table
                        ORDER BY c.ORDINAL_POSITION";
                break;
            case "SQLite":
                sql = $"PRAGMA table_info('{tableName.Replace("'", "''")}')";
                break;
            case "Oracle":
                sql = @"SELECT c.COLUMN_NAME, c.DATA_TYPE,
                               CASE WHEN c.NULLABLE = 'Y' THEN 1 ELSE 0 END,
                               CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END,
                               c.DATA_DEFAULT, CAST(cc.COMMENTS AS NVARCHAR2(500)), c.CHAR_LENGTH
                        FROM USER_TAB_COLUMNS c
                        LEFT JOIN (SELECT cols.COLUMN_NAME FROM USER_CONSTRAINTS cons
                                   JOIN USER_CONS_COLUMNS cols ON cons.CONSTRAINT_NAME = cols.CONSTRAINT_NAME
                                   WHERE cons.CONSTRAINT_TYPE = 'P') pk ON c.COLUMN_NAME = pk.COLUMN_NAME
                        LEFT JOIN USER_COL_COMMENTS cc ON c.TABLE_NAME = cc.TABLE_NAME AND c.COLUMN_NAME = cc.COLUMN_NAME
                        WHERE c.TABLE_NAME = UPPER(@table)
                        ORDER BY c.COLUMN_ID";
                break;
            case "PostgreSQL":
                sql = @"SELECT c.column_name, c.data_type,
                               CASE WHEN c.is_nullable = 'YES' THEN 1 ELSE 0 END,
                               CASE WHEN pk.column_name IS NOT NULL THEN 1 ELSE 0 END,
                               c.column_default, CAST(pd.description AS VARCHAR), c.character_maximum_length
                        FROM information_schema.columns c
                        LEFT JOIN (SELECT ku.table_name, ku.column_name
                                   FROM information_schema.table_constraints tc
                                   JOIN information_schema.key_column_usage ku ON tc.constraint_name = ku.constraint_name
                                   WHERE tc.constraint_type = 'PRIMARY KEY') pk
                             ON c.table_name = pk.table_name AND c.column_name = pk.column_name
                        LEFT JOIN pg_catalog.pg_statio_all_tables st ON c.table_name = st.relname
                        LEFT JOIN pg_catalog.pg_description pd ON pd.objoid = st.relid AND pd.objsubid = c.ordinal_position
                        WHERE c.table_schema = 'public' AND c.table_name = @table
                        ORDER BY c.ordinal_position";
                break;
            default:
                throw new InvalidOperationException($"不支持的数据库类型: {config.DbType}");
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        if (config.DbType == "MySQL")
        {
            var p1 = cmd.CreateParameter(); p1.ParameterName = "@db"; p1.Value = config.DatabaseName; cmd.Parameters.Add(p1);
            var p2 = cmd.CreateParameter(); p2.ParameterName = "@table"; p2.Value = tableName; cmd.Parameters.Add(p2);
        }
        else if (config.DbType is "SqlServer" or "PostgreSQL" or "Oracle")
        {
            var p = cmd.CreateParameter();
            p.ParameterName = "@table";
            p.Value = config.DbType == "Oracle" ? tableName.ToUpper() : tableName;
            cmd.Parameters.Add(p);
        }

        var columns = new List<ColumnInfo>();
        using var reader = await cmd.ExecuteReaderAsync();

        if (config.DbType == "SQLite")
        {
            // PRAGMA table_info 返回: cid, name, type, notnull, dflt_value, pk
            // 从本系统预置说明中获取列注释
            var colComments = SqliteColumnComments.GetValueOrDefault(tableName);
            while (await reader.ReadAsync())
            {
                var colName = reader.GetString(1);
                columns.Add(new ColumnInfo
                {
                    ColumnName = colName,
                    DataType = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    IsNullable = !reader.GetBoolean(3),
                    IsPrimaryKey = reader.GetBoolean(5),
                    DefaultValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Comment = colComments?.GetValueOrDefault(colName),
                });
            }
        }
        else
        {
            while (await reader.ReadAsync())
            {
                columns.Add(new ColumnInfo
                {
                    ColumnName = reader.GetString(0),
                    DataType = reader.GetString(1),
                    IsNullable = reader.GetInt32(2) == 1,
                    IsPrimaryKey = reader.GetInt32(3) == 1,
                    DefaultValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Comment = reader.IsDBNull(5) ? null : reader.GetString(5),
                    MaxLength = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                });
            }
        }
        return columns;
    }

    // ========== SQL 安全校验 ==========

    private static readonly HashSet<string> BlockedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "DROP", "DELETE", "TRUNCATE", "UPDATE", "INSERT", "ALTER", "CREATE",
        "EXEC", "EXECUTE", "GRANT", "REVOKE", "REPLACE", "MERGE", "CALL",
        "RENAME", "ATTACH", "DETACH", "VACUUM", "REINDEX",
    };

    public static void ValidateSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new InvalidOperationException("SQL 不能为空");

        // 提取第一个有意义的关键字（跳过注释和空白）
        var cleaned = Regex.Replace(sql, @"(--[^\n]*|/\*[\s\S]*?\*/)", " ", RegexOptions.Multiline);
        cleaned = cleaned.Trim();

        // 检查是否为空
        if (string.IsNullOrWhiteSpace(cleaned))
            throw new InvalidOperationException("SQL 不能为空");

        // 检查多语句（分号后还有内容）
        var semicolonIdx = cleaned.IndexOf(';');
        if (semicolonIdx >= 0 && semicolonIdx < cleaned.Length - 1)
        {
            var afterSemicolon = cleaned[(semicolonIdx + 1)..].Trim();
            if (!string.IsNullOrEmpty(afterSemicolon))
                throw new InvalidOperationException("不允许执行多条SQL语句");
        }

        // 提取第一个单词
        var firstWord = Regex.Match(cleaned, @"^\s*(\w+)").Groups[1].Value;

        if (string.IsNullOrEmpty(firstWord))
            throw new InvalidOperationException("无法识别SQL语句类型");

        if (BlockedKeywords.Contains(firstWord))
            throw new InvalidOperationException($"不允许执行 {firstWord} 操作，仅支持查询类SQL（SELECT/SHOW/DESCRIBE/EXPLAIN）");
    }

    // ========== 执行 SQL ==========

    public async Task<ExecuteSqlResponse> ExecuteSqlAsync(int connectionId, string sql)
    {
        ValidateSql(sql);

        var config = await GetConnectionByIdAsync(connectionId)
            ?? throw new InvalidOperationException("连接配置不存在");

        var connStr = BuildConnectionString(config);
        using var conn = CreateConnection(config.DbType, connStr);
        await conn.OpenAsync();

        var sw = Stopwatch.StartNew();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 30;

        using var reader = await cmd.ExecuteReaderAsync();

        // 提取列名（空列名自动生成默认名称，如 Column1）
        var columns = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            columns.Add(string.IsNullOrEmpty(name) ? $"Column{i + 1}" : name);
        }

        // 读取数据（最多500行）
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync() && rows.Count < 500)
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var val = reader.GetValue(i);
                row[columns[i]] = val == DBNull.Value ? null : val;
            }
            rows.Add(row);
        }

        sw.Stop();
        return new ExecuteSqlResponse
        {
            Columns = columns,
            Rows = rows,
            RowCount = rows.Count,
            ElapsedMs = sw.ElapsedMilliseconds,
        };
    }

    private static DbConnectionResponse Map(DbConnectionConfig c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        DbType = c.DbType,
        Host = c.Host,
        Port = c.Port,
        DatabaseName = c.DatabaseName,
        Username = c.Username,
        ExtraParams = c.ExtraParams,
        CreatedAt = c.CreatedAt,
    };
}
