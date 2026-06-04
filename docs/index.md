---
layout: home

hero:
  name: "权限管理系统"
  text: "企业级全栈管理平台"
  tagline: 基于 ASP.NET Core 10 + Vue 3 + Element Plus，支持 RBAC 权限控制、流程审批、集成平台
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
    details: JWT + BCrypt 安全认证，基于 RBAC 模型的细粒度权限控制，支持角色、权限、菜单三级管理
  - icon: 📋
    title: 流程审批
    details: 可视化拖拽建模，支持审批/抄送/条件分支节点，版本管理，会签/任签策略，超时自动处理
  - icon: 📦
    title: 入库单管理
    details: 完整的采购入库流程，材料明细管理，附件上传，CSV 导出，含税金额自动计算
  - icon: ⚙️
    title: 集成平台
    details: 低代码数据同步，HTTP API 连接配置，C# 脚本处理器，字段映射，定时调度
  - icon: ⏰
    title: 计划任务
    details: Cron 定时调度，支持自定义 C# 脚本和反射处理器，参数化配置，执行日志追踪
  - icon: 🗄️
    title: 数据库管理
    details: 多数据库连接管理，表结构浏览，SQL 查询执行，支持 MySQL / SQL Server / PostgreSQL
---

## 技术栈

| 层级 | 技术 | 版本 |
|------|------|------|
| 后端框架 | ASP.NET Core | .NET 10.0 |
| ORM | Entity Framework Core (SQLite) | 10.0.8 |
| 认证 | JWT Bearer + BCrypt | — |
| 缓存 | Redis (StackExchange.Redis) | — |
| 前端框架 | Vue 3 (Composition API) | ^3.5.34 |
| UI 组件 | Element Plus | ^2.14.1 |
| 构建工具 | Vite | ^8.0.12 |
| 文档 | VitePress | latest |

## 快速启动

```bash
# 1. 启动 Docker 服务（Redis + MinIO + Nginx）
docker compose up -d

# 2. 启动后端
cd backend && dotnet run

# 3. 启动前端
cd frontend && npm run dev

# 4. 访问
# 前端: http://localhost:5173
# 后端: http://localhost:5000
# Swagger: http://localhost:5000/swagger
```

默认管理员账号：`admin` / `password`
