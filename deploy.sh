#!/bin/bash
# 一键部署脚本
set -e

echo "========================================="
echo "  管理系统 - Docker Compose 部署"
echo "========================================="

# 检查 .env 文件
if [ ! -f .env ]; then
    echo "[WARN] .env 文件不存在，复制 .env.example ..."
    cp .env.example .env
    echo "[INFO] 请编辑 .env 文件，修改 JWT_KEY 等敏感配置"
fi

# 拉取最新代码
echo ""
echo "[1/4] 拉取最新代码..."
git pull origin main 2>/dev/null || echo "  (跳过，可能不在 git 仓库中)"

# 停止旧容器
echo ""
echo "[2/4] 停止旧容器..."
docker-compose down

# 构建并启动
echo ""
echo "[3/4] 构建镜像并启动服务..."
docker-compose up -d --build

# 等待启动
echo ""
echo "[4/4] 等待服务就绪..."
sleep 5

# 健康检查
echo ""
echo "检查服务状态..."
docker-compose ps

echo ""
echo "========================================="
echo "  部署完成！"
echo "  前端: http://localhost:8080"
echo "  Swagger: http://localhost:5000/swagger"
echo "========================================="
