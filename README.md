# 管理系统 (Management System)

基于 **ASP.NET Core 10** + **Vue 3** + **Element Plus** 的企业级全栈管理系统。涵盖 RBAC 权限控制、入库单管理、低代码集成平台、OAuth 2.0/OIDC 单点登录、计划任务调度、外部数据库管理等完整功能。

## 技术栈

| 层级 | 技术 | 版本/说明 |
|------|------|-----------|
| 后端框架 | ASP.NET Core | .NET 10.0 |
| ORM | Entity Framework Core (SQLite) | 10.0 |
| 认证 | JWT Bearer + BCrypt + OAuth 2.0 + OIDC | HMAC-SHA256 / RS256 |
| 缓存与会话 | Redis (StackExchange.Redis) | Token 缓存 / 在线状态 / SSO |
| 对象存储 | MinIO (Minio SDK) | 附件上传 / 下载 |
| 邮件 | MailKit (SMTP) | 密码重置验证码 |
| 脚本引擎 | Microsoft.CodeAnalysis.CSharp.Scripting | 集成平台 / 计划任务 |
| 前端框架 | Vue 3 (Composition API) | ^3.5 |
| UI 组件 | Element Plus | ^2.14 |
| 图表 | ECharts (vue-echarts) | ^6 |
| 构建工具 | Vite | ^8.0 |
| 文档 | VitePress | ^1.6 |
| 基础设施 | Docker Compose | Redis + MinIO + Nginx |
| 数据库驱动 | MySQL / SQL Server / PostgreSQL / Oracle | 外部数据库管理 |

---

## 项目结构

```
demo/
├── backend/                         # ASP.NET Core 后端
│   ├── Controllers/                 # 16 个 API 控制器
│   │   ├── AuthController.cs            # 登录 / 注册 / 密码重置
│   │   ├── UserController.cs            # 用户信息 / 主页配置 / 在线用户
│   │   ├── UserManagementController.cs  # 用户 CRUD / 冻结 / 批量操作 / 角色分配
│   │   ├── RoleController.cs            # 角色 CRUD + 权限分配
│   │   ├── MenuController.cs            # 动态菜单（按权限过滤）
│   │   ├── PermissionController.cs      # 权限列表 / 权限树
│   │   ├── MaterialController.cs        # 材料字典 CRUD
│   │   ├── InboundOrderController.cs    # 入库单 CRUD / 明细 / 附件 / 导出
│   │   ├── AttachmentController.cs      # 统一附件管理（MinIO）
│   │   ├── DatabaseController.cs        # 外部数据库连接 / 表结构 / SQL 执行
│   │   ├── IntegrationController.cs     # 集成平台（连接 / 任务 / 日志）
│   │   ├── ScheduleController.cs        # 计划任务调度（Cron / 一次性）
│   │   ├── OAuthController.cs           # OAuth 2.0 授权 / Token / 用户信息
│   │   ├── OAuthClientController.cs     # OAuth 客户端管理
│   │   ├── SsoController.cs             # SSO 魔法链接 / 授权码登录
│   │   └── WeatherController.cs         # 天气查询（IP 定位 + wttr.in）
│   ├── Models/                      # 25+ 实体模型
│   ├── DTOs/                        # 10 组数据传输对象
│   ├── Services/                    # 14 个业务服务
│   │   ├── AuthService.cs              # JWT 签发 / BCrypt / Redis 缓存 / 在线追踪
│   │   ├── PermissionService.cs        # RBAC 权限 + 角色 + 菜单
│   │   ├── EmailService.cs             # SMTP 邮件发送
│   │   ├── InboundOrderService.cs      # 入库单 + 材料字典业务
│   │   ├── AttachmentService.cs        # 统一附件管理
│   │   ├── MinioService.cs             # MinIO S3 客户端
│   │   ├── DatabaseService.cs          # 多数据库连接 / SQL 安全执行
│   │   ├── IntegrationService.cs       # 低代码集成引擎
│   │   ├── ScheduleService.cs          # Cron 调度器（BackgroundService）
│   │   ├── OAuthService.cs             # OAuth 2.0 授权码流程 + PKCE
│   │   ├── OidcService.cs              # OpenID Connect
│   │   ├── RsaKeyProvider.cs           # RSA 2048 密钥对管理 + JWKS
│   │   └── SsoService.cs               # SSO 令牌 / 授权码
│   ├── ScheduledTaskHandlers/      # 计划任务处理器
│   │   ├── CreateUserHandler.cs        # 自动创建用户
│   │   ├── CleanupLogsHandler.cs       # 清理日志
│   │   ├── DataExportHandler.cs        # 数据导出
│   │   └── MaterialStatsHandler.cs     # 材料统计
│   ├── Data/AppDbContext.cs         # EF Core 数据库上下文
│   ├── SeedData.cs                  # 种子数据（角色 / 权限 / 菜单 / 材料 / OAuth 客户端）
│   └── Program.cs                   # 应用入口 + 中间件配置
├── frontend/                        # Vue 3 前端
│   └── src/
│       ├── api/auth.js                  # 70+ API 函数封装
│       ├── router/index.js              # 路由 + 守卫（含 SSO/OAuth 回调）
│       ├── composables/useOAuth.js      # OAuth 2.0 + PKCE 工具
│       ├── components/                  # 通用组件
│       │   ├── WeatherCard.vue              # 天气卡片
│       │   ├── InboundChartCard.vue         # 入库图表卡片
│       │   ├── StatsCard.vue                # 在线用户统计卡片
│       │   ├── ClockCard.vue                # 实时时钟卡片
│       │   └── KeyValueEditor.vue           # 键值对编辑器
│       └── views/                       # 18 个页面组件
│           ├── Login.vue / Register.vue     # 登录 / 注册
│           ├── Dashboard.vue                # 主布局（动态菜单 + 搜索）
│           ├── HomePage.vue                 # 可配置卡片仪表盘
│           ├── UserProfile.vue              # 个人信息修改
│           ├── UserManagement.vue           # 用户管理
│           ├── RoleManagement.vue           # 角色管理（树形权限）
│           ├── MaterialDictionary.vue       # 材料字典
│           ├── InboundOrder.vue             # 入库单列表
│           ├── InboundOrderDetail.vue       # 入库单详情 + 附件
│           ├── OAuthCallback.vue            # OAuth 回调处理
│           ├── SsoManagement.vue            # SSO 令牌管理
│           ├── OAuthClientManagement.vue    # OAuth 客户端管理
│           ├── HomeConfig.vue               # 主页布局配置
│           ├── AttachmentManagement.vue     # 统一附件管理
│           ├── DatabaseManagement.vue       # 外部数据库管理
│           ├── IntegrationManagement.vue    # 集成平台
│           ├── ScheduleManagement.vue       # 计划任务管理
├── docs/                            # VitePress 文档站点
│   ├── index.md                         # 文档首页
│   ├── guide/                           # 指南（部署 / 架构 / 数据库 / 性能）
│   └── features/                        # 功能文档（8 篇）
├── MockThirdPartyApi/               # 第三方 API 模拟（ERP 系统）
│   └── Program.cs                       # 物料查询 / 采购单同步 / 登录认证
├── SsoTestClient/                   # OAuth 2.0 + OIDC 测试客户端
│   └── Program.cs                       # PKCE 流程端到端测试
├── docker-compose.yml               # Docker 编排（Redis + MinIO + Nginx）
├── nginx.conf                       # Nginx 反向代理配置
├── start.bat / stop.bat             # Windows 一键启动 / 停止脚本
└── .gitignore
```

