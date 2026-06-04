# 计划任务

## 功能

基于 Cron 表达式的定时任务调度，后台服务每 10 秒轮询。支持三种执行方式，参数化配置，执行日志追踪。

## 执行方式

| 方式 | 说明 | 示例 |
|------|------|------|
| 关联集成任务 | 定时触发集成平台的同步任务 | 每小时同步物料数据 |
| 自定义代码 (CodeHandler) | 运行时编译 C# 代码执行 | 自动创建测试用户 |
| 执行器类 (HandlerClass) | 反射调用 IScheduledTaskHandler 实现 | 清理日志、统计导出 |

## 内置处理器

| 类 | 功能 | 参数 |
|------|------|------|
| `CreateUserHandler` | 自动创建用户（前缀+时间戳+随机后缀） | `prefix`, `password`, `role` |
| `CleanupLogsHandler` | 清理旧 IntegrationLogs | `days`（默认30） |
| `DataExportHandler` | 导出表记录到日志 | `table`, `topN`, `minDate` |
| `MaterialStatsHandler` | 系统数据统计 | `includeDetail` |

## 自定义处理器

实现 `IScheduledTaskHandler` 接口即可被系统自动发现：

```csharp
public class MyCustomHandler : IScheduledTaskHandler
{
    public Task<string> ExecuteAsync(IServiceProvider sp,
        Dictionary<string, string?> parameters,
        CancellationToken ct)
    {
        // 自定义逻辑
        return Task.FromResult("OK");
    }

    public List<ParameterMeta> GetParameterMetas()
    {
        return new() { new("param1", "参数说明") };
    }
}
```

将类放在 `ScheduledTaskHandlers/` 目录下，前端可自动发现并配置。

## Cron 表达式

支持标准 5 字段：`分 时 日 月 周`

| 预设 | 表达式 | 说明 |
|------|--------|------|
| 每分钟 | `* * * * *` | 每分钟执行 |
| 每 5 分钟 | `*/5 * * * *` | 间隔调度 |
| 每小时 | `0 * * * *` | 整点执行 |
| 每天 0 点 | `0 0 * * *` | 日常批处理 |
| 工作日 9 点 | `0 9 * * 1-5` | 工作时间执行 |

支持 `*`（任意）、`*/n`（间隔）、`n-m`（范围）、`n,m`（列表）。

## 执行模式

| 模式 | 说明 |
|------|------|
| 定时重复 (Cron) | 按 Cron 表达式周期执行 |
| 仅执行一次 | 指定时间执行后自动禁用（RunOnceAt） |

## 可靠性保证

- **并发控制**：同一个任务不会并发执行（执行中自动跳过）
- **状态追踪**：记录最后执行时间、成功/失败状态、耗时、消息
- **手动触发**：`POST /api/schedule/{id}/trigger` 立即执行（不受 enabled 限制）
- **错误处理**：执行异常不中断调度器，错误信息记录到日志

## API 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/schedule` | 任务列表 |
| POST | `/api/schedule` | 创建任务 |
| PUT | `/api/schedule/{id}` | 编辑任务 |
| DELETE | `/api/schedule/{id}` | 删除任务 |
| POST | `/api/schedule/{id}/trigger` | 手动触发 |
| GET | `/api/schedule/handlers` | 发现处理器类 |

## 前端页面

路径：`/dashboard/schedule`（需 `integration:manage` 权限）

功能：
- 任务列表（名称/Cron/状态/最后执行时间/耗时）
- 创建/编辑对话框（选择执行方式、配置参数）
- Cron 表达式选择器
- 手动触发按钮
- 处理器类自动发现列表
