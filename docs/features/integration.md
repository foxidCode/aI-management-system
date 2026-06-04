# 集成平台

## 功能

低代码数据集成平台，可配置外部 API 连接，定义数据同步任务，支持字段映射和自定义代码处理。

## 架构

```
IntegrationConnection (连接配置: URL、认证)
  → IntegrationTask (同步任务: 来源/目标、映射)
    → IntegrationLog (执行日志)
```

## 认证方式

| 类型 | 说明 |
|------|------|
| None | 无认证 |
| Basic | 用户名密码 |
| Bearer | Bearer Token |
| ApiKey | API Key |
| Chain | 链式认证（多步请求提取 Token） |

## 任务配置

- **来源**：外部 API 路径、请求方法、请求体、响应数据提取路径
- **目标**：外部 API 推送 或 本系统数据库写入
- **字段映射**：JSON 数组配置来源→目标字段对应
- **自定义代码**：C# 脚本处理复杂转换逻辑

## 事件钩子

- **BeforeExecute**：拉取数据前执行的 C# 代码
- **AfterExecute**：推送数据后执行的 C# 代码

## API 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/integration/connections` | 连接列表 |
| POST | `/api/integration/connections` | 创建连接 |
| POST | `/api/integration/connections/test` | 测试连接 |
| GET | `/api/integration/tasks` | 任务列表 |
| POST | `/api/integration/tasks/{id}/execute` | 手动执行 |
| GET | `/api/integration/logs` | 执行日志 |