---

## 功能特性

### 核心功能

| 模块 | 说明 |
|------|------|
| 🔐 **用户认证** | JWT + BCrypt 登录/注册，Redis Token 缓存与吊销，Cookie IdP 会话 |
| 👥 **用户管理** | 分页/搜索/排序，CRUD，冻结/解冻，批量操作，在线状态追踪，角色分配 |
| 🎯 **RBAC 权限** | 用户 → 角色 → 权限三层模型，19 项细粒度权限码，树形权限勾选 |
| 📋 **动态菜单** | 数据库驱动菜单树，按权限自动过滤，顶栏搜索（键盘导航），外部链接支持 |
| 🔑 **密码重置** | 邮箱验证码（6位），SMTP/MailKit，5分钟有效期 |

### 业务功能

| 模块 | 说明 |
|------|------|
| 📦 **入库单管理** | 完整 CRUD + 明细行，自动编码（RK-YYYYMMDD-NNNN），含税金额自动计算，CSV 导出，日统计 |
| 📎 **附件管理** | 统一附件表，MinIO 对象存储，按模块关联，上传/下载/删除 |
| 🏷️ **材料字典** | 500 条种子材料，10 大分类，关键字搜索，批量删除 |

### 高级功能

| 模块 | 说明 |
|------|------|
| ⚙️ **集成平台** | 低代码数据同步，5 种认证方式（None/Basic/Bearer/ApiKey/Chain），Pull→Transform→Push 流水线，字段映射，C# 脚本处理器，执行日志 |
| ⏰ **计划任务** | Cron 定时调度（10 秒轮询），一次性任务，4 个内置处理器，C# 脚本/反射处理器，手动触发 |
| 🗄️ **数据库管理** | 5 种外部数据库（MySQL/SQL Server/SQLite/Oracle/PostgreSQL），AES 密码加密，表结构浏览，只读 SQL 执行 |
| 🔗 **SSO 单点登录** | 魔法链接（64位随机令牌），8位授权码，Redis 缓存验证 |
| 🔒 **OAuth 2.0 / OIDC** | 授权码流程 + PKCE S256，RS256 签名 id_token，Refresh Token 轮转，OIDC Discovery，JWKS 端点，多客户端管理 |
| 🏠 **主页仪表盘** | 可配置卡片网格，4 种卡片（天气/图表/统计/时钟），拖拽布局，用户级持久化 |
| 🌤️ **天气组件** | IP 自动定位（3 服务回退），wttr.in 天气数据，温湿度/风速/体感温度 |

