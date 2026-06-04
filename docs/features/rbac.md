# 权限管理 (RBAC)

## 模型

```
用户 (User) ←→ 角色 (Role) ←→ 权限 (Permission)
     ↓                ↓
  冻结/解冻        角色 CRUD
  密码重置         权限分配（树形勾选）
```

## 三层结构

1. **权限 (Permission)**：最小授权单元，如 `user:view`、`inbound:manage`
2. **角色 (Role)**：权限的集合，如"超级管理员"、"普通用户"
3. **用户 (User)**：可分配多个角色

## 内置角色

| 角色 | 权限数 | 说明 |
|------|--------|------|
| 超级管理员 | 全部 20+ | 可管理所有资源 |
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
| 流程 | `workflow:manage`, `workflow:approve`, `workflow:monitor` | 流程审批 |
| 集成 | `integration:manage` | 集成平台 |
| 其他 | `sso:manage`, `oauth:manage`, `home:config`, `attachment:manage`, `database:manage` | |

## 权限验证

JWT Token 中携带 `permission` 声明，后端通过 Claims 校验：

```csharp
// 控制器中手动校验
if (!User.Claims.Any(c => c.Type == "permission" && c.Value == "role:manage"))
    return Forbid();
```
