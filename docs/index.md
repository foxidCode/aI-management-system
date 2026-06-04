---
layout: home

hero:
  name: "权限管理系统"
  text: "企业级全栈管理平台"
  tagline: 基于 ASP.NET Core 10 + Vue 3 + Element Plus，涵盖 RBAC 权限控制、流程审批、低代码集成、OAuth 2.0/OIDC 单点登录
  actions:
    - theme: brand
      text: 快速开始
      link: /guide/getting-started
    - theme: alt
      text: 功能概览
      link: /features/auth

features:
  - icon: 🔐
    title: 认证与授权
    details: JWT + BCrypt 安全认证，RBAC 用户→角色→权限三层模型，22 项细粒度权限码，动态菜单按权限过滤
  - icon: 📋
    title: 流程审批
    details: LogicFlow 可视化拖拽建模，5 种节点类型（开始/审批/抄送/条件/结束），任签/会签策略，版本管理，超时自动处理
  - icon: 📦
    title: 入库单管理
    details: 完整采购入库流程，自动编码与金额计算，材料明细管理，附件上传（MinIO），CSV 导出，日统计
  - icon: ⚙️
    title: 低代码集成平台
    details: HTTP API 连接管理（5 种认证方式），Pull→Transform→Push 流水线，字段映射，C# 脚本处理器，完整执行日志
  - icon: ⏰
    title: 计划任务调度
    details: Cron 定时调度，一次性任务，4 个内置处理器，支持 C# 脚本和反射处理器，参数化配置，手动触发
  - icon: 🗄️
    title: 外部数据库管理
    details: 支持 MySQL / SQL Server / SQLite / Oracle / PostgreSQL 五种数据库，表结构浏览，只读 SQL 执行
  - icon: 🔗
    title: SSO / OAuth 2.0
    details: OAuth 2.0 授权码流程 + PKCE S256，OpenID Connect，RS256 签名 id_token，JWKS 端点，多客户端管理
  - icon: 🏠
    title: 可配置仪表盘
    details: 6 种功能卡片（天气/图表/统计/时钟/待办/已办），拖拽布局，用户级配置持久化，IP 自动天气定位
---

## 技术栈

| 层级 | 技术 | 说明 |
|------|------|------|
| 后端框架 | ASP.NET Core | .NET 10.0 |
| ORM | Entity Framework Core | SQLite 存储 |
| 认证 | JWT Bearer + BCrypt + OAuth 2.0 + OIDC | HMAC-SHA256 / RS256 |
| 缓存 | Redis | Token 缓存、在线状态、SSO |
| 对象存储 | MinIO | 附件上传下载 |
| 邮件 | MailKit (SMTP) | 密码重置验证码 |
| 流程引擎 | 自研 LogicFlow 状态机 | 可视化建模 + 审批流转 |
| 前端框架 | Vue 3 + Element Plus + LogicFlow | Composition API |
| 构建工具 | Vite | ^8.0 |
| 文档 | VitePress | latest |

## 快速启动

```bash
# 1. 启动 Docker 服务（Redis + MinIO + Nginx）
docker compose up -d

# 2. 启动后端（首次运行自动创建数据库和种子数据）
cd backend && dotnet run

# 3. 启动前端
cd frontend && npm run dev

# 4. 启动文档
npx vitepress dev docs --port 5174
```

| 服务 | 地址 |
|------|------|
| 前端应用 | http://localhost:5173 |
| 后端 API | http://localhost:5000 |
| Swagger 文档 | http://localhost:5000/swagger |
| VitePress 文档 | http://localhost:5174 |
| MinIO 控制台 | http://localhost:9001 |

默认管理员账号：`admin` / `password`

## 项目组成

```
demo/
├── backend/           ASP.NET Core 后端（19 控制器 + 17 服务）
├── frontend/          Vue 3 前端（24 页面 + 7 组件）
├── docs/              VitePress 文档站点
├── MockThirdPartyApi/ 第三方 ERP 模拟 API
├── SsoTestClient/     OAuth 2.0 / OIDC 测试客户端
└── docker-compose.yml Docker 基础设施（Redis + MinIO + Nginx）
```
