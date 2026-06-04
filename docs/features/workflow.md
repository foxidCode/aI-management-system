# 流程审批

## 架构

```
WorkflowDefinition (模板) → WorkflowInstance (实例) → WorkflowTask (待办)
       ↓ 版本管理              ↓ 状态跟踪              ↓ 审批操作
   发布/归档/新版本         running/approved/       pending/approved/
                           rejected/recalled       rejected
```

## 节点类型

| 类型 | 图标 | 说明 |
|------|------|------|
| start | 🟢 | 开始节点，自动创建 |
| approval | 🔵 | 审批节点，需配置审批人 |
| cc | 🟠 | 抄送节点，仅通知 |
| condition | 🟣 | 条件分支 |
| end | 🔴 | 结束节点，自动完成 |

## 审批策略

| 策略 | 说明 |
|------|------|
| 任签 (any) | 任一审批人通过即推进 |
| 会签 (all) | 所有审批人通过才推进 |

## 超时处理

| 策略 | 行为 |
|------|------|
| auto_reject | 超时自动驳回 |
| auto_pass | 超时自动通过 |
| escalate | 升级处理（需管理员介入） |

## 版本管理

- 草稿 → 发布 → 新编辑自动创建新版本
- 已发布的旧版本自动归档
- 运行中的实例不受版本变更影响（快照机制）

## API 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/workflow` | 流程定义列表 |
| POST | `/api/workflow` | 创建流程 |
| PUT | `/api/workflow/{id}` | 编辑（自动版本化） |
| POST | `/api/workflow/{id}/publish` | 发布 |
| POST | `/api/workflow-instance/start` | 发起流程 |
| POST | `/api/workflow-instance/{id}/approve` | 审批通过 |
| POST | `/api/workflow-instance/{id}/reject` | 审批驳回 |
| GET | `/api/workflow-task/todo` | 我的待办 |
| GET | `/api/workflow-task/done` | 我的已办 |

## 前端页面

| 路径 | 页面 | 权限 |
|------|------|------|
| `/dashboard/workflows` | 流程管理 | `workflow:manage` |
| `/dashboard/workflows/design` | 流程设计器 | `workflow:manage` |
| `/dashboard/workflows/todo` | 待办任务 | `workflow:approve` |
| `/dashboard/workflows/done` | 已办任务 | `workflow:approve` |
| `/dashboard/workflows/monitor` | 流程监控 | `workflow:monitor` |
| `/dashboard/workflows/stats` | 流程统计 | `workflow:manage` |