---

## 预置数据

项目首次启动自动创建：

### 内置角色与权限

| 角色 | 权限数 | 说明 |
|------|--------|------|
| **超级管理员** | 19 项全部 | 可管理所有资源、分配角色 |
| 普通用户 | 2 项 | `user:view` + `system:config` |
| 只读用户 | 1 项 | 仅 `user:view` |

### 完整权限码

```
user:view, user:create, user:edit, user:delete, user:freeze, user:reset_password,
role:manage, role:assign_permission, system:config,
material:view, material:manage,
inbound:view, inbound:manage,
sso:manage, oauth:manage, home:config, attachment:manage,
database:manage, integration:manage
```

### 内置菜单

| 一级菜单 | 子菜单 |
|----------|--------|
| 主页 | 仪表盘、个人信息 |
| 系统管理 | 用户列表、角色管理、材料字典、入库单、SSO 管理、OAuth 客户端、主页配置、附件管理、数据库管理、集成平台、计划任务、系统配置 |
| 接口文档 | Swagger API 文档 |
| 帮助中心 | VitePress 文档站点 |

### 内置账号

| 用户名 | 密码 | 角色 |
|--------|------|------|
| `admin` | `password` | 超级管理员 |

### OAuth 客户端

| ClientId | 名称 | 授权类型 | PKCE |
|----------|------|----------|------|
| `vue-spa` | Vue 前端应用 | authorization_code | ✅ S256 |

### 种子数据

- **500 条材料数据**（10 大分类，随机生成固定种子，支持中英文名称）
- 每次启动增量添加新权限码和菜单项，不影响已有数据

---

## 快速启动

### 前置要求

- **.NET SDK 10.0**+
- **Node.js 18+**
- **Docker Desktop**（用于 Redis + MinIO + Nginx）

### 一键启动（Windows）

```bat
# 双击运行
start.bat
```

自动启动 Docker 容器 → 后端 (5000) → 前端 (5173) → 文档 (5174)。

### 手动启动

```bash
# 1. 启动基础设施（Redis + MinIO + Nginx）
docker compose up -d

# 2. 安装前端依赖
cd frontend && npm install && cd ..

# 3. 启动后端
cd backend && dotnet run

# 4. 启动前端（新终端）
cd frontend && npm run dev

# 5. 启动文档（新终端）
npx vitepress dev docs --port 5174
```

### 访问地址

| 服务 | 地址 |
|------|------|
| 前端应用 | http://localhost:5173 |
| 后端 API | http://localhost:5000 |
| Swagger 文档 | http://localhost:5000/swagger |
| VitePress 文档 | http://localhost:5174 |
| MinIO 控制台 | http://localhost:9001 |
| Nginx 反向代理 | http://localhost:8080 |

### 停止服务

```bat
# 双击运行
stop.bat
```

---

## 基础设施服务

`docker-compose.yml` 提供 3 个服务：

| 服务 | 镜像 | 端口 | 用途 |
|------|------|------|------|
| **Redis** | redis:alpine | 6379 | Token 缓存、在线状态、SSO 令牌 |
| **MinIO** | minio/minio:latest | 9000 / 9001 | 附件对象存储 |
| **Nginx** | nginx:alpine | 8080:80 | 反向代理（前端 + API） |

MinIO 默认凭证：`minioadmin` / `minioadmin`

---

## 环境变量

| 变量 | 说明 | 默认值 |
|------|------|--------|
| `ConnectionStrings__DefaultConnection` | SQLite 路径 | `Data Source=auth.db` |
| `ConnectionStrings__Redis` | Redis 连接 | `localhost:6379` |
| `Jwt__Key` | JWT 签名密钥（≥32字符） | 内置开发密钥 |
| `Jwt__Issuer` | JWT 签发者 | `AuthApi` |
| `Jwt__Audience` | JWT 受众 | `AuthApp` |
| `Minio__Endpoint` | MinIO 地址 | `localhost:9000` |
| `Minio__AccessKey` | MinIO Access Key | `minioadmin` |
| `Minio__SecretKey` | MinIO Secret Key | `minioadmin` |
| `Minio__BucketName` | MinIO 桶名称 | `attachments` |
| `Smtp__Host` | SMTP 服务器 | `smtp.163.com` |
| `Smtp__Port` | SMTP 端口 | `465` |
| `Smtp__Username` | SMTP 用户名 | — |
| `Smtp__Password` | SMTP 密码 | — |

