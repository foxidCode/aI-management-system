# 权限管理系统 (Permission Management System)

基于 **ASP.NET Core 10** + **Vue 3** + **Element Plus** 的全栈权限管理系统，支持用户认证、角色管理、菜单权限控制。

## 技术栈

| 层级 | 技术 | 版本 |
|------|------|------|
| 后端框架 | ASP.NET Core | .NET 10.0 |
| ORM | Entity Framework Core (SQLite) | 10.0.8 |
| 认证 | JWT Bearer + BCrypt | — |
| 缓存 | Redis (StackExchange.Redis) | 2.13.17 |
| 前端框架 | Vue 3 (Composition API) | ^3.5.34 |
| UI 组件 | Element Plus | ^2.14.1 |
| 构建工具 | Vite | ^8.0.12 |
| HTTP 客户端 | Axios | ^1.16.1 |

## 项目结构

```
demo/
├── backend/                    # ASP.NET Core 后端
│   ├── Controllers/            # API 控制器
│   │   ├── AuthController.cs       # 登录/注册
│   │   ├── UserController.cs       # 用户信息
│   │   ├── UserManagementController.cs  # 用户管理 CRUD + 角色分配
│   │   └── RoleController.cs       # 角色/权限/菜单管理
│   ├── Models/                 # 数据模型
│   │   ├── User.cs                 # 用户
│   │   ├── Permission.cs           # 权限 + 角色 + 关联表
│   │   └── Menu.cs                 # 菜单
│   ├── DTOs/                   # 数据传输对象
│   ├── Services/               # 业务逻辑
│   │   ├── AuthService.cs          # 认证服务
│   │   └── PermissionService.cs    # 权限管理服务
│   ├── Data/                   # 数据库上下文
│   ├── SeedData.cs             # 种子数据
│   ├── Program.cs              # 应用入口
│   └── appsettings.json        # 配置文件
├── frontend/                   # Vue 3 前端
│   └── src/
│       ├── api/auth.js             # API 封装
│       ├── router/index.js         # 路由配置
│       └── views/                  # 页面组件
│           ├── Login.vue               # 登录页
│           ├── Register.vue            # 注册页
│           ├── Dashboard.vue           # 主布局（动态菜单）
│           ├── UserProfile.vue         # 个人信息
│           ├── UserManagement.vue      # 用户管理
│           └── RoleManagement.vue      # 角色管理
├── docker-compose.yml          # Docker 编排（Redis + Nginx）
└── nginx.conf                  # Nginx 反向代理配置
```

## 功能特性

- **用户认证**：JWT 登录/注册，BCrypt 密码哈希，Redis Token 缓存
- **权限管理**：基于 RBAC 模型，用户 → 角色 → 权限 三层控制
- **动态菜单**：根据用户权限自动过滤可见菜单
- **内置管理员**：`admin / password` 账号，不可冻结，唯一可管理角色
- **角色管理**：树形权限分配，父子级联勾选
- **用户管理**：分页/搜索/排序，冻结/解冻，批量操作，角色分配

## 预置数据

项目首次启动时自动创建：

| 角色 | 权限 |
|------|------|
| **超级管理员** | 全部 9 项权限 |
| 普通用户 | 用户查看、系统配置 |
| 只读用户 | 用户查看 |

| 内置账号 | 用户名 | 密码 |
|----------|--------|------|
| 管理员 | `admin` | `password` |

---

## 部署步骤

### 前置要求

- **.NET SDK 10.0** 或更高版本
- **Node.js 18+**（含 npm）
- **Redis**（可选，用于 Token 缓存；不使用也可运行，但 Token 缓存功能不可用）

---

### Windows 环境

#### 1. 安装依赖

```powershell
# 安装 .NET SDK 10.0
# 下载地址：https://dotnet.microsoft.com/download

# 安装 Node.js 18+
# 下载地址：https://nodejs.org/

# 安装 Redis（可选）
# 方式一：下载 Windows 版 Redis https://github.com/tporadowski/redis/releases
# 方式二：使用 Docker（见下方 Docker 部署）
```

#### 2. 克隆项目并安装前端依赖

```powershell
cd demo

# 安装前端依赖
cd frontend
npm install
cd ..
```

#### 3. 配置

后端配置文件 `backend/appsettings.json`：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=auth.db",
    "Redis": "localhost:6379"
  },
  "Jwt": {
    "Key": "你的密钥（至少32字符）",
    "Issuer": "AuthApi",
    "Audience": "AuthApp"
  }
}
```

> 生产环境请务必修改 `Jwt:Key` 为强随机字符串。

#### 4. 启动 Redis（可选）

```powershell
# 如果使用 Windows 版 Redis，直接双击 redis-server.exe
# 或使用 Docker：
docker run -d --name redis -p 6379:6379 redis:alpine
```

#### 5. 启动后端

```powershell
cd backend
dotnet run
```

后端将监听 `http://localhost:5000`，首次运行自动创建 SQLite 数据库和种子数据。

#### 6. 启动前端（开发模式）

```powershell
cd frontend
npm run dev
```

前端将监听 `http://localhost:5173`，API 请求自动代理到后端 `localhost:5000`。

#### 7. 访问

打开浏览器访问 `http://localhost:5173`，使用 `admin / password` 登录。

#### 8. 生产构建

```powershell
# 构建前端
cd frontend
npm run build

# 后端发布
cd backend
dotnet publish -c Release -o ../publish

# 使用 Nginx 或 IIS 部署前端静态文件（dist 目录）
# 后端通过 dotnet publish/backend.dll 运行
```

---

### Linux 环境 (Ubuntu/Debian)

#### 1. 安装 .NET SDK

