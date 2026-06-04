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
  "children": [
    { "id": 10, "name": "计划任务", "path": "/dashboard/schedule", "permissionCode": "integration:manage" }
  ]
}
```

## 菜单属性

| 字段 | 说明 |
|------|------|
| name | 显示名称 |
| path | 路由路径（外部链接以 http:// 开头） |
| icon | Element Plus 图标名 |
| parentId | 父级菜单 ID |
| sortOrder | 排序 |
| permissionCode | 权限码（null = 所有人可见） |
| component | 对应 Vue 组件名 |

## 菜单搜索

顶栏左侧的搜索框支持快速搜索菜单：
- 输入名称模糊匹配
- 显示父级分组名称
- 键盘导航（↑↓Enter）
