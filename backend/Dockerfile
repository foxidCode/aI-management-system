# ====== 构建阶段 ======
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 还原依赖（利用层缓存）
COPY backend.csproj .
RUN dotnet restore

# 复制源码并构建
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

# ====== 运行阶段 ======
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 安装 curl 用于健康检查
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

# 创建数据目录（SQLite 持久化）
RUN mkdir -p /app/data && chmod 777 /app/data
VOLUME ["/app/data"]

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/auth.db"

EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:5000/swagger/index.html || exit 1

ENTRYPOINT ["dotnet", "backend.dll"]