```bash
# 添加 Microsoft 包源
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# 安装 .NET SDK
sudo apt update
sudo apt install -y dotnet-sdk-10.0
```

#### 2. 安装 Node.js

```bash
# 使用 NodeSource 安装 Node.js 20
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
sudo apt install -y nodejs

# 或使用 nvm
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.40.1/install.sh | bash
nvm install 20
```

#### 3. 安装 Redis

```bash
sudo apt update
sudo apt install -y redis-server
sudo systemctl enable redis-server
sudo systemctl start redis-server
```

#### 4. 安装前端依赖

```bash
cd demo/frontend
npm install
cd ..
```

#### 5. 启动后端

```bash
cd demo/backend
# 后台运行
nohup dotnet run --urls "http://0.0.0.0:5000" > /dev/null 2>&1 &
```

#### 6. 启动前端（开发模式）

```bash
cd demo/frontend
nohup npm run dev > /dev/null 2>&1 &
```

访问 `http://<服务器IP>:5173`。

#### 7. 生产部署（使用 systemd）

创建后端服务 `/etc/systemd/system/auth-backend.service`：

```ini
[Unit]
Description=Auth Backend Service
After=network.target

[Service]
WorkingDirectory=/opt/demo/backend
ExecStart=/usr/bin/dotnet run --urls "http://0.0.0.0:5000"
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable auth-backend
sudo systemctl start auth-backend
```

前端使用 Nginx 部署：

```bash
# 构建前端
cd demo/frontend
npm run build

# 配置 Nginx
sudo cp -r dist/* /var/www/html/

# Nginx 配置 /etc/nginx/sites-available/auth
# 见下方 Nginx 配置章节
sudo ln -s /etc/nginx/sites-available/auth /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

---

### Docker 部署

项目包含 `docker-compose.yml`，可一键启动 Redis + Nginx：

```bash
# 启动 Redis 和 Nginx
docker compose up -d

# 然后手动启动后端和前端（开发模式）
cd backend && dotnet run &
cd frontend && npm run dev &
```

完整的生产 Docker 部署（需添加 Dockerfile）：

```dockerfile
# backend/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 5000
ENTRYPOINT ["dotnet", "backend.dll"]
```

```dockerfile
# frontend/Dockerfile
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/nginx.conf
EXPOSE 80
```

---

### Nginx 生产配置

`/etc/nginx/sites-available/auth`：

```nginx
server {
    listen 80;
    server_name your-domain.com;

    # 前端静态文件
    root /var/www/html;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    # 后端 API 代理
    location /api/ {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
}
```

---

## 环境变量

| 变量 | 说明 | 默认值 |
|------|------|--------|
| `ConnectionStrings__DefaultConnection` | SQLite 数据库路径 | `Data Source=auth.db` |
| `ConnectionStrings__Redis` | Redis 连接字符串 | `localhost:6379` |
| `Jwt__Key` | JWT 签名密钥（至少32字符） | 内置开发密钥 |
| `Jwt__Issuer` | JWT 签发者 | `AuthApi` |
| `Jwt__Audience` | JWT 受众 | `AuthApp` |

## 并发性能测试

测试环境：Windows 11 + .NET 10 + SQLite + Redis (Docker)，使用 `curl` 并行请求模拟并发。

### 读操作（登录接口 — BCrypt 验密 + JWT 签发 + Redis 缓存）

| 并发数 | 总耗时 | 成功率 | 平均响应 |
|--------|--------|--------|----------|
| 10 | 305ms | 100% | 30ms |
| 50 | 716ms | 100% | 14ms |
| 100 | 1,326ms | 100% | 13ms |
| 200 | 2,508ms | 100% | 12ms |
| 500 | 5,535ms | 100% | 11ms |
| 2,000 | 24,105ms | 100% | 12ms |

### 读操作（用户列表 — SQLite 多表联查）

| 并发数 | 总耗时 | 成功率 | 平均响应 |
|--------|--------|--------|----------|
| 100 | 987ms | 100% | 5ms |

### 写操作（创建用户 — SQLite INSERT + 角色分配）

| 并发数 | 总耗时 | 成功率 | 平均响应 |
|--------|--------|--------|----------|
| 200 | 3,088ms | 100% | 15ms |
| 500 | 7,446ms | 100% | 15ms |

### 瓶颈分析

```
当前架构: Kestrel → ASP.NET Core → EF Core → SQLite (读) / Redis (缓存)

• Kestrel      — 默认无连接数限制，仅受系统资源约束
• Redis        — Token 验证缓存，有效降低重复认证的 DB 压力
• SQLite       — 仅支持单写者（WAL 模式下读可并发），EF Core 自动排队串行写入
• JWT + BCrypt — CPU 密集型操作，登录接口主要的单次耗时来源

实测结论:
• 读写混合 500~1000 并发无压力，2,000 读并发全通过
• 瓶颈在 SQLite 串行写入，写 >1,000 时延迟线性增长
• 如需生产级高写入 → 推荐迁移到 PostgreSQL / SQL Server
• 如需更高读并发 → 加入 Response Caching 中间件
```

## 常见问题

**Q: 后端启动后前端请求报 502/ECONNREFUSED？**

检查后端是否在 `http://localhost:5000` 启动，前端 `vite.config.js` 中的代理目标是否正确。

**Q: Redis 连接失败？**

Redis 非必须。如未安装 Redis，Token 缓存功能不可用，但登录认证仍可正常工作。安装 Redis 或配置正确的连接字符串即可。

**Q: 数据库如何重置？**

删除 `backend/auth.db` 文件，重启后端即可自动重建并填充种子数据。

**Q: 如何修改 admin 密码？**

登录后进入"修改用户信息"页面修改；或在 `SeedData.cs` 中修改后删除数据库重建。
