# 技术栈

## 后端

| 组件 | 技术 | 说明 |
|------|------|------|
| 框架 | ASP.NET Core 10 | 跨平台 Web API 框架 |
| ORM | Entity Framework Core 10 | 对象关系映射，SQLite 提供者 |
| 认证 | JWT Bearer | JSON Web Token 无状态认证 |
| 密码 | BCrypt.Net | 安全密码哈希 |
| 缓存 | StackExchange.Redis | Redis 客户端，Token 缓存与在线追踪 |
| 对象存储 | MinIO | S3 兼容对象存储，附件管理 |
| 脚本 | Roslyn (CSharpScript) | 运行时 C# 代码编译执行 |
| 文档 | Swagger / Swashbuckle | OpenAPI 接口文档 |

## 前端

| 组件 | 技术 | 说明 |
|------|------|------|
| 框架 | Vue 3 | Composition API (`<script setup>`) |
| UI 库 | Element Plus | 企业级 UI 组件 |
| 构建 | Vite 8 | 极速开发服务器与构建 |
| HTTP | Axios | Promise 风格 HTTP 客户端 |
| 图表 | ECharts / vue-echarts | 数据可视化 |
| 表格 | xlsx | Excel 导入导出 |
| 路由 | Vue Router 4 | SPA 路由 |

## 基础设施

| 组件 | 技术 | 说明 |
|------|------|------|
| 数据库 | SQLite | 零配置嵌入式数据库 |
| 缓存 | Redis (Docker) | Token 缓存、在线状态 |
| 反向代理 | Nginx (Docker) | 生产环境静态文件 + API 代理 |
| 对象存储 | MinIO (Docker) | 附件文件存储 |

## 文档

| 工具 | 用途 |
|------|------|
| VitePress | 系统说明文档 |
| Swagger | API 接口文档 |
