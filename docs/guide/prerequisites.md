# 环境要求

## 必备环境

| 工具 | 版本要求 | 用途 |
|------|----------|------|
| .NET SDK | 10.0 或更高 | 后端运行时 |
| Node.js | 18+ | 前端构建 |
| Docker | 20+ | Redis、MinIO、Nginx 容器 |
| Redis | 6+ | Token 缓存（可选） |

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

## 可选：Redis

- **Windows**：[tporadowski/redis](https://github.com/tporadowski/redis/releases)
- **Docker**：`docker run -d --name redis -p 6379:6379 redis:alpine`

> Redis 非必须。未安装时 Token 缓存不可用，但登录认证仍可正常工作。
