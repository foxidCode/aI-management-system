# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概要

基于 ASP.NET Core 10 + Vue 3 + Element Plus 的企业级全栈管理系统。涵盖 RBAC 权限、入库单管理、低代码集成平台、OAuth 2.0/OIDC、计划任务调度、工作流引擎等。

## 技术栈

- 前端：Vue3 (Composition API, `<script setup>`) + JavaScript + Element Plus ^2.14 + Vite ^8.0
- 后端：.NET 10 Web API + EF Core (SQLite) + JWT Bearer + Swagger
- 基础设施：Redis (Token缓存/在线状态/SignalR背板) + MinIO (附件存储) + Nginx
- 认证：JWT HMAC-SHA256 + BCrypt + OAuth 2.0 (PKCE S256) + OIDC (RS256)

## 命令速查

```bash
# 前端（在 frontend/ 下执行）
npm run dev          # Vite 开发服务器 → localhost:5173
npm run build        # 生产构建

# 后端（在 backend/ 下执行）
dotnet run           # 启动 API → localhost:5000
dotnet build         # 编译检查
dotnet watch run     # 热重载开发

# 基础设施
docker compose up -d  # 启动 Redis + MinIO + Nginx
docker compose down   # 停止

# 文档
npx vitepress dev docs --port 5174   # VitePress 文档站点

# 一键启动/停止（Windows）
start.bat            # 自动启动所有服务
stop.bat             # 停止所有服务

# CI/CD
# Jenkins → http://localhost:8090
```

## 项目结构

```
demo/
├── backend/                          # .NET 10 Web API
│   ├── Controllers/                  # API 控制器（继承 ControllerBase）
│   ├── Models/                       # EF Core 实体（继承 DbContext 注册）
│   ├── DTOs/                         # 请求/响应 DTO
│   ├── Services/                     # 业务逻辑层（通过 DI 注册为 Scoped/Singleton/HostedService）
│   ├── Hubs/                         # SignalR Hub（实时通知）
│   ├── ScheduledTaskHandlers/         # IScheduledTaskHandler 实现（自动发现）
│   ├── Data/AppDbContext.cs           # EF Core DbContext
│   ├── SeedData.cs                    # 种子数据 + 增量迁移 SQL
│   ├── Program.cs                     # 应用入口、中间件管线、服务注册
│   └── appsettings.json               # 配置（含 SMTP 密码，勿提交真实凭证）
├── frontend/
│   └── src/
│       ├── api/auth.js                # Axios 实例 + 70+ API 函数（含拦截器）
│       ├── router/index.js            # 路由表 + JWT 守卫 + SSO/OAuth 回调处理
│       ├── composables/               # useOAuth.js、useSignalR.js
│       ├── components/                # 通用组件（WeatherCard、StatsCard、KeyValueEditor 等）
│       ├── views/                     # 页面组件（Dashboard 为布局壳）
│       └── workflow/                  # 工作流模块（设计器、表单渲染、store）
├── docs/                              # VitePress 文档站点（指南 + 功能文档）
├── MockThirdPartyApi/                 # 模拟第三方 ERP 系统（Minimal API，端口 5100）
├── SsoTestClient/                     # OAuth 2.0 + OIDC 控制台测试客户端
├── docker-compose.yml                 # Redis + MinIO + Nginx + 前后端容器化
├── Jenkinsfile                        # CI/CD 流水线（构建→测试→部署）
└── deploy.sh / docker-deploy.sh       # 部署脚本
```

## 后端架构要点

### 服务注册模式

所有 Service 在 `Program.cs` 中手动注册 DI：
- `AddScoped<>()` — 请求级服务（AuthService、PermissionService、InboundOrderService 等）
- `AddSingleton<>()` — 单例（RsaKeyProvider、MinioService、IConnectionMultiplexer）
- `AddHostedService<>()` — 后台服务（仅 ScheduleService 是 BackgroundService）

### SQLite 迁移模式

**不使用 EF Core Migration。** 建表和增量变更通过 `Program.cs` 中的 `db.Database.ExecuteSqlRaw()` 执行原始 SQL。新增字段用 `ALTER TABLE ... ADD COLUMN` 包裹在 `try/catch` 中：

```csharp
// 增量迁移模式（Program.cs 启动时执行）
try { db.Database.ExecuteSqlRaw(@"ALTER TABLE Users ADD COLUMN NewField TEXT"); } catch { }
```

**重置数据库：** 删除 `backend/auth.db`，重启后 `EnsureCreated()` + `SeedData.Initialize()` 自动重建。

