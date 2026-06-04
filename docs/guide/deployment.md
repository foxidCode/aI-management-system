# 部署运行

## 开发环境部署

### 1. 克隆并安装依赖

```bash
cd demo
cd frontend && npm install && cd ..
```

### 2. 启动 Docker 服务

```bash
docker compose up -d
```

启动 Redis（`:6379`）、MinIO（`:9000/:9001`）、Nginx（`:8080`）。

### 3. 启动后端

```bash
cd backend
dotnet run
```

后端监听 `http://localhost:5000`，首次运行自动创建 SQLite 数据库和种子数据。

### 4. 启动前端

```bash
cd frontend
npm run dev
```

前端监听 `http://localhost:5173`，API 请求自动代理到后端。

### 5. 一键启动（Windows）

```bash
start.bat
```

## 访问地址

| 服务 | 地址 | 说明 |
|------|------|------|
| 前端 | http://localhost:5173 | Vue 3 应用 |
| 后端 | http://localhost:5000 | ASP.NET Core API |
| Swagger | http://localhost:5000/swagger | 接口文档 |
| MinIO | http://localhost:9001 | 对象存储控制台 |
| VitePress | http://localhost:5174 | 本文档 |

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
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

## 环境变量

| 变量 | 说明 | 默认值 |
|------|------|--------|
| `ConnectionStrings__DefaultConnection` | SQLite 路径 | `Data Source=auth.db` |
| `ConnectionStrings__Redis` | Redis 连接 | `localhost:6379` |
| `Jwt__Key` | JWT 签名密钥 | 内置开发密钥 |
