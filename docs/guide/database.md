# 数据库设计

## 数据库

- **类型**：SQLite
- **文件**：`backend/auth.db`（首次运行自动创建）
- **ORM**：Entity Framework Core 10

## 核心表（28 张）

### 用户与权限

| 表 | 说明 |
|------|------|
| Users | 用户（用户名、邮箱、密码哈希、状态） |
| Roles | 角色（名称、描述） |
| Permissions | 权限（名称、编码、描述） |
| UserRoles | 用户-角色关联（多对多） |
| RolePermissions | 角色-权限关联（多对多） |
| Menus | 菜单（名称、路径、图标、父级、排序） |

### 业务表

| 表 | 说明 |
|------|------|
| InboundOrders | 入库单（编码、供应商、金额、状态、审批关联） |
| InboundOrderDetails | 入库明细（材料、数量、单价、税额） |
| MaterialDictionaries | 材料字典（编码、名称、规格、型号） |
| Attachments | 统一附件（模块、关联ID、MinIO Key） |

### 流程审批

| 表 | 说明 |
|------|------|
| WorkflowDefinitions | 流程定义（名称、分类、版本、状态、节点数据 JSON） |
| WorkflowInstances | 流程实例（定义关联、模块、实体ID、状态、节点快照） |
| WorkflowTasks | 审批任务（实例关联、审批人、状态、意见） |

### 其他

| 表 | 说明 |
|------|------|
| ScheduledTasks | 计划任务（Cron、代码处理器、参数） |
| IntegrationConnections | 集成连接配置 |
| IntegrationTasks | 集成数据同步任务 |
| IntegrationLogs | 集成执行日志 |
| OAuthClients / AuthorizationCodes / RefreshTokens | OAuth 2.0 |
| SsoTokens | SSO 登录令牌 |
| DbConnectionConfigs | 外部数据库配置 |
| PasswordResetTokens | 密码重置令牌 |

## 权限码

```json
user:view, user:create, user:edit, user:delete, user:freeze,
user:reset_password, role:manage, role:assign_permission,
system:config, material:view, material:manage,
inbound:view, inbound:manage, sso:manage, oauth:manage,
home:config, attachment:manage, database:manage,
integration:manage, workflow:manage, workflow:approve,
workflow:monitor
```
