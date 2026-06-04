# 项目结构

```
demo/
├── backend/                         # ASP.NET Core 后端
│   ├── Controllers/                 # 19 个 API 控制器
│   │   ├── AuthController.cs            # 登录 / 注册 / 密码重置
│   │   ├── UserController.cs            # 用户信息 / 主页配置 / 在线用户
│   │   ├── UserManagementController.cs  # 用户 CRUD / 冻结 / 批量操作 / 角色分配
│   │   ├── RoleController.cs            # 角色 CRUD + 权限分配
│   │   ├── MenuController.cs            # 动态菜单（按权限过滤）
│   │   ├── PermissionController.cs      # 权限列表 / 权限树
│   │   ├── MaterialController.cs        # 材料字典 CRUD
│   │   ├── InboundOrderController.cs    # 入库单 CRUD / 明细 / 附件 / 导出 / 提交审批
│   │   ├── AttachmentController.cs      # 统一附件管理（MinIO）
│   │   ├── DatabaseController.cs        # 外部数据库连接 / 表结构 / SQL 执行
│   │   ├── IntegrationController.cs     # 集成平台（连接 / 任务 / 日志）
│   │   ├── ScheduleController.cs        # 计划任务调度（Cron / 一次性）
│   │   ├── WorkflowController.cs        # 流程定义 CRUD / 发布 / 版本管理
│   │   ├── WorkflowInstanceController.cs # 流程实例 / 发起 / 审批 / 召回
│   │   ├── WorkflowTaskController.cs    # 待办 / 已办 / 我的申请
│   │   ├── OAuthController.cs           # OAuth 2.0 授权 / Token / 用户信息
│   │   ├── OAuthClientController.cs     # OAuth 客户端管理
│   │   ├── SsoController.cs             # SSO 魔法链接 / 授权码登录
│   │   └── WeatherController.cs         # 天气查询（IP 定位 + wttr.in）
│   ├── Models/                      # 28+ 实体模型
│   ├── DTOs/                        # 11 组数据传输对象
│   ├── Services/                    # 17 个业务服务
│   │   ├── AuthService.cs              # JWT 签发 / BCrypt / Redis 缓存 / 在线追踪
│   │   ├── PermissionService.cs        # RBAC 权限 + 角色 + 菜单
│   │   ├── EmailService.cs             # SMTP 邮件发送
│   │   ├── InboundOrderService.cs      # 入库单 + 材料字典业务
│   │   ├── AttachmentService.cs        # 统一附件管理
│   │   ├── MinioService.cs             # MinIO S3 客户端
│   │   ├── DatabaseService.cs          # 多数据库连接 / SQL 安全执行
│   │   ├── IntegrationService.cs       # 低代码集成引擎
│   │   ├── ScheduleService.cs          # Cron 调度器（BackgroundService）
│   │   ├── WorkflowEngine.cs           # 流程状态机
│   │   ├── WorkflowService.cs          # 流程业务管理
│   │   ├── WorkflowTimeoutService.cs   # 审批超时处理（BackgroundService）
│   │   ├── OAuthService.cs             # OAuth 2.0 授权码流程 + PKCE
│   │   ├── OidcService.cs              # OpenID Connect
│   │   ├── RsaKeyProvider.cs           # RSA 2048 密钥对管理 + JWKS
│   │   └── SsoService.cs               # SSO 令牌 / 授权码
│   ├── ScheduledTaskHandlers/       # 计划任务处理器
│   │   ├── CreateUserHandler.cs        # 自动创建用户
│   │   ├── CleanupLogsHandler.cs       # 清理日志
│   │   ├── DataExportHandler.cs        # 数据导出
│   │   └── MaterialStatsHandler.cs     # 材料统计
│   ├── Data/AppDbContext.cs         # EF Core 数据库上下文
│   ├── SeedData.cs                  # 种子数据（角色 / 权限 / 菜单 / 材料 / OAuth 客户端）
│   ├── Filters/                     # 中间件 / 过滤器
│   └── Program.cs                   # 应用入口 + 中间件配置
├── frontend/                        # Vue 3 前端
│   └── src/
│       ├── api/auth.js                  # 70+ API 函数封装（Axios 实例 + 拦截器）
│       ├── router/index.js              # 路由 + 守卫（SSO/OAuth 回调处理）
│       ├── composables/useOAuth.js      # OAuth 2.0 + PKCE 工具函数
│       ├── components/                  # 7 个通用组件
│       │   ├── WeatherCard.vue              # 天气卡片（IP 定位 + wttr.in）
│       │   ├── InboundChartCard.vue         # 入库金额图表（ECharts）
│       │   ├── StatsCard.vue                # 在线用户统计
│       │   ├── ClockCard.vue                # 实时数字时钟
│       │   ├── TodoCard.vue                 # 待办任务卡片
│       │   ├── DoneCard.vue                 # 已办任务卡片
│       │   └── KeyValueEditor.vue           # 键值对编辑器
│       └── views/                       # 24 个页面组件
│           ├── Login.vue                    # 登录（含 OAuth 入口 / 授权码登录）
│           ├── Register.vue                 # 注册
│           ├── Dashboard.vue                # 主布局（动态菜单 + 菜单搜索）
│           ├── HomePage.vue                 # 可配置卡片仪表盘
│           ├── UserProfile.vue              # 个人信息修改
│           ├── UserManagement.vue           # 用户管理
│           ├── RoleManagement.vue           # 角色管理（树形权限勾选）
│           ├── MaterialDictionary.vue       # 材料字典
│           ├── InboundOrder.vue             # 入库单列表
│           ├── InboundOrderDetail.vue       # 入库单详情 + 附件管理
│           ├── OAuthCallback.vue            # OAuth 回调处理
│           ├── SsoManagement.vue            # SSO 令牌管理
│           ├── OAuthClientManagement.vue    # OAuth 客户端管理
│           ├── HomeConfig.vue               # 主页布局配置
│           ├── AttachmentManagement.vue     # 统一附件管理
│           ├── DatabaseManagement.vue       # 外部数据库管理
│           ├── IntegrationManagement.vue    # 集成平台
│           ├── ScheduleManagement.vue       # 计划任务管理
│           ├── WorkflowDesigner.vue         # 流程设计器（LogicFlow）
│           ├── WorkflowManagement.vue       # 流程定义管理
│           ├── WorkflowMonitor.vue          # 流程监控
│           ├── WorkflowTodo.vue             # 待办中心
│           ├── WorkflowDone.vue             # 已办任务
│           └── WorkflowStats.vue            # 流程统计
├── docs/                            # VitePress 文档站点
├── MockThirdPartyApi/               # 第三方 API 模拟（ERP 系统）
├── SsoTestClient/                   # OAuth 2.0 + OIDC 测试客户端
├── docker-compose.yml               # Docker 编排（Redis + MinIO + Nginx）
├── nginx.conf                       # Nginx 反向代理配置
├── start.bat / stop.bat             # Windows 一键启动 / 停止脚本
└── README.md
```
