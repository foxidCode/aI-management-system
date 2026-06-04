# 项目结构

```
demo/
├── backend/                    # ASP.NET Core 后端
│   ├── Controllers/            # API 控制器 (16个)
│   │   ├── AuthController.cs       # 登录/注册/密码重置
│   │   ├── UserController.cs       # 用户信息
│   │   ├── UserManagementController.cs  # 用户管理 CRUD
│   │   ├── RoleController.cs       # 角色/权限管理
│   │   ├── InboundOrderController.cs   # 入库单管理
│   │   ├── WorkflowController.cs       # 流程定义管理
│   │   ├── WorkflowInstanceController.cs # 流程实例 + 审批
│   │   ├── WorkflowTaskController.cs    # 待办/已办
│   │   ├── ScheduleController.cs    # 计划任务
│   │   ├── IntegrationController.cs # 集成平台
│   │   ├── DatabaseController.cs    # 数据库管理
│   │   ├── MaterialController.cs    # 材料字典
│   │   ├── AttachmentController.cs  # 附件管理
│   │   ├── OAuthController.cs       # OAuth 2.0
│   │   ├── OAuthClientController.cs # OAuth 客户端
│   │   └── SsoController.cs         # SSO 单点登录
│   ├── Models/                 # 数据模型 (28+ 实体)
│   ├── DTOs/                   # 数据传输对象
│   ├── Services/               # 业务逻辑层
│   ├── ScheduledTaskHandlers/  # 计划任务处理器
│   ├── Data/AppDbContext.cs    # 数据库上下文
│   ├── SeedData.cs             # 种子数据
│   └── Program.cs              # 应用入口
├── frontend/                   # Vue 3 前端
│   └── src/
│       ├── api/auth.js             # API 封装
│       ├── router/index.js         # 路由配置
│       ├── components/             # 通用组件
│       │   ├── KeyValueEditor.vue      # 键值编辑器
│       │   ├── StatsCard.vue           # 统计卡片
│       │   └── InboundChartCard.vue    # 图表卡片
│       └── views/                  # 页面组件 (24个)
│           ├── Dashboard.vue           # 主布局
│           ├── Login.vue / Register.vue # 登录注册
│           ├── UserManagement.vue      # 用户管理
│           ├── RoleManagement.vue      # 角色管理
│           ├── InboundOrder.vue        # 入库单列表
│           ├── WorkflowDesigner.vue    # 流程设计器
│           ├── WorkflowTodo.vue        # 待办中心
│           ├── WorkflowManagement.vue  # 流程管理
│           └── ...
├── docs/                       # VitePress 文档
├── MockThirdPartyApi/          # 第三方 API 模拟
├── SsoTestClient/              # SSO 测试客户端
├── docker-compose.yml          # Docker 编排
├── nginx.conf                  # Nginx 配置
└── README.md
```
