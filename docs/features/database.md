# 数据库管理

## 功能

配置外部数据库连接，浏览表结构，执行只读 SQL 查询。支持 5 种主流数据库。

## 支持的数据库

| 类型 | 驱动 | 说明 |
|------|------|------|
| SQLite | Microsoft.Data.Sqlite | 零配置，文件型 |
| MySQL | MySql.Data | MySQL 5.7+ |
| SQL Server | Microsoft.Data.SqlClient | SQL Server 2016+ |
| PostgreSQL | Npgsql | PostgreSQL 12+ |
| Oracle | Oracle.ManagedDataAccess | Oracle 19c+ |

## API 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/database/connections` | 连接列表 |
| POST | `/api/database/connections` | 创建连接 |
| PUT | `/api/database/connections/{id}` | 编辑连接 |
| DELETE | `/api/database/connections/{id}` | 删除连接 |
| POST | `/api/database/test-connection` | 测试连接 |
| GET | `/api/database/{id}/tables` | 获取表列表 |
| GET | `/api/database/{id}/tables/{table}/schema` | 获取表结构（含注释） |
| POST | `/api/database/{id}/execute` | 执行 SQL（只读） |

## 安全机制

### 权限控制
- 仅具备 `database:manage` 权限的用户可访问（默认仅 admin）

### 密码加密
- 数据库连接密码使用 AES 加密存储
- 不在日志中明文输出

### SQL 执行限制
- **仅允许 SELECT 语句**（禁止 INSERT/UPDATE/DELETE/DROP/ALTER 等）
- **仅允许单条语句**（防止多语句注入）
- **最大返回 500 行**（防止大量数据导出）
- 前端提供 SQL 编辑器组件，显示执行耗时和错误信息

## 表结构浏览

- **表列表**：显示所有用户表，含注释信息
- **列信息**：字段名、类型、是否可空、默认值、注释
  - SQLite 通过内置字典提供字段注释
  - 其他数据库通过 `INFORMATION_SCHEMA` 或对应系统表获取
- **PRAGMA 支持**：SQLite 连接支持查询 PRAGMA 信息

## 连接字符串

系统自动根据数据库类型构建连接字符串：

| 数据库 | 格式 |
|--------|------|
| SQLite | `Data Source={文件路径}` |
| MySQL | `Server={Host};Port={Port};Database={Name};User={User};Password={Pwd}` |
| SQL Server | `Server={Host},{Port};Database={Name};User Id={User};Password={Pwd};TrustServerCertificate=True` |
| PostgreSQL | `Host={Host};Port={Port};Database={Name};Username={User};Password={Pwd}` |
| Oracle | `Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={Host})(PORT={Port}))(CONNECT_DATA=(SERVICE_NAME={Name})));User Id={User};Password={Pwd}` |
