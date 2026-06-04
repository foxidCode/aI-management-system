# 权限管理 (RBAC)

## 模型

```
用户 (User) ←→ 角色 (Role) ←→ 权限 (Permission)
     ↓                ↓
  冻结/解冻        角色 CRUD
  密码重置         权限分配（树形勾选）
  在线追踪         级联删除
```

## 三层结构

1. **权限 (Permission)**：最小授权单元，如 `user:view`、`inbound:manage`
2. **角色 (Role)**：权限的集合，如"超级管理员"、"普通用户"
3. **用户 (User)**：可分配多个角色，权限为所有角色权限的并集

## 内置角色

| 角色 | 权限数 | 说明 |
|------|--------|------|
| 超级管理员 | 全部 22 | 可管理所有资源、分配角色 |
| 普通用户 | 2 | `user:view` + `system:config` |
| 只读用户 | 1 | 仅 `user:view` |

## 权限码列表

| 模块 | 权限码 | 说明 |
|------|--------|------|
| 用户 | `user:view`, `user:create`, `user:edit`, `user:delete`, `user:freeze` | 用户管理 |
| 密码 | `user:reset_password` | 重置密码 |
| 角色 | `role:manage`, `role:assign_permission` | 角色管理 |
| 系统 | `system:config` | 系统配置 |
| 材料 | `material:view`, `material:manage` | 材料字典 |
| 入库 | `inbound:view`, `inbound:manage` | 入库单 |
| SSO | `sso:manage` | SSO 令牌管理 |
| OAuth | `oauth:manage` | OAuth 客户端管理 |
| 主页 | `home:config` | 主页布局配置 |
| 附件 | `attachment:manage` | 统一附件管理 |
| 数据库 | `database:manage` | 外部数据库管理 |
| 集成 | `integration:manage` | 集成平台 |
| 流程 | `workflow:manage`, `workflow:approve`, `workflow:monitor` | 流程审批 |

## 权限验证

JWT Token 中携带 `permission` 声明（可多个同名声明），后端通过 Claims 校验：

```csharp
// 控制器中手动校验
if (!User.Claims.Any(c => c.Type == "permission" && c.Value == "role:manage"))
    return Forbid();
```

## 动态菜单过滤

- 每个菜单项可配置 `PermissionCode`
- `PermissionCode` 为空：所有人可见
- `PermissionCode` 有值：仅拥有对应权限的用户可见
- 管理员用户自动获取全部菜单（包括 `database:manage`）

## 角色管理

### 创建/编辑角色
- 角色名唯一
- 树形权限勾选（按菜单层级组织权限节点）
- 支持父子级联选择

### 删除角色
- 级联删除关联的用户-角色和角色-权限关系

## 用户-角色分配

- 管理员可为用户分配/修改角色
- 一个用户可有多个角色
- 角色变更后用户需重新登录获取新 JWT

## 权限树

前端角色管理页面的权限树按菜单层级组织：

```
系统管理
  ├── 用户列表 → user:view, user:create, user:edit, user:delete, user:freeze, user:reset_password
  ├── 角色管理 → role:manage, role:assign_permission
  ├── 材料字典 → material:view, material:manage
  ├── 入库单   → inbound:view, inbound:manage
  ├── SSO 管理 → sso:manage
  └── ...
流程中心
  ├── 流程设计 → workflow:manage
  ├── 待办任务 → workflow:approve
  └── 流程监控 → workflow:monitor
```
