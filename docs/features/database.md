# 数据库管理

## 功能

配置外部数据库连接，浏览表结构，执行 SQL 查询。

## 支持的数据库

| 类型 | 说明 |
|------|------|
| MySQL | MySQL 5.7+ |
| SQL Server | SQL Server 2016+ |
| PostgreSQL | PostgreSQL 12+ |

## API 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/database/connections` | 连接列表 |
| POST | `/api/database/connections` | 创建连接 |
| PUT | `/api/database/connections/{id}` | 编辑连接 |
| DELETE | `/api/database/connections/{id}` | 删除连接 |
| POST | `/api/database/test-connection` | 测试连接 |
| GET | `/api/database/{id}/tables` | 获取表列表 |
| GET | `/api/database/{id}/tables/{table}/schema` | 获取表结构 |
| POST | `/api/database/{id}/execute` | 执行 SQL |

## 安全

- 仅具备 `database:manage` 权限的用户可访问（默认仅 admin）
- 密码加密存储
- SQL 查询限制返回条数
