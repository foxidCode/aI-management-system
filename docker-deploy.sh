#!/bin/bash
# Jenkins CI/CD 部署脚本（由 Jenkinsfile 调用）

set -e
cd "$(dirname "$0")"

echo "=== Building Docker Images ==="

echo "[1/3] Building backend..."
docker build -t auth-backend:latest -f backend/Dockerfile backend/

echo "[2/3] Building frontend..."
docker build -t auth-frontend:latest -f frontend/Dockerfile frontend/

echo "[3/3] Restarting services..."
docker compose down 2>/dev/null || true
docker compose up -d

echo ""
echo "=== Deployment Complete ==="
docker compose ps
echo ""
echo "Frontend: http://localhost:8080"
echo "Swagger:  http://localhost:5000/swagger"
