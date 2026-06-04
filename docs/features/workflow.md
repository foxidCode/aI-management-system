# 流程审批

## 架构

```
WorkflowDefinition (模板) → WorkflowInstance (实例) → WorkflowTask (待办)
       ↓ 版本管理              ↓ 状态跟踪              ↓ 审批操作
   草稿→发布→归档         running/approved/       pending/approved/
                           rejected/recalled       rejected/transferred
```

## 节点类型

| 类型 | 图标 | 说明 |
|------|------|------|
| start | 🟢 | 开始节点，自动创建，流程起点 |
| approval | 🔵 | 审批节点，需配置审批人（用户/角色）+ 策略 |
| cc | 🟠 | 抄送节点，仅通知，不阻塞流程 |
| condition | 🟣 | 条件分支，按条件路由至不同分支 |
| end | 🔴 | 结束节点，流程自动完成 |

## 审批策略

| 策略 | 说明 | 适用场景 |
|------|------|----------|
| 任签 (any) | 任一审批人通过即推进 | 快速审批 |
| 会签 (all) | 所有审批人通过才推进 | 严格审批 |

## 超时处理

`WorkflowTimeoutService` 后台服务每 30 秒轮询超时任务：

| 策略 | 行为 |
|------|------|
| auto_reject | 超时自动驳回 |
| auto_pass | 超时自动通过 |

超时时长通过节点属性的 `timeoutHours` 配置。

## 审批人配置

| 类型 | 说明 |
|------|------|
| 按用户 | 指定具体用户作为审批人 |
| 按角色 | 指定角色，该角色所有用户均为审批人 |

## 流程操作

| 操作 | 说明 |
|------|------|
| 发起 | 从已发布定义创建实例，自动从 start 节点推进到首个审批节点 |
| 通过 | 审批人同意，按策略判断是否推进到下一节点 |
| 驳回 | 审批人驳回，实例状态变为 rejected，关联业务单状态同步 |
| 召回 | 发起人撤回申请，实例状态变为 recalled |
| 转交 | 审批人将任务转交给其他用户 |

## 版本管理

- **草稿** → 编辑中
- **发布** → 可用于发起流程
- **归档** → 旧版本，运行中实例不受影响

编辑已发布定义时自动创建新版本（旧版本归档），运行中的实例使用发起时的节点快照（不受版本变更影响）。

## 流程设计器

前端使用 LogicFlow 实现可视化拖拽建模：

- 从节点面板拖入节点
- 连线定义流转方向
- 配置节点属性（审批人、策略、超时）
- 支持条件分支的路由配置

## API 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/workflow` | 流程定义列表 |
| POST | `/api/workflow` | 创建流程 |
| GET | `/api/workflow/{id}` | 获取定义详情 |
| PUT | `/api/workflow/{id}` | 编辑（自动版本化） |
| DELETE | `/api/workflow/{id}` | 删除 |
| POST | `/api/workflow/{id}/publish` | 发布 |
| GET | `/api/workflow/stats` | 流程统计 |
| GET | `/api/workflow-instance` | 实例列表 |
| GET | `/api/workflow-instance/{id}` | 实例详情 |
| POST | `/api/workflow-instance/start` | 发起流程 |
| POST | `/api/workflow-instance/{id}/approve` | 审批通过 |
| POST | `/api/workflow-instance/{id}/reject` | 审批驳回 |
| POST | `/api/workflow-instance/{id}/recall` | 召回 |
| GET | `/api/workflow-task/todo` | 我的待办 |
| GET | `/api/workflow-task/done` | 我的已办 |
| GET | `/api/workflow-task/my-applications` | 我的申请 |

## 前端页面

| 路径 | 页面 | 权限 |
|------|------|------|
| `/dashboard/workflows` | 流程管理（定义列表） | `workflow:manage` |
| `/dashboard/workflows/design/:id?` | 流程设计器 | `workflow:manage` |
| `/dashboard/workflows/todo` | 待办任务 | `workflow:approve` |
| `/dashboard/workflows/done` | 已办任务 | `workflow:approve` |
| `/dashboard/workflows/monitor` | 流程监控（实例列表） | `workflow:monitor` |
| `/dashboard/workflows/stats` | 流程统计 | `workflow:manage` |

## 入库单集成

入库单提交审批时：
1. 入库单状态变为 `pending_approval`
2. 创建流程实例，关联 `WorkflowInstanceId`
3. 审批通过 → 入库单状态变为 `approved`
4. 审批驳回 → 入库单状态变为 `rejected`
5. 召回 → 入库单状态回到 `draft`
