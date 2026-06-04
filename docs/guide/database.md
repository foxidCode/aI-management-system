# 数据库设计

## 主数据库

- **类型**：SQLite
- **文件**：`backend/auth.db`（首次运行自动创建）
- **ORM**：Entity Framework Core 10
- **模式**：WAL（Write-Ahead Logging），读并发、写串行

## 核心表（28+ 张）

### 用户与权限

| 表 | 说明 |
|------|------|
| Users | 用户（用户名、邮箱、BCrypt 密码哈希、性别、身份证、工号、备注、主页配置 JSON、冻结状态、时间戳） |
| Roles | 角色（名称、描述、创建时间） |
| Permissions | 权限（名称、编码、描述） |
| UserRoles | 用户-角色关联（多对多） |
| RolePermissions | 角色-权限关联（多对多） |
| Menus | 菜单（名称、路径、图标、父级ID、排序、权限码、类型、组件） |
| PasswordResetTokens | 密码重置令牌（邮箱、6位验证码、过期时间、是否使用） |

### 业务表

| 表 | 说明 |
|------|------|
| InboundOrders | 入库单（编码 RK-YYYYMMDD-NNNN、仓库、供应商、合同、含税/成本/税额合计、税率、状态、流程实例、软删除） |
| InboundOrderDetails | 入库明细（材料编码/名称/规格/型号/单位、数量、单价、含税金额、税额、成本金额） |
| InboundOrderAttachments | 入库单附件（文件名、MinIO ObjectKey、文件大小、类型） |
| MaterialDictionaries | 材料字典（编码、名称、规格、型号、单位、备注） |
| Attachments | 统一附件（模块名、关联ID、关联名称、文件名、MinIO Key、大小、类型、上传者） |

### 流程审批

| 表 | 说明 |
|------|------|
| WorkflowDefinitions | 流程定义（名称、分类、版本、状态、节点数据 JSON） |
| WorkflowInstances | 流程实例（定义关联、模块、实体ID、当前节点、状态、发起人、节点快照） |
| WorkflowTasks | 审批任务（实例关联、节点ID/名称、类型、审批人、状态、意见、已读标记） |

### OAuth / SSO

| 表 | 说明 |
|------|------|
| OAuthClients | OAuth 客户端（ClientId、BCrypt ClientSecret、回调URI、作用域、授权类型） |
| AuthorizationCodes | 授权码（SHA256 哈希、用户、客户端、PKCE 参数、作用域、Nonce、过期） |
| RefreshTokens | 刷新令牌（SHA256 哈希、用户、客户端、作用域、吊销、过期） |
| SsoTokens | SSO 令牌（Token、授权码、类型、用户、创建人、过期、使用状态） |

### 集成与调度

| 表 | 说明 |
|------|------|
| IntegrationConnections | 集成连接（名称、BaseUrl、认证类型/配置、默认 Headers） |
| IntegrationTasks | 集成任务（来源/目标连接、字段映射、代码处理器、目标类型、子表配置） |
| IntegrationLogs | 执行日志（任务ID、方向、状态、请求/响应、错误、耗时） |
| ScheduledTasks | 计划任务（名称、Cron、集成任务关联、代码处理器、处理器类、参数、启用） |
| DbConnectionConfigs | 外部数据库配置（名称、类型、主机、端口、库名、用户名、AES 加密密码） |

## 权限码（22 项）

```
user:view, user:create, user:edit, user:delete, user:freeze,
user:reset_password, role:manage, role:assign_permission,
system:config, material:view, material:manage,
inbound:view, inbound:manage, sso:manage, oauth:manage,
home:config, attachment:manage, database:manage,
integration:manage, workflow:manage, workflow:approve,
workflow:monitor
```

## 种子数据

`SeedData.cs` 在首次启动时自动执行：

1. **权限**：13 个基础权限 + 每次启动增量添加新权限
2. **角色**：超级管理员（全部）、普通用户（2项）、只读用户（1项）
3. **菜单**：主页 + 系统管理（含子菜单）+ 流程中心 + 接口文档 + 帮助中心
4. **管理员**：`admin / password`（BCrypt）
5. **材料**：500 条随机种子材料（10 分类，固定种子保证一致）
6. **OAuth 客户端**：`vue-spa`（PKCE 启用）

每次启动的增量添加使用 `INSERT OR IGNORE` 语义，不会影响已有数据。

## 增量迁移

`Program.cs` 中通过 `EnsureCreated()` + 一系列 `ExecuteSqlRaw` 实现轻量级增量迁移，适合 SQLite 场景。新增表/列通过 SQL 手动添加，并在首次启动时自动执行。
