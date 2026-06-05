pipeline {
    agent any

    stages {

        stage('Build & Deploy') {
            steps {
                script {
                    sh '''
                        echo "=== Pre-cleanup (keep redis/minio) ==="
                        docker ps -a --filter "name=auth-" --format "{{.Names}}" | grep -v redis | grep -v minio | xargs -r docker stop 2>/dev/null || true
                        docker ps -a --filter "name=auth-" --format "{{.Names}}" | grep -v redis | grep -v minio | xargs -r docker rm 2>/dev/null || true

                        echo "=== Building Backend ==="
                        docker build -t auth-backend -f /var/jenkins_home/workspace/demo/backend/Dockerfile /var/jenkins_home/workspace/demo/backend/

                        echo "=== Building Frontend ==="
                        docker build -t auth-frontend -f /var/jenkins_home/workspace/demo/frontend/Dockerfile /var/jenkins_home/workspace/demo/frontend/

                        echo "=== Creating Network ==="
                        docker network create auth-net 2>/dev/null || true

                        echo "=== Starting Redis (if not running) ==="
                        docker start auth-redis 2>/dev/null || \
                          docker run -d --name auth-redis --network auth-net redis:alpine

                        echo "=== Starting Minio (if not running) ==="
                        docker start auth-minio 2>/dev/null || \
                          docker run -d --name auth-minio --network auth-net \
                            -e MINIO_ROOT_USER=minioadmin \
                            -e MINIO_ROOT_PASSWORD=minioadmin \
                            -v minio-data:/data \
                            minio/minio:latest server /data --console-address ":9001"

                        echo "=== Starting Backend ==="
                        docker run -d --name auth-backend --network auth-net \
                            -p 5000:5000 \
                            -v auth-backend-data:/app/data \
                            -e ASPNETCORE_ENVIRONMENT=Production \
                            -e "ConnectionStrings__DefaultConnection=Data Source=/app/data/auth.db" \
                            -e "ConnectionStrings__Redis=auth-redis:6379" \
                            -e "Jwt__Key=ThisIsAVeryLongSecretKeyForJwtAuth2024!AtLeast256Bits!" \
                            -e "Jwt__Issuer=AuthApi" \
                            -e "Jwt__Audience=AuthApp" \
                            -e "Minio__Endpoint=auth-minio:9000" \
                            -e "Minio__AccessKey=minioadmin" \
                            -e "Minio__SecretKey=minioadmin" \
                            auth-backend

                        echo "=== Starting Frontend ==="
                        docker run -d --name auth-frontend --network auth-net \
                            auth-frontend

                        echo "=== Building Nginx ==="
                        docker build -t auth-nginx -f /var/jenkins_home/workspace/demo/nginx/Dockerfile /var/jenkins_home/workspace/demo/nginx/

                        echo "=== Starting Nginx (Reverse Proxy) ==="
                        docker run -d --name auth-nginx --network auth-net \
                            -p 8080:80 \
                            auth-nginx

                        echo "=== Waiting for services ==="
                        sleep 5
                        docker ps --filter "name=auth-" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
                    '''.stripIndent()
                }
            }
        }
    }

    post {
        success {
            echo '''
                ====== 部署成功 ======
                系统入口: http://localhost:8080
                Swagger:  http://localhost:8080/swagger
            '''.stripIndent()
        }
        failure {
            echo '部署失败，请检查日志。'
        }
    }
}
