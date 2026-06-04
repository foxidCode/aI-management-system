# 计划任务

## 功能

基于 Cron 表达式的定时任务调度，10 秒轮询。支持三种执行方式。

## 执行方式

| 方式 | 说明 | 示例 |
|------|------|------|
| 关联集成任务 | 定时触发集成平台的同步任务 | 每小时同步物料数据 |
| 自定义代码 (CodeHandler) | 运行时编译 C# 代码 | 自动创建测试用户 |
| 执行器类 (HandlerClass) | 反射调用 IScheduledTaskHandler | 清理日志、统计导出 |

## 内置处理器

| 类 | 功能 | 参数 |
|------|------|------|
| `CreateUserHandler` | 自动创建用户 | prefix, password, role |
| `CleanupLogsHandler` | 清理旧日志 | days (默认30) |
| `DataExportHandler` | 统计数据导出 | table, topN, minDate |
| `MaterialStatsHandler` | 系统数据统计 | includeDetail |

## Cron 表达式

支持 5 字段：`分 时 日 月 周`

| 预设 | 表达式 |
|------|--------|
| 每分钟 | `* * * * *` |
| 每 5 分钟 | `*/5 * * * *` |
| 每小时 | `0 * * * *` |
| 每天 0 点 | `0 0 * * *` |
| 工作日 9 点 | `0 9 * * 1-5` |

## 执行模式

- **定时重复**：按 Cron 表达式周期执行
- **仅执行一次**：指定时间执行后自动禁用

## 超时与监控

- 并发控制：同一个任务不会并发执行
- 状态追踪：成功/失败状态、耗时、消息
- 手动触发：`POST /api/schedule/{id}/trigger` 立即执行（不受 enabled 限制）
