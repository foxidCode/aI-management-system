# 用户认证

## 认证流程

```
用户输入凭证 → POST /api/auth/login → BCrypt 验证密码
  → 生成 JWT Token → 缓存到 Redis → 返回 Token + 权限列表
```

## API 端点

| 方法 | 路径 | 说明 | 认证 |
|------|------|------|------|
| POST | `/api/auth/register` | 注册新用户 | 否 |
| POST | `/api/auth/login` | 登录获取 Token | 否 |
| POST | `/api/auth/logout` | 登出（清除缓存） | 是 |
| POST | `/api/auth/send-reset-code` | 发送重置验证码 | 否 |
| POST | `/api/auth/reset-password` | 重置密码 | 否 |

## JWT Token

- 算法：HMAC-SHA256
- 有效期：2 小时
- 载荷：`nameidentifier`（用户ID）、`name`（用户名）、`email`、`permission`（权限列表）
- 验证：签发者、受众、签名密钥、Redis 黑名单

## 在线状态

通过 Redis `online:{userId}` 键追踪最近 5 分钟有活动的用户，在线状态在用户列表中显示。

## 内置账号

| 用户名 | 密码 | 角色 |
|--------|------|------|
| `admin` | `password` | 超级管理员 |

> 生产环境请务必修改默认密码。
