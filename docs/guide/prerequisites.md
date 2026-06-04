# 环境要求

## 必备环境

| 工具 | 版本要求 | 用途 |
|------|----------|------|
| .NET SDK | 10.0 或更高 | 后端运行时 |
| Node.js | 18+ | 前端构建与开发 |
| Docker | 20+ | Redis、MinIO、Nginx 容器 |
| Redis | 6+（可选） | Token 缓存 |

## 安装 .NET SDK

从 [dotnet.microsoft.com](https://dotnet.microsoft.com/download) 下载安装。

```bash
# 验证安装
dotnet --version  # 应显示 10.0.xxx
```

## 安装 Node.js

从 [nodejs.org](https://nodejs.org/) 下载安装 LTS 版本。

```bash
# 验证安装
node --version   # 应显示 v18+ 或 v20+
npm --version    # 应显示 9+
```

## 安装 Docker

从 [docker.com](https://www.docker.com/products/docker-desktop/) 下载 Docker Desktop。

```bash
# 验证安装
docker --version
docker compose version
```

## Docker 基础设施

项目使用 Docker Compose 管理 3 个基础服务：

| 服务 | 端口 | 说明 |
|------|------|------|
| Redis | 6379 | Token 缓存、在线状态、SSO 令牌 |
| MinIO | 9000/9001 | 对象存储（9000: API, 9001: 控制台） |
| Nginx | 8080 | 反向代理（前端 :5173 + API :5000） |

```bash
# 启动所有基础设施
docker compose up -d

# 查看运行状态
docker compose ps

# 停止
docker compose down
```

## 可选：本地 Redis

不依赖 Docker 时可直接安装 Redis：

- **Windows**：[tporadowski/redis](https://github.com/tporadowski/redis/releases)
- **Linux**：`sudo apt install redis-server`
- **Docker**：`docker run -d --name redis -p 6379:6379 redis:alpine`

> Redis 非必须。未安装时 Token 缓存、在线状态、SSO 令牌等功能不可用，但基础登录认证仍可正常工作。