---

## 第三方模拟 API

`MockThirdPartyApi/` 是一个独立的 ASP.NET Core Minimal API（端口 5100），模拟外部 ERP 系统，用于集成平台测试：

| 端点 | 说明 |
|------|------|
| `POST /api/auth/login` | 认证（admin/password） |
| `POST /api/inbound-orders` | 接收入库单数据 |
| `POST /ierp/api/getAppToken.do` | ERP AppToken 获取 |
| `POST /ierp/api/login.do` | ERP AccessToken 交换 |
| `POST /ierp/kapi/v2/ctgp/basedata/queryMaterials` | 物料查询（分页） |
| `POST /ierp/kapi/v2/ctgp/pssc/pm_requirapplybill/sf_ST_im_reqapplication` | 采购单同步 |

## SSO 测试客户端

`SsoTestClient/` 是 .NET 控制台程序，完整测试 OAuth 2.0 + OIDC 流程：

1. 生成 PKCE 参数（CodeVerifier + S256 Challenge）
2. 构建授权 URL → 打开浏览器
3. HTTP Listener 捕获回调（端口 18080）
4. Code 换 Token + 解析 id_token
5. 调用 UserInfo + Refresh Token
6. OIDC Discovery + JWKS 验证

```bash
cd SsoTestClient && dotnet run
```

---

## 并发性能

测试环境：Windows 11 + .NET 10 + SQLite WAL + Redis (Docker)

### 读操作

| 并发数 | 总耗时 | 成功率 | 平均响应 |
|--------|--------|--------|----------|
| 10 | 305ms | 100% | 30ms |
| 100 | 1,326ms | 100% | 13ms |
| 500 | 5,535ms | 100% | 11ms |
| 2,000 | 24,105ms | 100% | 12ms |

### 写操作

| 并发数 | 总耗时 | 成功率 | 平均响应 |
|--------|--------|--------|----------|
| 200 | 3,088ms | 100% | 15ms |
| 500 | 7,446ms | 100% | 15ms |

### 瓶颈分析

```
Kestrel → ASP.NET Core → EF Core → SQLite (读) / Redis (缓存)

• Kestrel — 无连接数限制，仅受系统资源约束
• Redis — Token 缓存 + 在线状态，有效降低 DB 压力
• SQLite — WAL 模式下读可并发，写串行排队
• JWT + BCrypt — CPU 密集型，登录接口主要耗时来源

实测结论：
• 读写混合 500~1000 并发无压力，2000 读并发全通过
• 瓶颈在 SQLite 串行写入，写 >1000 时延迟线性增长
• 生产级高写入 → 建议迁移 PostgreSQL / SQL Server
• 更高读并发 → 加入 Response Caching 中间件
```

---

## 部署

### Windows

```powershell
# 生产构建
cd frontend && npm run build
cd backend && dotnet publish -c Release -o ../publish
# 使用 Nginx 或 IIS 部署前端 dist/，后端通过 dotnet publish/backend.dll 运行
```

### Linux (systemd)

```bash
# 后端服务
sudo vi /etc/systemd/system/auth-backend.service
# [Service] WorkingDirectory=/opt/demo/backend
# [Service] ExecStart=/usr/bin/dotnet run --urls "http://0.0.0.0:5000"

sudo systemctl daemon-reload && sudo systemctl enable --now auth-backend

# 前端：构建后 Nginx 部署
cd frontend && npm run build
sudo cp -r dist/* /var/www/html/
```

### Nginx 生产配置

```nginx
server {
    listen 80;
    server_name your-domain.com;
    root /var/www/html;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location /api/ {
        proxy_pass http://127.0.0.1:5000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
}
```

---

## 常见问题

**Q: 后端启动后报 Redis 连接失败？**
Redis 非必须。未安装时 Token 缓存 / 在线状态 / SSO 功能不可用，但基础登录认证正常。

**Q: 如何重置数据库？**
删除 `backend/auth.db`，重启后端自动重建并填充种子数据。

**Q: admin 密码如何修改？**
登录后进入个人信息页面修改，或在 `SeedData.cs` 中修改后重建数据库。

**Q: 前端访问 Nginx (8080) 时报 502？**
确保后端在 5000 端口运行，前端在 5173 端口运行。Nginx 通过 `host.docker.internal` 访问宿主机。

**Q: 如何添加自定义计划任务处理器？**
实现 `IScheduledTaskHandler` 接口，放置于 `ScheduledTaskHandlers/` 目录，系统自动发现。

**Q: jenkins地址？**
http://localhost:8090/。