### 认证管线

```
请求 → CORS → UseAuthentication (JWT Bearer + Cookie "IdpSession")
     → UseAuthorization → 在线追踪中间件 → Controller
```

- JWT 验证时额外检查 Redis 中 token 是否存在（支持吊销）
- Redis 不可用时降级跳过 Token 吊销检查
- SignalR WebSocket 通过 query string `?access_token=xxx` 传递 JWT

### 权限模型

User → UserRole → Role → RolePermission → Permission → Menu（菜单按 PermissionCode 过滤）

- `PermissionService.GetMenusForUserAsync()` 根据用户权限码过滤可见菜单
- 管理员 (`user:view` + `role:manage`) 额外获得 `menu:manage`，可看到 `IsVisible=false` 的菜单
- 权限码格式：`resource:action`（如 `user:create`、`inbound:manage`）

### 工作流系统

新增模块，含 4 张核心表：
- `WorkflowDefinitions` — 流程定义（Nodes 字段存 JSON 节点配置）
- `WorkflowInstances` — 流程实例（FormData 存提交表单 JSON）
- `WorkflowTasks` — 审批任务（AssigneeId、Status: Pending/Approved/Rejected）
- `WorkflowHistories` — 操作历史

流程引擎在前端 `frontend/src/workflow/` 中，含可视化设计器 (`WorkflowDesigner.vue`)、表单渲染 (`formRender.vue`)、状态管理 (`store/modules/workflow.js`)。

## 前端架构要点

### API 层

- `frontend/src/api/auth.js` — 集中管理的 Axios 实例
- 请求拦截器自动附加 `Authorization: Bearer <token>`
- 响应拦截器拦截 401 → 清除 token → 跳转 `/login`
- Vite 开发模式下 `/api` 请求代理到 `localhost:5000`，`/hubs` WebSocket 也代理

### 路由守卫

`router/index.js` `beforeEach` 处理三种场景：
1. 已登录 + `sso_token` 参数 → 清除 URL 中的 token 后放行
2. 未登录 + `sso_token` → 调用 SSO 一键登录后写入 localStorage
3. 未登录 + 访问需认证路由 → 重定向 `/login`

### 菜单系统

- Dashboard.vue 使用递归 `<MenuItem>` 组件渲染任意层级菜单
- `pathMenuMap` computed 扁平化菜单树，`handleMenuSelect` 根据 `openType` 分流：
  - `self` → `router.push()`
  - `blank` / 外部 URL → `window.open()`
  - `iframe` → 设置 `iframeUrl` ref，el-main 区渲染 `<iframe>` 替代 `<router-view>`
- 菜单搜索：顶栏输入框 → `flatMenus` computed 扁平化所有叶子节点 → 键盘上下导航

### SignalR 实时通知

- `composables/useSignalR.js` — 连接 `/hubs/notification`，监听 `ReceiveNotification`
- Dashboard 顶栏铃铛显示未读通知数，点击可跳转到工作流实例

## 重要约束和陷阱

### 不得修改的文件
- `.env` 文件
- 任何测试文件

### CSS 类名冲突
`@form-create/designer` 的 `icon.css` 定义了 `.icon-grid:before`、`.icon-cell:before` 等伪元素（使用 fc-icon 字体）。自定义组件避免使用 `icon-grid`、`icon-cell` 等类名，改用带前缀的类名（如 `menu-icon-grid`）。

### Element Plus 图标
- 图标名称来自 `@element-plus/icons-vue`（共 294 个），使用 PascalCase
- 在 popover/drawer 等 teleported 上下文中，`<component :is="'IconName'" />` 字符串解析可能失败，使用 `import * as Icons from '@element-plus/icons-vue'` 后传递 `Icons['IconName']` 组件引用
- 所有图标在 `main.js` 中已全局注册 `app.component(key, component)`

### 后端服务管理
- 运行时 `backend.exe` 占文件锁，需先 `taskkill /f /im backend.exe` 再 `dotnet build`
- 种子数据仅在 Roles 表为空时初始化，增量菜单/权限/材料通过 `Ensure*` 方法每次启动检查

## 内置账号

| 用户名 | 密码 | 角色 |
|--------|------|------|
| `admin` | `password` | 超级管理员（19 项权限） |

## 代码规范

- 函数命名用驼峰，组件命名用 PascalCase
- 所有新函数必须写类型注解
- 提交前运行 `npm run lint`
- 提交前用 /review 检查代码质量
