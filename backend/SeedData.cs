using backend.Data;
using backend.Models;

namespace backend;

public static class SeedData
{
    public static void Initialize(AppDbContext db)
    {
        // ========== 权限定义 ==========
        var permissions = new List<Permission>
        {
            new() { Name = "用户查看", Code = "user:view", Description = "查看用户列表和详情" },
            new() { Name = "用户创建", Code = "user:create", Description = "创建新用户" },
            new() { Name = "用户编辑", Code = "user:edit", Description = "修改用户信息" },
            new() { Name = "用户删除", Code = "user:delete", Description = "删除用户" },
            new() { Name = "用户冻结", Code = "user:freeze", Description = "冻结/解冻用户" },
            new() { Name = "密码重置", Code = "user:reset_password", Description = "重置用户密码" },
            new() { Name = "角色管理", Code = "role:manage", Description = "创建、编辑、删除角色" },
            new() { Name = "权限分配", Code = "role:assign_permission", Description = "为角色分配权限" },
            new() { Name = "系统配置", Code = "system:config", Description = "访问系统配置" },
            new() { Name = "材料查看", Code = "material:view", Description = "查看材料字典" },
            new() { Name = "材料管理", Code = "material:manage", Description = "新增、编辑、删除材料" },
            new() { Name = "入库单查看", Code = "inbound:view", Description = "查看入库单列表和详情" },
            new() { Name = "入库单管理", Code = "inbound:manage", Description = "新增、编辑、删除入库单" },
        };
        db.Permissions.AddRange(permissions);
        db.SaveChanges();

        // ========== 角色定义 ==========
        var adminRole = new Role { Name = "超级管理员", Description = "拥有所有权限", CreatedAt = DateTime.Now };
        var userRole = new Role { Name = "普通用户", Description = "默认用户角色，仅可查看用户信息", CreatedAt = DateTime.Now };
        var viewerRole = new Role { Name = "只读用户", Description = "仅可查看，无操作权限", CreatedAt = DateTime.Now };

        db.Roles.AddRange(adminRole, userRole, viewerRole);
        db.SaveChanges();

        // ========== 角色权限分配 ==========
        // 超级管理员：所有权限
        foreach (var perm in permissions)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionId = perm.Id });
        }

        // 普通用户：查看用户、查看系统配置
        var userPerms = permissions.Where(p => p.Code is "user:view" or "system:config").ToList();
        foreach (var perm in userPerms)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = userRole.Id, PermissionId = perm.Id });
        }

        // 只读用户：仅查看用户
        var viewerPerm = permissions.FirstOrDefault(p => p.Code == "user:view");
        if (viewerPerm != null)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = viewerRole.Id, PermissionId = viewerPerm.Id });
        }

        db.SaveChanges();

        // ========== 菜单定义 ==========
        var menus = new List<Menu>
        {
            new() { Name = "主页", Path = "/dashboard/home", Icon = "HomeFilled", ParentId = null, SortOrder = 0, Component = "HomePage" },
            new() { Name = "系统配置", Path = "/dashboard/profile", Icon = "Setting", ParentId = null, SortOrder = 1, PermissionCode = "system:config", Component = "UserProfile" },
            new() { Name = "用户列表", Path = "/dashboard/users", Icon = "List", ParentId = null, SortOrder = 2, PermissionCode = "user:view", Component = "UserManagement" },
            new() { Name = "角色管理", Path = "/dashboard/roles", Icon = "Lock", ParentId = null, SortOrder = 3, PermissionCode = "role:manage", Component = "RoleManagement" },
            new() { Name = "材料字典", Path = "/dashboard/materials", Icon = "Box", ParentId = null, SortOrder = 4, PermissionCode = "material:view", Component = "MaterialDictionary" },
            new() { Name = "入库单", Path = "/dashboard/inbound", Icon = "Document", ParentId = null, SortOrder = 5, PermissionCode = "inbound:view", Component = "InboundOrder" },
        };

        db.Menus.AddRange(menus);
        db.SaveChanges();

        // ========== 内置管理员账号 ==========
        var adminUser = new User
        {
            Username = "admin",
            Email = "admin@system.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        db.Users.Add(adminUser);
        db.SaveChanges();

        // 将admin分配为超级管理员
        db.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });
        db.SaveChanges();
    }

    /// <summary>
    /// 确保权限存在（增量更新）
    /// </summary>
    public static void EnsurePermissions(AppDbContext db)
    {
        var existingCodes = db.Permissions.Select(p => p.Code).ToHashSet();
        var newPerms = new List<Permission>
        {
            new() { Name = "材料查看", Code = "material:view", Description = "查看材料字典" },
            new() { Name = "材料管理", Code = "material:manage", Description = "新增、编辑、删除材料" },
            new() { Name = "入库单查看", Code = "inbound:view", Description = "查看入库单列表和详情" },
            new() { Name = "入库单管理", Code = "inbound:manage", Description = "新增、编辑、删除入库单" },
            new() { Name = "SSO链接管理", Code = "sso:manage", Description = "生成、查看和撤销SSO一键登录链接" },
            new() { Name = "主页配置", Code = "home:config", Description = "配置主页仪表盘卡片布局" },
            new() { Name = "附件管理", Code = "attachment:manage", Description = "统一查看和管理所有模块的附件" },
            new() { Name = "数据库管理", Code = "database:manage", Description = "配置数据库连接，浏览表结构，执行SQL查询" },
            new() { Name = "集成平台", Code = "integration:manage", Description = "低代码集成平台：配置接口连接、数据同步任务" },
            new() { Name = "流程管理", Code = "workflow:manage", Description = "创建、编辑、发布、归档流程定义" },
            new() { Name = "审批操作", Code = "workflow:approve", Description = "审批/驳回流程节点，查看待办已办" },
            new() { Name = "流程监控", Code = "workflow:monitor", Description = "查看所有流程实例，强制终止或删除" },
        };

        var added = new List<Permission>();
        foreach (var p in newPerms)
        {
            if (!existingCodes.Contains(p.Code))
            {
                db.Permissions.Add(p);
                added.Add(p);
            }
        }

        if (added.Count > 0)
        {
            db.SaveChanges();
            // 给超级管理员分配新权限（database:manage 除外，仅admin账号可使用）
            var adminRole = db.Roles.FirstOrDefault(r => r.Name == "超级管理员");
            if (adminRole != null)
            {
                foreach (var p in added.Where(p => p.Code != "database:manage"))
                {
                    if (!db.RolePermissions.Any(rp => rp.RoleId == adminRole.Id && rp.PermissionId == p.Id))
                        db.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionId = p.Id });
                }
                db.SaveChanges();
            }
        }
    }

    /// <summary>
    /// 确保 OAuth 权限存在
    /// </summary>
    public static void EnsureOAuthPermissions(AppDbContext db)
    {
        var existingCodes = db.Permissions.Select(p => p.Code).ToHashSet();
        var newPerms = new List<Permission>
        {
            new() { Name = "OAuth客户端管理", Code = "oauth:manage", Description = "管理OAuth 2.0客户端注册" },
        };

        var added = new List<Permission>();
        foreach (var p in newPerms)
        {
            if (!existingCodes.Contains(p.Code))
            {
                db.Permissions.Add(p);
                added.Add(p);
            }
        }

        if (added.Count > 0)
        {
            db.SaveChanges();
            var adminRole = db.Roles.FirstOrDefault(r => r.Name == "超级管理员");
            if (adminRole != null)
            {
                foreach (var p in added)
                {
                    if (!db.RolePermissions.Any(rp => rp.RoleId == adminRole.Id && rp.PermissionId == p.Id))
                        db.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionId = p.Id });
                }
                db.SaveChanges();
            }
        }
    }

    /// <summary>
    /// 确保 OAuth 菜单存在
    /// </summary>
    public static void EnsureOAuthMenus(AppDbContext db)
    {
        var existingPaths = db.Menus.Select(m => m.Path).ToHashSet();

        var newMenus = new List<Menu>
        {
            new() { Name = "OAuth客户端", Path = "/dashboard/oauth-clients", Icon = "Key", ParentId = null, SortOrder = 7, PermissionCode = "oauth:manage", Component = "OAuthClientManagement" },
        };

        foreach (var m in newMenus)
        {
            if (!existingPaths.Contains(m.Path))
            {
                db.Menus.Add(m);
            }
        }

        db.SaveChanges();
    }

    /// <summary>
    /// 确保内置 OAuth 客户端存在
    /// </summary>
    public static void EnsureOAuthClients(AppDbContext db)
    {
        if (db.OAuthClients.Any(c => c.ClientId == "vue-spa"))
            return;

        var client = new Models.OAuthClient
        {
            ClientId = "vue-spa",
            ClientName = "Vue前端应用",
            RedirectUris = "[\"http://localhost:5173/callback\",\"http://localhost:8080/callback\",\"http://localhost:18080/callback\"]",
            AllowedScopes = "openid profile email",
            AllowedGrantTypes = "authorization_code refresh_token",
            IsFirstParty = true,
            RequirePkce = true,
            IsActive = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
        };

        db.OAuthClients.Add(client);
        db.SaveChanges();
    }

    /// <summary>
    /// 确保材料字典种子数据（500条）
    /// </summary>
    public static void EnsureMaterials(AppDbContext db)
    {
        if (db.MaterialDictionaries.Any()) return;

        var categories = new[] { "钢材", "水泥", "砂石", "木材", "管材", "电线电缆", "五金", "油漆", "防水材料", "保温材料" };
        var specs = new[] { "国标", "非标", "A级", "B级", "Ⅰ类", "Ⅱ类", "优等品", "合格品" };
        var units = new[] { "吨", "千克", "米", "立方米", "根", "卷", "桶", "块", "套", "个" };
        var models = new[] { "A型", "B型", "C型", "D型", "E型", "普通型", "加强型", "轻型", "重型", "标准型" };
        var rand = new Random(42);

        var materials = new List<Models.MaterialDictionary>();
        for (int i = 1; i <= 500; i++)
        {
            var cat = categories[rand.Next(categories.Length)];
            var name = $"{cat}{rand.Next(1, 100)}号";
            materials.Add(new Models.MaterialDictionary
            {
                Code = $"MAT-{i:D4}",
                Name = name,
                Specification = specs[rand.Next(specs.Length)],
                Model = models[rand.Next(models.Length)],
                Unit = units[rand.Next(units.Length)],
                Remark = i % 10 == 0 ? $"第{i}号材料" : null,
                CreatedAt = DateTime.Now.AddDays(-rand.Next(1, 365)),
                UpdatedAt = DateTime.Now,
            });
        }

        db.MaterialDictionaries.AddRange(materials);
        db.SaveChanges();
    }

    /// <summary>
    /// 确保菜单存在（增量更新，已有菜单不重复添加）
    /// </summary>
    public static void EnsureMenus(AppDbContext db)
    {
        var existingPaths = db.Menus.Select(m => m.Path).ToHashSet();

        var newMenus = new List<Menu>
        {
            new() { Name = "材料字典", Path = "/dashboard/materials", Icon = "Box", ParentId = null, SortOrder = 4, PermissionCode = "material:view", Component = "MaterialDictionary" },
            new() { Name = "入库单", Path = "/dashboard/inbound", Icon = "Document", ParentId = null, SortOrder = 5, PermissionCode = "inbound:view", Component = "InboundOrder" },
            new() { Name = "SSO链接管理", Path = "/dashboard/sso", Icon = "Link", ParentId = null, SortOrder = 6, PermissionCode = "sso:manage", Component = "SsoManagement" },
        };

        foreach (var m in newMenus)
        {
            if (!existingPaths.Contains(m.Path))
            {
                db.Menus.Add(m);
            }
        }

        // ===== 系统管理 父级菜单 + 子菜单 =====
        var sysParent = db.Menus.FirstOrDefault(m => m.Name == "系统管理" && m.ParentId == null);
        if (sysParent == null)
        {
            sysParent = new Menu
            {
                Name = "系统管理",
                Path = null,
                Icon = "Setting",
                ParentId = null,
                SortOrder = 99,
                PermissionCode = null,
                MenuType = "menu",
                Component = null,
            };
            db.Menus.Add(sysParent);
            db.SaveChanges(); // 先保存以获取 Id
        }

        // 主页配置（子菜单）
        if (!existingPaths.Contains("/dashboard/home-config"))
        {
            db.Menus.Add(new Menu
            {
                Name = "主页配置",
                Path = "/dashboard/home-config",
                Icon = "Menu",
                ParentId = sysParent.Id,
                SortOrder = 1,
                PermissionCode = "home:config",
                MenuType = "menu",
                Component = "HomeConfig",
            });
        }

        // 附件管理（子菜单，在系统管理下）
        if (!existingPaths.Contains("/dashboard/attachments"))
        {
            db.Menus.Add(new Menu
            {
                Name = "附件管理",
                Path = "/dashboard/attachments",
                Icon = "Paperclip",
                ParentId = sysParent.Id,
                SortOrder = 2,
                PermissionCode = "attachment:manage",
                MenuType = "menu",
                Component = "AttachmentManagement",
            });
        }

        // 数据库管理（子菜单，在系统管理下）
        if (!existingPaths.Contains("/dashboard/database"))
        {
            db.Menus.Add(new Menu
            {
                Name = "数据库管理",
                Path = "/dashboard/database",
                Icon = "Coin",
                ParentId = sysParent.Id,
                SortOrder = 3,
                PermissionCode = "database:manage",
                MenuType = "menu",
                Component = "DatabaseManagement",
            });
        }

        // Swagger 接口文档（外部链接，新标签页打开）
        if (!existingPaths.Contains("http://localhost:5000/swagger"))
        {
            db.Menus.Add(new Menu
            {
                Name = "接口文档",
                Path = "http://localhost:5000/swagger",
                Icon = "Notebook",
                ParentId = sysParent.Id,
                SortOrder = 4,
                PermissionCode = null,  // 所有人可见
                MenuType = "menu",
                Component = null,
            });
        }

        // 集成平台（子菜单，在系统管理下）
        if (!existingPaths.Contains("/dashboard/integration"))
        {
            db.Menus.Add(new Menu
            {
                Name = "集成平台",
                Path = "/dashboard/integration",
                Icon = "Connection",
                ParentId = sysParent.Id,
                SortOrder = 5,
                PermissionCode = "integration:manage",
                MenuType = "menu",
                Component = "IntegrationManagement",
            });
        }

        // 计划任务（子菜单，在系统管理下）
        if (!existingPaths.Contains("/dashboard/schedule"))
        {
            db.Menus.Add(new Menu
            {
                Name = "计划任务",
                Path = "/dashboard/schedule",
                Icon = "Clock",
                ParentId = sysParent.Id,
                SortOrder = 6,
                PermissionCode = "integration:manage",
                MenuType = "menu",
                Component = "ScheduleManagement",
            });
        }

        // 如果系统配置有 system:config 权限，也移到系统管理下（可选）
        var sysConfigMenu = db.Menus.FirstOrDefault(m => m.Path == "/dashboard/profile");
        if (sysConfigMenu != null && sysConfigMenu.ParentId == null)
        {
            sysConfigMenu.ParentId = sysParent.Id;
            sysConfigMenu.SortOrder = 0;
        }

        // 更新已有菜单的权限码
        var existingMenus = db.Menus.ToList();
        foreach (var m in existingMenus)
        {
            if (m.Path == "/dashboard/materials" && string.IsNullOrEmpty(m.PermissionCode))
                m.PermissionCode = "material:view";
            else if (m.Path == "/dashboard/inbound" && string.IsNullOrEmpty(m.PermissionCode))
                m.PermissionCode = "inbound:view";
        }

        // ===== 流程中心 父级菜单 + 子菜单 =====
        var wfParent = db.Menus.FirstOrDefault(m => m.Name == "流程中心" && m.ParentId == null);
        if (wfParent == null)
        {
            wfParent = new Menu
            {
                Name = "流程中心",
                Path = null,
                Icon = "Operation",
                ParentId = null,
                SortOrder = 50,
                PermissionCode = null,
                MenuType = "menu",
                Component = null,
            };
            db.Menus.Add(wfParent);
            db.SaveChanges();
        }

        var wfMenus = new[] {
            ("/dashboard/workflows/design", "流程设计", "Edit"),
            ("/dashboard/workflows", "流程管理", "List"),
            ("/dashboard/workflows/monitor", "流程监控", "Monitor"),
            ("/dashboard/workflows/stats", "流程统计", "DataAnalysis"),
        };
        foreach (var (path, name, icon) in wfMenus)
        {
            if (!existingPaths.Contains(path))
            {
                db.Menus.Add(new Menu
                {
                    Name = name, Path = path, Icon = icon,
                    ParentId = wfParent.Id, SortOrder = wfMenus.Select(w => w.Item1).ToList().IndexOf(path),
                    PermissionCode = "workflow:manage", MenuType = "menu",
                    Component = path.Split('/').Last().Replace("-", ""),
                });
            }
        }

        // 待办/已办 — 需要审批权限即可见
        if (!existingPaths.Contains("/dashboard/workflows/todo"))
        {
            db.Menus.Add(new Menu { Name = "待办任务", Path = "/dashboard/workflows/todo", Icon = "AlarmClock", ParentId = wfParent.Id, SortOrder = 10, PermissionCode = "workflow:approve", MenuType = "menu", Component = "WorkflowTodo" });
        }
        if (!existingPaths.Contains("/dashboard/workflows/done"))
        {
            db.Menus.Add(new Menu { Name = "已办任务", Path = "/dashboard/workflows/done", Icon = "Finished", ParentId = wfParent.Id, SortOrder = 11, PermissionCode = "workflow:approve", MenuType = "menu", Component = "WorkflowDone" });
        }

        // 帮助中心（外部链接，新标签页打开）
        if (!existingPaths.Contains("http://localhost:5174"))
        {
            db.Menus.Add(new Menu { Name = "帮助中心", Path = "http://localhost:5174", Icon = "Notebook", ParentId = null, SortOrder = 100, PermissionCode = null, MenuType = "menu", Component = null });
        }

        db.SaveChanges();
    }
}
