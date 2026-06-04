# SSO / OAuth

## SSO 单点登录

支持生成一次性登录链接，第三方系统可通过链接直接登录。

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/sso/create` | 生成 SSO Token |
| POST | `/api/sso/create-auth-code` | 生成授权码（8位） |
| POST | `/api/sso/login` | Token 登录 |
| POST | `/api/sso/login-by-code` | 授权码登录 |
| GET | `/api/sso/tokens` | Token 列表 |
| POST | `/api/sso/revoke/{id}` | 撤销 Token |

## OAuth 2.0 / OIDC

支持标准 OAuth 2.0 授权码流程（PKCE）和 OpenID Connect。

| 端点 | 说明 |
|------|------|
| `GET /api/oauth/authorize` | 授权端点 |
| `POST /api/oauth/token` | Token 端点 |
| `GET /api/oauth/userinfo` | 用户信息端点 |
| `POST /api/oauth/revoke` | Token 撤销 |
| `GET /.well-known/openid-configuration` | OIDC Discovery |
| `GET /.well-known/jwks.json` | JWKS 公钥 |

## 内置客户端

| ClientId | 名称 | 授权类型 |
|------|------|------|
| `vue-spa` | Vue 前端应用 | authorization_code + PKCE |

## 回调地址

- `http://localhost:5173/callback`
- `http://localhost:8080/callback`
