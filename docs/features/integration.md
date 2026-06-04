# 集成平台

## 功能

低代码数据集成平台，可配置外部 API 连接，定义数据同步任务，支持字段映射和自定义代码处理。适用于从外部系统拉取数据、转换后写入本地或推送至其他系统。

## 架构

```
IntegrationConnection (连接配置: BaseUrl、认证)
  → IntegrationTask (同步任务: 来源→目标、映射)
    → IntegrationLog (执行日志)
```

## 认证方式

| 类型 | 说明 | 配置 |
|------|------|------|
| None | 无认证 | — |
| Basic | HTTP Basic Auth | 用户名 + 密码 |
| Bearer | Bearer Token | Token + 登录接口（自动刷新） |
| ApiKey | API Key | Header 名称 + Key 值 |
| Chain | 链式认证 | 多步请求提取 Token（支持 JSONPath 提取） |

Bearer 模式支持自动登录获取 Token 并缓存，Token 过期前自动续期。

## 任务配置

### 数据来源 (Source)
- **请求方式**：GET / POST
- **请求体**：JSON（POST 时）
- **响应路径**：JSONPath 提取数据数组（如 `$.data.items`）

### 数据目标 (Target)

| 目标类型 | 说明 |
|----------|------|
| Api | 推送至外部 API（配置 URL + 连接） |
| Database | 写入本地 SQLite（自动建表、INSERT OR REPLACE） |

Database 模式下自动添加 `CreateTime`、`UpdateTime`、`SyncId` 字段。支持子表配置（一对多嵌套数据同步）。

### 字段映射

JSON 数组配置来源→目标字段对应：

```json
[
  { "sourceField": "materialNumber", "targetField": "MaterialCode" },
  { "sourceField": "materialName", "targetField": "MaterialName" },
  { "sourceField": "unit", "targetField": "Unit" }
]
```

### 自定义代码

C# 脚本处理器，运行时编译执行，可用于复杂数据转换。通过 `Microsoft.CodeAnalysis.CSharp.Scripting` 实现。

## 事件钩子

- **BeforeExecute**：拉取数据前执行的 C# 代码
- **AfterExecute**：推送数据后执行的 C# 代码

## API 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/integration/connections` | 连接列表 |
| POST | `/api/integration/connections` | 创建连接 |
| PUT | `/api/integration/connections/{id}` | 编辑连接 |
| DELETE | `/api/integration/connections/{id}` | 删除连接 |
| POST | `/api/integration/connections/test` | 测试连接 |
| GET | `/api/integration/tasks` | 任务列表 |
| POST | `/api/integration/tasks` | 创建任务 |
| PUT | `/api/integration/tasks/{id}` | 编辑任务 |
| DELETE | `/api/integration/tasks/{id}` | 删除任务 |
| POST | `/api/integration/tasks/{id}/execute` | 手动执行 |
| GET | `/api/integration/logs` | 执行日志（分页） |

## 执行流程

```
1. BeforeExecute 钩子（如有）
2. 拉取数据 → 来源 API 请求
3. 数据提取（JSONPath）
4. 字段映射 → 转为本地方案格式
5. 自定义代码处理（如有）
6. 推送数据 → 目标 API 或 本地数据库写入
7. AfterExecute 钩子（如有）
8. 记录执行日志（请求/响应/耗时/状态）
```

## 测试工具

项目包含 `MockThirdPartyApi/`（端口 5100），模拟外部 ERP 系统：

| 端点 | 说明 |
|------|------|
| `POST /ierp/api/getAppToken.do` | AppToken 获取 |
| `POST /ierp/api/login.do` | AccessToken 交换 |
| `POST /ierp/kapi/v2/ctgp/basedata/queryMaterials` | 物料查询（分页） |
| `POST /ierp/kapi/v2/ctgp/pssc/pm_requirapplybill/sf_ST_im_reqapplication` | 采购单同步 |

```bash
cd MockThirdPartyApi && dotnet run
```
