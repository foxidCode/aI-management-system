# 技术栈

## 后端

| 组件 | 技术 | 说明 |
|------|------|------|
| 框架 | ASP.NET Core 10 | 跨平台 Web API 框架 |
| ORM | Entity Framework Core 10 | 对象关系映射，SQLite 提供者 |
| 认证 | JWT Bearer + OAuth 2.0 + OIDC | HMAC-SHA256 签名 / RS256 id_token |
| 密码 | BCrypt.Net | 安全密码哈希（客户端密码 + 用户密码） |
| 缓存 | StackExchange.Redis | Redis 客户端，Token 缓存、在线追踪、SSO 令牌 |
| 对象存储 | MinIO SDK | S3 兼容对象存储，附件管理 |
| 邮件 | MailKit | SMTP 邮件发送（密码重置验证码） |
| 脚本引擎 | Microsoft.CodeAnalysis.CSharp.Scripting | 运行时 C# 代码编译（集成平台 + 计划任务） |
| 流程引擎 | 自研 LogicFlow 状态机 | 可视化流程定义驱动，审批任务管理 |
| 文档 | Swagger / Swashbuckle | OpenAPI 接口文档 + JWT Bearer 认证 |
| 数据库驱动 | SQL Server / MySQL / PostgreSQL / Oracle | 外部数据库连接与管理 |

## 前端

| 组件 | 技术 | 说明 |
|------|------|------|
| 框架 | Vue 3 | Composition API (`<script setup>`) |
| UI 库 | Element Plus | 企业级 UI 组件 |
| 流程图 | LogicFlow | 流程设计器（拖拽节点 + 连线） |
| 图表 | ECharts / vue-echarts | 入库金额趋势图 |
| 构建 | Vite 8 | 开发服务器 + 生产构建 |
| HTTP | Axios | 请求/响应拦截，自动 Token 附加 |
| 表格 | xlsx | Excel 导出 |
| 路由 | Vue Router 4 | SPA 路由 + 导航守卫 |

## 基础设施

| 组件 | 技术 | 说明 |
|------|------|------|
| 数据库 | SQLite | 零配置嵌入式数据库，WAL 模式 |
| 缓存 | Redis (Docker) | Token 缓存、在线状态、SSO |
| 反向代理 | Nginx (Docker) | 前端 + API 反向代理 |
| 对象存储 | MinIO (Docker) | 附件文件存储（S3 协议） |

## 文档

| 工具 | 用途 |
|------|------|
| VitePress | 项目说明文档 |
| Swagger | API 接口文档 |
