# 部署运行

## 开发环境部署

### 1. 克隆并安装依赖

```bash
cd demo
cd frontend && npm install && cd ..
```

### 2. 启动 Docker 基础设施

```bash
docker compose up -d
```

启动 Redis（`:6379`）、MinIO（`:9000/:9001`）、Nginx（`:8080`）。

MinIO 默认凭证：`minioadmin` / `minioadmin`

### 3. 启动后端

```bash
cd backend
dotnet run
```

后端监听 `http://localhost:5000`，首次运行自动创建 SQLite 数据库和种子数据（含 500 条材料）。

### 4. 启动前端

```bash
cd frontend
npm run dev
```

前端监听 `http://localhost:5173`，API 请求自动代理到后端。

### 5. 启动文档

```bash
npx vitepress dev docs --port 5174
```

文档站点监听 `http://localhost:5174`。

### 6. 一键启动（Windows）

```bat
start.bat
```

自动启动 Docker → 后端 → 前端 → 文档。

## 访问地址

| 服务 | 地址 | 说明 |
|------|------|------|
| 前端 | http://localhost:5173 | Vue 3 应用 |
| 后端 | http://localhost:5000 | ASP.NET Core API |
| Swagger | http://localhost:5000/swagger | 接口文档 |
| MinIO | http://localhost:9001 | 对象存储控制台 |
| VitePress | http://localhost:5174 | 本文档 |
| Nginx 代理 | http://localhost:8080 | 反向代理 |

## 生产部署

### 前端构建

```bash
cd frontend && npm run build
# 产出 dist/ 目录，部署到 Nginx 等静态服务器
```

### 后端发布

```bash
cd backend
dotnet publish -c Release -o ../publish
# 运行: dotnet publish/backend.dll
```

### Nginx 配置示例

```nginx
server {
    listen 80;
    server_name your-domain.com;

    root /var/www/html;
    index index.html;
    location / { try_files $uri $uri/ /index.html; }

    location /api/ {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
}
```

### Linux systemd 后端服务

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

### Docker 生产部署

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

> 生产环境请务必修改 `Jwt:Key` 为强随机字符串。
