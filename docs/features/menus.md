# 动态菜单

## 机制

菜单根据用户角色权限动态加载。用户登录后，前端调用 `GET /api/menu` 获取该用户可见的菜单树。后端根据用户权限过滤无权限的菜单项。

## 菜单结构

```json
{
  "id": 1,
  "name": "系统管理",
  "icon": "Setting",
  "path": null,
  "sortOrder": 99,
  "children": [
    { "id": 10, "name": "计划任务", "path": "/dashboard/schedule", "permissionCode": "integration:manage" }
  ]
}
```

## 菜单属性

| 字段 | 说明 |
|------|------|
| name | 显示名称 |
| path | 路由路径（`http://` 开头则作为外部链接在新窗口打开） |
| icon | Element Plus 图标名 |
| parentId | 父级菜单 ID（null 为一级菜单） |
| sortOrder | 排序数值（越小越靠前） |
| permissionCode | 权限码（null = 所有人可见） |
| menuType | 类型：menu（菜单项）/ button（按钮） |
| component | Vue 组件名（用于路由懒加载） |

## 菜单搜索

顶栏左侧的搜索框支持快速菜单搜索：

- **输入匹配**：模糊匹配菜单名称
- **分组显示**：显示父级分组名称
- **键盘导航**：↑ ↓ 切换，Enter 跳转，Esc 关闭
- **权限过滤**：搜索结果仅显示用户有权限的菜单

## 权限过滤规则

1. `permissionCode` 为 `null` → 所有人可见
2. `permissionCode` 有值 → 仅拥有对应权限的用户可见
3. **admin 用户特殊规则**：自动获取全部菜单（包括 `database:manage` 等管理员专属菜单）
4. 父菜单无可见子菜单时，父菜单也隐藏

## 菜单层级

```
📊 主页
  ├── 仪表盘 → /dashboard/home
  └── 个人信息 → /dashboard/profile

⚙️ 系统管理
  ├── 用户列表 → /dashboard/users
  ├── 角色管理 → /dashboard/roles
  ├── 材料字典 → /dashboard/materials
  ├── 入库单 → /dashboard/inbound
  ├── SSO 管理 → /dashboard/sso
  ├── OAuth 客户端 → /dashboard/oauth-clients
  ├── 主页配置 → /dashboard/home-config
  ├── 附件管理 → /dashboard/attachments
  ├── 数据库管理 → /dashboard/database
  ├── 集成平台 → /dashboard/integration
  ├── 计划任务 → /dashboard/schedule
  └── 系统配置 → (系统设置)

📚 接口文档 → /swagger (外部链接)
❓ 帮助中心 → http://localhost:5174 (外部链接)
```

## 外部链接

菜单 `path` 以 `http://` 或 `https://` 开头时，前端自动识别并在新标签页打开，不进行 SPA 路由跳转。适用于 Swagger 文档、帮助中心等外部资源。
