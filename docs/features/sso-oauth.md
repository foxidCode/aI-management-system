# SSO / OAuth

## SSO 单点登录

支持生成一次性登录凭证，第三方系统或跨设备可通过令牌/授权码直接登录。

### 两种模式

| 模式 | 凭证 | 使用方式 | 适用场景 |
|------|------|----------|----------|
| 魔法链接 (Magic Link) | 64 位随机 Token | 点击链接自动登录 | 邮件通知、第三方系统跳转 |
| 授权码 (Auth Code) | 8 位数字字母码 | 手动输入 | 跨设备、无网络共享 |

### API 端点

| 方法 | 路径 | 说明 | 认证 |
|------|------|------|------|
| POST | `/api/sso/create` | 生成 SSO Token（魔法链接） | 是 |
| POST | `/api/sso/create-auth-code` | 生成授权码（8位） | 是 |
| POST | `/api/sso/login` | Token 登录 | 否 |
| POST | `/api/sso/login-by-code` | 授权码登录 | 否 |
| GET | `/api/sso/tokens` | Token 列表 | 是 |
| POST | `/api/sso/revoke/{id}` | 撤销 Token | 是 |

### 授权码设计

- 8 位字符，排除易混淆字符（`0O1Il`），保留 30 个可辨识字符
- Redis 缓存 + 数据库双存储
- 使用后立即失效

### 前端集成

SSO Token 通过 URL 参数 `?sso_token=xxx` 传递，路由守卫自动检测并调用 SSO 登录接口。

---

## OAuth 2.0 / OpenID Connect

完整实现 OAuth 2.0 授权码流程（PKCE S256）和 OpenID Connect 1.0 协议。

### OAuth 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/oauth/authorize` | 授权端点（显示授权页面） |
| POST | `/api/oauth/token` | Token 端点 |
| GET/POST | `/api/oauth/userinfo` | 用户信息端点 |
| POST | `/api/oauth/revoke` | Token 撤销 |

### OIDC 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/.well-known/openid-configuration` | OIDC Discovery 文档 |
| GET | `/.well-known/jwks.json` | JWKS 公钥（RS256） |

### PKCE 流程

```
1. 客户端生成 CodeVerifier (32字节随机) + CodeChallenge (S256)
2. 重定向到 /api/oauth/authorize?code_challenge=xxx&code_challenge_method=S256
3. 用户登录并授权
4. 回调返回 authorization_code
5. POST /api/oauth/token { code, code_verifier, grant_type=authorization_code }
6. 返回 access_token + refresh_token + id_token
```

### Token 类型

| Token | 存储 | 有效期 | 说明 |
|-------|------|--------|------|
| Authorization Code | 数据库 (SHA256) | 10 分钟 | 一次性使用 |
| Access Token | JWT (HS256) | 1 小时 | Bearer Token |
| Refresh Token | 数据库 (SHA256) | 30 天 | 轮转刷新 |
| id_token | JWT (RS256) | 1 小时 | OIDC 身份令牌 |

### Refresh Token 轮转

- 每次使用 Refresh Token 后，旧的立即吊销，生成新的 Refresh Token
- 防止 Refresh Token 泄露后长期滥用
- 关联 Redis 缓存用于快速吊销检查

### 客户端管理

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/oauth/clients` | 客户端列表 |
| POST | `/api/oauth/clients` | 创建客户端 |
| GET | `/api/oauth/clients/{id}` | 客户端详情 |
| PUT | `/api/oauth/clients/{id}` | 更新客户端 |
| DELETE | `/api/oauth/clients/{id}` | 删除客户端 |

### 客户端属性

| 属性 | 说明 |
|------|------|
| ClientId | 唯一标识 |
| ClientSecret | BCrypt 哈希存储 |
| RedirectUris | 允许的回调地址列表 |
| AllowedScopes | 允许的 scope（openid/profile/email） |
| AllowedGrantTypes | 授权类型（authorization_code/refresh_token） |
| IsFirstParty | 是否第一方应用 |
| RequirePkce | 是否强制 PKCE |

### 内置客户端

| ClientId | 名称 | 授权类型 | PKCE |
|----------|------|----------|------|
| `vue-spa` | Vue 前端应用 | authorization_code + refresh_token | ✅ S256 |

回调地址：
- `http://localhost:5173/callback`
- `http://localhost:8080/callback`
- `http://localhost:5173/dashboard`

### id_token 结构

```json
{
  "iss": "AuthApi",
  "sub": "1",
  "aud": "vue-spa",
  "name": "admin",
  "email": "admin@example.com",
  "iat": 1717430400,
  "exp": 1717434000,
  "nonce": "abc123..."
}
```

使用 RSA 2048 密钥对签名（RS256），密钥首次启动自动生成并持久化。公钥通过 JWKS 端点对外公开。

### 测试客户端

项目包含 `SsoTestClient/` 控制台程序，可端到端测试完整 OAuth 2.0 + OIDC 流程：

```bash
cd SsoTestClient && dotnet run
```

测试步骤：
1. 生成 PKCE 参数（CodeVerifier + S256 Challenge + State + Nonce）
2. 构建并打开浏览器授权 URL
3. HTTP Listener（端口 18080）捕获回调
4. Code 换 Token + 解析 id_token
5. 调用 UserInfo + Refresh Token
6. OIDC Discovery + JWKS 验证
