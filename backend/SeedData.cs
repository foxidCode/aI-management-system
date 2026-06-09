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
            new() { Name = "菜单管理", Code = "menu:manage", Description = "管理后台侧边栏菜单结构" },
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
            new() { Name = "材料字典", Path = "/dashboard/materials", Icon = "Collection", ParentId = null, SortOrder = 4, PermissionCode = "material:view", Component = "MaterialDictionary" },
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
    /// 确保工作流权限存在
    /// </summary>
    public static void EnsureWorkflowPermissions(AppDbContext db)
    {
        var existingCodes = db.Permissions.Select(p => p.Code).ToHashSet();
        var newPerms = new List<Permission>
        {
            new() { Name = "流程定义", Code = "workflow:define", Description = "设计、编辑、发布工作流定义" },
            new() { Name = "流程提交", Code = "workflow:submit", Description = "发起工作流申请" },
            new() { Name = "流程审批", Code = "workflow:approve", Description = "审批待办任务" },
            new() { Name = "流程查看", Code = "workflow:view", Description = "查看所有流程实例和历史" },
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

                // 也给普通用户分配流程提交和审批权限
                var normalRole = db.Roles.FirstOrDefault(r => r.Name == "普通用户");
                if (normalRole != null)
                {
                    var userPerms = added.Where(p => p.Code is "workflow:submit" or "workflow:approve").ToList();
                    foreach (var p in userPerms)
                    {
                        if (!db.RolePermissions.Any(rp => rp.RoleId == normalRole.Id && rp.PermissionId == p.Id))
                            db.RolePermissions.Add(new RolePermission { RoleId = normalRole.Id, PermissionId = p.Id });
                    }
                    db.SaveChanges();
                }
            }
        }
    }

    /// <summary>
    /// 确保工作流菜单存在
    /// </summary>
    public static void EnsureWorkflowMenus(AppDbContext db)
    {
        var existingPaths = db.Menus.Select(m => m.Path).ToHashSet();

        // 工作流父级菜单
        var wfParent = db.Menus.FirstOrDefault(m => m.Name == "工作流" && m.ParentId == null);
        if (wfParent == null)
        {
            wfParent = new Menu
            {
                Name = "工作流",
                Path = null,
                Icon = "Promotion",
                ParentId = null,
                SortOrder = 6,
                PermissionCode = null,
                MenuType = "menu",
                Component = null,
            };
            db.Menus.Add(wfParent);
            db.SaveChanges();
        }

        // 调整已有菜单的 SortOrder 给工作流腾位置（入库单 5→7，OAuth 7→8）
        var inbound = db.Menus.FirstOrDefault(m => m.Path == "/dashboard/inbound");
        if (inbound != null && inbound.ParentId == null) { inbound.SortOrder = 7; }

        var oauthMenu = db.Menus.FirstOrDefault(m => m.Path == "/dashboard/oauth-clients");
        if (oauthMenu != null) { oauthMenu.SortOrder = 8; }

        // 子菜单
        var childMenus = new List<Menu>
        {
            new() { Name = "流程定义", Path = "/dashboard/workflow/definitions", Icon = "Document", ParentId = wfParent.Id, SortOrder = 1, PermissionCode = "workflow:define", MenuType = "menu", Component = "WorkflowDefinitionList" },
            new() { Name = "我的申请", Path = "/dashboard/workflow/my-applications", Icon = "Edit", ParentId = wfParent.Id, SortOrder = 2, PermissionCode = "workflow:submit", MenuType = "menu", Component = "WorkflowMyApplications" },
            new() { Name = "待办审批", Path = "/dashboard/workflow/my-tasks", Icon = "Select", ParentId = wfParent.Id, SortOrder = 3, PermissionCode = "workflow:approve", MenuType = "menu", Component = "WorkflowTaskList" },
        };

        foreach (var m in childMenus)
        {
            if (!existingPaths.Contains(m.Path))
            {
                db.Menus.Add(m);
            }
        }

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
            new() { Name = "材料字典", Path = "/dashboard/materials", Icon = "Collection", ParentId = null, SortOrder = 4, PermissionCode = "material:view", Component = "MaterialDictionary" },
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
                Icon = "Files",
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
                Icon = "Money",
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
                Icon = "Share",
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

        // 菜单管理（子菜单，在系统管理下，仅admin可见）
        if (!existingPaths.Contains("/dashboard/menus"))
        {
            db.Menus.Add(new Menu
            {
                Name = "菜单管理",
                Path = "/dashboard/menus",
                Icon = "Menu",
                ParentId = sysParent.Id,
                SortOrder = 7,
                PermissionCode = "menu:manage",
                MenuType = "menu",
                Component = "MenuManagement",
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

        // 帮助中心（外部链接，新标签页打开）
        if (!existingPaths.Contains("http://localhost:5174"))
        {
            db.Menus.Add(new Menu { Name = "帮助中心", Path = "http://localhost:5174", Icon = "Notebook", ParentId = null, SortOrder = 100, PermissionCode = null, MenuType = "menu", Component = null });
        }

        db.SaveChanges();
    }

    /// <summary>
    /// 确保权限管理相关权限存在
    /// </summary>
    public static void EnsurePermissionManagementPermissions(AppDbContext db)
    {
        var existingCodes = db.Permissions.Select(p => p.Code).ToHashSet();
        var newPerms = new List<Permission>
        {
            new() { Name = "权限管理", Code = "permission:manage", Description = "管理权限项、直接对用户授权" },
            new() { Name = "审计查看", Code = "audit:view", Description = "查看操作日志、权限变更日志和异常告警" },
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
    /// 确保权限管理和安全审计菜单存在
    /// </summary>
    public static void EnsurePermissionManagementMenus(AppDbContext db)
    {
        var existingPaths = db.Menus.Select(m => m.Path).ToHashSet();
        var sysParent = db.Menus.FirstOrDefault(m => m.Name == "系统管理" && m.ParentId == null);
        if (sysParent == null) return; // 系统管理父菜单不存在则跳过

        // 权限管理（子菜单，挂在系统管理下）
        if (!existingPaths.Contains("/dashboard/permissions"))
        {
            db.Menus.Add(new Menu
            {
                Name = "权限管理",
                Path = "/dashboard/permissions",
                Icon = "Key",
                ParentId = sysParent.Id,
                SortOrder = 8,
                PermissionCode = "permission:manage",
                MenuType = "menu",
                Component = "PermissionManagement",
            });
        }

        // 安全审计 父级菜单（挂在系统管理下）
        var auditParent = db.Menus.FirstOrDefault(m => m.Name == "安全审计" && m.ParentId == sysParent.Id);
        if (auditParent == null)
        {
            auditParent = new Menu
            {
                Name = "安全审计",
                Path = null,
                Icon = "Warning",
                ParentId = sysParent.Id,
                SortOrder = 9,
                PermissionCode = "audit:view",
                MenuType = "menu",
                Component = null,
            };
            db.Menus.Add(auditParent);
            db.SaveChanges();
        }

        // 操作日志（安全审计子节点）
        if (!existingPaths.Contains("/dashboard/audit-log"))
        {
            db.Menus.Add(new Menu
            {
                Name = "操作日志",
                Path = "/dashboard/audit-log",
                Icon = "Document",
                ParentId = auditParent.Id,
                SortOrder = 1,
                PermissionCode = "audit:view",
                MenuType = "menu",
                Component = "AuditLog",
            });
        }

        // 权限变更日志（安全审计子节点）
        if (!existingPaths.Contains("/dashboard/permission-change-log"))
        {
            db.Menus.Add(new Menu
            {
                Name = "权限变更日志",
                Path = "/dashboard/permission-change-log",
                Icon = "Lock",
                ParentId = auditParent.Id,
                SortOrder = 2,
                PermissionCode = "audit:view",
                MenuType = "menu",
                Component = "PermissionChangeLog",
            });
        }

        // 异常告警（安全审计子节点）
        if (!existingPaths.Contains("/dashboard/alerts"))
        {
            db.Menus.Add(new Menu
            {
                Name = "异常告警",
                Path = "/dashboard/alerts",
                Icon = "Bell",
                ParentId = auditParent.Id,
                SortOrder = 3,
                PermissionCode = "audit:view",
                MenuType = "menu",
                Component = "AlertManagement",
            });
        }

        db.SaveChanges();
    }

    /// <summary>
    /// 重置所有内置菜单到默认状态：恢复原始父级、排序、可见性，删除所有非内置菜单
    /// </summary>
    public static void ResetBuiltInMenus(AppDbContext db)
    {
        // 删除所有非内置菜单（会级联删除它们的子菜单，但内置子菜单已被保护）
        var nonBuiltIn = db.Menus.Where(m => !m.IsBuiltIn).ToList();
        db.Menus.RemoveRange(nonBuiltIn);
        db.SaveChanges();

        // 重置内置菜单到默认父级和排序
        var defaults = new Dictionary<string, (int? ParentSortKey, int Sort, bool IsVisible)>
        {
            // 顶级菜单
            ["/dashboard/home"] = (null, 0, true),
            ["/dashboard/profile"] = (null, 1, true),
            ["/dashboard/users"] = (null, 2, true),
            ["/dashboard/roles"] = (null, 3, true),
            ["/dashboard/materials"] = (null, 4, true),
            ["/dashboard/inbound"] = (null, 5, true),
            ["/dashboard/sso"] = (null, 7, true),
            ["/dashboard/oauth-clients"] = (null, 8, true),
            ["http://localhost:5174"] = (null, 100, true),
        };

        // 先获取所有内置菜单
        var allBuiltIn = db.Menus.Where(m => m.IsBuiltIn).ToList();

        // 系统管理（ParentId=null, SortOrder=99）
        var sysParent = allBuiltIn.FirstOrDefault(m => m.Name == "系统管理" && m.Path == null);
        if (sysParent != null)
        {
            sysParent.ParentId = null;
            sysParent.SortOrder = 99;
        }

        // 工作流（ParentId=null, SortOrder=6）
        var wfParent = allBuiltIn.FirstOrDefault(m => m.Name == "工作流" && m.Path == null);
        if (wfParent != null)
        {
            wfParent.ParentId = null;
            wfParent.SortOrder = 6;
        }

        // 安全审计（ParentId=sysParent.Id, SortOrder=9）
        var auditParent = allBuiltIn.FirstOrDefault(m => m.Name == "安全审计" && m.Path == null);
        if (auditParent != null && sysParent != null)
        {
            auditParent.ParentId = sysParent.Id;
            auditParent.SortOrder = 9;
        }

        // 系统管理下的子菜单
        var sysChildren = new Dictionary<string, int>
        {
            ["/dashboard/profile"] = 0,
            ["/dashboard/home-config"] = 1,
            ["/dashboard/attachments"] = 2,
            ["/dashboard/database"] = 3,
            ["http://localhost:5000/swagger"] = 4,
            ["/dashboard/integration"] = 5,
            ["/dashboard/schedule"] = 6,
            ["/dashboard/menus"] = 7,
            ["/dashboard/permissions"] = 8,
        };
        foreach (var m in allBuiltIn.Where(m => sysChildren.ContainsKey(m.Path ?? "")))
        {
            m.ParentId = sysParent?.Id;
            m.SortOrder = sysChildren[m.Path!];
        }

        // 工作流下的子菜单
        var wfChildren = new Dictionary<string, int>
        {
            ["/dashboard/workflow/definitions"] = 1,
            ["/dashboard/workflow/my-applications"] = 2,
            ["/dashboard/workflow/my-tasks"] = 3,
        };
        foreach (var m in allBuiltIn.Where(m => wfChildren.ContainsKey(m.Path ?? "")))
        {
            m.ParentId = wfParent?.Id;
            m.SortOrder = wfChildren[m.Path!];
        }

        // 安全审计下的子菜单
        var auditChildren = new Dictionary<string, int>
        {
            ["/dashboard/audit-log"] = 1,
            ["/dashboard/permission-change-log"] = 2,
            ["/dashboard/alerts"] = 3,
        };
        foreach (var m in allBuiltIn.Where(m => auditChildren.ContainsKey(m.Path ?? "")))
        {
            m.ParentId = auditParent?.Id;
            m.SortOrder = auditChildren[m.Path!];
        }

        // 顶级路径菜单重置 ParentId=null
        var topLevelPaths = new HashSet<string>
        {
            "/dashboard/home", "/dashboard/users", "/dashboard/roles",
            "/dashboard/materials", "/dashboard/inbound", "/dashboard/sso",
            "/dashboard/oauth-clients", "http://localhost:5174",
        };
        foreach (var m in allBuiltIn.Where(m => topLevelPaths.Contains(m.Path ?? "")))
        {
            m.ParentId = null;
        }

        db.SaveChanges();
    }
}
