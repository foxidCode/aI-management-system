# 用户认证

## 认证方式

系统支持多种认证方式：

| 方式 | 说明 | 适用场景 |
|------|------|----------|
| 用户名密码 | JWT + BCrypt 验证 | 主登录方式 |
| SSO 魔法链接 | 64位随机令牌链接 | 第三方系统免密登录 |
| SSO 授权码 | 8位数字字母码 | 跨设备登录 |
| OAuth 2.0 | 授权码流程 + PKCE | 第三方应用接入 |
| OIDC | OpenID Connect 身份认证 | 统一身份平台 |

## 认证流程

```
用户输入凭证 → POST /api/auth/login → BCrypt 验证密码
  → 检查冻结状态 → 生成 JWT Token
  → 缓存到 Redis (token:{jwt}) → 设置在线状态 (online:{userId})
  → 返回 Token + 权限列表 + 用户信息
```

## API 端点

| 方法 | 路径 | 说明 | 认证 |
|------|------|------|------|
| POST | `/api/auth/register` | 注册新用户（默认"普通用户"角色） | 否 |
| POST | `/api/auth/login` | 用户名密码登录 | 否 |
| POST | `/api/auth/logout` | 登出（清除 Redis 缓存 + 在线状态） | 是 |
| POST | `/api/auth/send-reset-code` | 发送密码重置验证码（邮件） | 否 |
| POST | `/api/auth/reset-password` | 通过验证码重置密码 | 否 |
| GET | `/api/auth/profile` | 获取当前用户信息 | 是 |
| PUT | `/api/auth/profile` | 更新个人信息（需验证当前密码） | 是 |

## JWT Token

- **算法**：HMAC-SHA256
- **有效期**：2 小时
- **载荷**：
  - `nameidentifier` — 用户 ID
  - `name` — 用户名
  - `email` — 邮箱
  - `permission` — 权限码列表（多个同名声明）
- **验证流程**：
  1. 验证签名 + 签发者 + 受众
  2. 检查 Redis 黑名单（`token:{jwt}` 键是否存在）
  3. 通过后注入 ClaimsPrincipal

## Token 吊销

登出时将当前 JWT 写入 Redis，设置与 Token 剩余有效期相同的 TTL。每次请求 `OnTokenValidated` 事件检查 Redis 黑名单。

## 在线状态

通过 Redis `online:{userId}` 键追踪用户活动：

- 每个认证请求刷新 TTL（5 分钟）
- 用户列表页显示绿点（在线）/ 灰点（离线）
- 首页统计卡片显示在线用户数和头像
- 管理员可强制踢出用户（删除 Redis 键）

## Cookie IdP 会话

OAuth 2.0 授权页面使用独立的 Cookie 认证方案（`IdpSession`），用户登录后签发 IdP Cookie，授权页面可直接识别已登录用户，无需重复输入密码。

## 密码重置

1. 用户输入已注册邮箱
2. 系统生成 6 位数字验证码，通过 SMTP 发送邮件
3. 验证码 5 分钟有效，存储在 `PasswordResetTokens` 表
4. 用户输入验证码 + 新密码完成重置

## 注册

- 新用户注册后自动分配"普通用户"角色
- 用户名和邮箱唯一性校验
- 密码 BCrypt 哈希存储

## 内置账号

| 用户名 | 密码 | 角色 |
|--------|------|------|
| `admin` | `password` | 超级管理员（不可冻结） |

> 生产环境请务必修改默认密码。
