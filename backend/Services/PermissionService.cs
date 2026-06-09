using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Models;

namespace backend.Services;

public class PermissionService
{
    private readonly AppDbContext _db;

    public PermissionService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>暴露 DbContext 供种子数据重置等操作使用</summary>
    public AppDbContext GetDbContext() => _db;

    // ========== 角色管理 ==========

    public async Task<RoleListResponse> GetAllRolesAsync(string? keyword, int page = 1, int pageSize = 0)
    {
        var query = _db.Roles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(r => r.Name.ToLower().Contains(kw)
                                  || (r.Description != null && r.Description.ToLower().Contains(kw)));
        }

        var total = await query.CountAsync();

        IQueryable<Role> pagedQuery = query.OrderBy(r => r.Id);
        if (pageSize > 0)
            pagedQuery = pagedQuery.Skip((page - 1) * pageSize).Take(pageSize);

        var list = await pagedQuery
            .Include(r => r.RolePermissions)
            .Select(r => new RoleResponse
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                CreatedAt = r.CreatedAt,
                PermissionIds = r.RolePermissions.Select(rp => rp.PermissionId).ToList()
            })
            .ToListAsync();

        return new RoleListResponse { List = list, Total = total };
    }

    public async Task<RoleResponse?> GetRoleByIdAsync(int id)
    {
        return await _db.Roles
            .Include(r => r.RolePermissions)
            .Where(r => r.Id == id)
            .Select(r => new RoleResponse
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                CreatedAt = r.CreatedAt,
                PermissionIds = r.RolePermissions.Select(rp => rp.PermissionId).ToList()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request)
    {
        if (await _db.Roles.AnyAsync(r => r.Name == request.Name))
            throw new InvalidOperationException("角色名称已存在");

        var role = new Role
        {
            Name = request.Name,
            Description = request.Description,
            CreatedAt = DateTime.Now
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        // 分配权限
        if (request.PermissionIds.Count > 0)
        {
            foreach (var permId in request.PermissionIds)
            {
                if (await _db.Permissions.AnyAsync(p => p.Id == permId))
                {
                    _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permId });
                }
            }
            await _db.SaveChangesAsync();
        }

        return (await GetRoleByIdAsync(role.Id))!;
    }

    public async Task<RoleResponse?> UpdateRoleAsync(int id, UpdateRoleRequest request)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null) return null;

        if (await _db.Roles.AnyAsync(r => r.Name == request.Name && r.Id != id))
            throw new InvalidOperationException("角色名称已存在");

        role.Name = request.Name;
        role.Description = request.Description;

        // 更新权限：先删除旧的，再添加新的
        var existingPerms = _db.RolePermissions.Where(rp => rp.RoleId == id);
        _db.RolePermissions.RemoveRange(existingPerms);

        foreach (var permId in request.PermissionIds)
        {
            if (await _db.Permissions.AnyAsync(p => p.Id == permId))
            {
                _db.RolePermissions.Add(new RolePermission { RoleId = id, PermissionId = permId });
            }
        }

        await _db.SaveChangesAsync();
        return await GetRoleByIdAsync(id);
    }

    public async Task<bool> DeleteRoleAsync(int id)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null) return false;

        // 删除关联的用户角色和权限
        var userRoles = _db.UserRoles.Where(ur => ur.RoleId == id);
        _db.UserRoles.RemoveRange(userRoles);

        var rolePerms = _db.RolePermissions.Where(rp => rp.RoleId == id);
        _db.RolePermissions.RemoveRange(rolePerms);

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        return true;
    }

    // ========== 权限管理 ==========

    public async Task<List<PermissionResponse>> GetAllPermissionsAsync()
    {
        return await _db.Permissions
            .Select(p => new PermissionResponse
            {
                Id = p.Id,
                Name = p.Name,
                Code = p.Code,
                Description = p.Description
            })
            .ToListAsync();
    }

    public async Task<List<PermissionTreeNode>> GetPermissionTreeAsync()
    {
        var allMenus = await _db.Menus.OrderBy(m => m.SortOrder).ToListAsync();
        var permissions = await _db.Permissions.ToListAsync();

        // 先构建菜单树（与侧边栏菜单结构一致）
        var menuTree = BuildMenuTree(allMenus, null);

        // 将菜单树转换为权限树，保持相同的层级结构
        return BuildPermissionTree(menuTree, permissions);
    }

    /// <summary>
    /// 递归将菜单树转换为权限树，权限作为叶子节点挂到对应菜单下
    /// </summary>
    private List<PermissionTreeNode> BuildPermissionTree(List<MenuResponse> menus, List<Permission> permissions)
    {
        var tree = new List<PermissionTreeNode>();

        foreach (var menu in menus)
        {
            var node = new PermissionTreeNode
            {
                Id = $"menu_{menu.Id}",
                Label = menu.Name,
            };

            // 将匹配的权限作为叶子节点添加到对应菜单下
            if (!string.IsNullOrEmpty(menu.PermissionCode))
            {
                var prefix = menu.PermissionCode.Split(':')[0];
                var matchingPerms = permissions
                    .Where(p => p.Code.StartsWith(prefix))
                    .Select(p => new PermissionTreeNode
                    {
                        Id = p.Id.ToString(),
                        Label = p.Name,
                        Code = p.Code
                    })
                    .ToList();

                node.Children.AddRange(matchingPerms);
            }

            // 递归处理子菜单（保持菜单树的父子层级）
            if (menu.Children.Count > 0)
            {
                var childNodes = BuildPermissionTree(menu.Children, permissions);
                node.Children.AddRange(childNodes);
            }

            // 只添加有内容的节点（有权限或有子菜单的节点）
            if (node.Children.Count > 0)
            {
                tree.Add(node);
            }
        }

        return tree;
    }

    // ========== 菜单管理 ==========

    public async Task<List<MenuResponse>> GetMenusForUserAsync(int userId)
    {
        // 获取用户的所有权限编码（角色权限 + 直接权限）
        var roleCodes = await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync();

        var directCodes = await _db.UserPermissions
            .Where(up => up.UserId == userId)
            .Select(up => up.Permission.Code)
            .Distinct()
            .ToListAsync();

        var permissionCodes = roleCodes.Union(directCodes).Distinct().ToList();

        // 数据库管理 + 菜单管理仅admin账号可见（通过 role:manage 判断是否为admin）
        if (permissionCodes.Contains("role:manage"))
        {
            permissionCodes.Add("database:manage");
            permissionCodes.Add("menu:manage");
        }

        // 获取所有菜单
        var allMenus = await _db.Menus
            .OrderBy(m => m.SortOrder)
            .ToListAsync();

        // 过滤用户有权限的菜单（没有 PermissionCode 的菜单所有人可见 + 必须可见）
        var accessibleMenus = allMenus
            .Where(m => m.IsVisible && (string.IsNullOrEmpty(m.PermissionCode) || permissionCodes.Contains(m.PermissionCode)))
            .ToList();

        // 构建菜单树
        return BuildMenuTree(accessibleMenus, null);
    }

    public async Task<List<MenuResponse>> GetAllMenusAsync()
    {
        var allMenus = await _db.Menus
            .OrderBy(m => m.SortOrder)
            .ToListAsync();

        return BuildMenuTree(allMenus, null);
    }

    private List<MenuResponse> BuildMenuTree(List<Menu> menus, int? parentId)
    {
        return menus
            .Where(m => m.ParentId == parentId)
            .OrderBy(m => m.SortOrder)
            .Select(m => new MenuResponse
            {
                Id = m.Id,
                Name = m.Name,
                Path = m.Path,
                Icon = m.Icon,
                ParentId = m.ParentId,
                SortOrder = m.SortOrder,
                PermissionCode = m.PermissionCode,
                Component = m.Component,
                OpenType = m.OpenType,
                IsVisible = m.IsVisible,
                IsBuiltIn = m.IsBuiltIn,
                Children = BuildMenuTree(menus, m.Id)
            })
            .ToList();
    }

    // ========== 菜单 CRUD ==========

    public async Task<MenuResponse> CreateMenuAsync(CreateMenuRequest req)
    {
        var menu = new Menu
        {
            Name = req.Name,
            Path = req.Path,
            Icon = req.Icon,
            ParentId = req.ParentId,
            SortOrder = req.SortOrder,
            PermissionCode = req.PermissionCode,
            Component = req.Component,
            OpenType = req.OpenType ?? "self",
            IsVisible = req.IsVisible,
        };
        _db.Menus.Add(menu);
        await _db.SaveChangesAsync();
        return await GetMenuResponseByIdAsync(menu.Id);
    }

    public async Task<MenuResponse?> UpdateMenuAsync(int id, UpdateMenuRequest req)
    {
        var menu = await _db.Menus.FindAsync(id);
        if (menu == null) return null;

        menu.Name = req.Name;
        menu.Path = req.Path;
        menu.Icon = req.Icon;
        menu.ParentId = req.ParentId;
        menu.SortOrder = req.SortOrder;
        menu.PermissionCode = req.PermissionCode;
        menu.Component = req.Component;
        menu.OpenType = req.OpenType ?? "self";
        menu.IsVisible = req.IsVisible;

        await _db.SaveChangesAsync();
        return await GetMenuResponseByIdAsync(id);
    }

    public async Task<bool> DeleteMenuAsync(int id)
    {
        var menu = await _db.Menus.FindAsync(id);
        if (menu == null) return false;

        // 内置菜单不可删除
        if (menu.IsBuiltIn) return false;

        // 收集所有将被删除的后代 + 需要重挂载的内置后代
        var toDelete = new List<Menu>();
        var toReparent = new List<Menu>();
        CollectDescendants(id, toDelete, toReparent);

        // 内置后代（任意深度）：重新挂载到被删菜单的父级下保留
        var newParentId = menu.ParentId;
        foreach (var child in toReparent)
        {
            child.ParentId = newParentId;
        }

        // 删除所有非内置后代
        foreach (var child in toDelete)
        {
            _db.Menus.Remove(child);
        }

        // 删除目标菜单本身
        _db.Menus.Remove(menu);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 递归收集后代：非内置节点加入 toDelete 待删除，内置节点加入 toReparent 待重挂载。
    /// 遇到内置节点时不会递归进入其子节点（其子树整体保留）。
    /// </summary>
    private void CollectDescendants(int parentId, List<Menu> toDelete, List<Menu> toReparent)
    {
        var children = _db.Menus.Where(m => m.ParentId == parentId).ToList();
        foreach (var child in children)
        {
            if (child.IsBuiltIn)
            {
                // 内置节点：重挂载到祖先，不递归其子节点（子树保留）
                toReparent.Add(child);
            }
            else
            {
                // 非内置节点：递归深入，然后加入删除列表
                CollectDescendants(child.Id, toDelete, toReparent);
                toDelete.Add(child);
            }
        }
    }

    public async Task BatchUpdateMenusAsync(BatchUpdateMenusRequest req)
    {
        var ids = req.Menus.Select(m => m.Id).ToHashSet();
        var menus = await _db.Menus.Where(m => ids.Contains(m.Id)).ToListAsync();
        var map = menus.ToDictionary(m => m.Id);

        foreach (var item in req.Menus)
        {
            if (map.TryGetValue(item.Id, out var menu))
            {
                menu.ParentId = item.ParentId;
                menu.SortOrder = item.SortOrder;
            }
        }
        await _db.SaveChangesAsync();
    }

    private async Task<MenuResponse> GetMenuResponseByIdAsync(int id)
    {
        var menu = await _db.Menus.FindAsync(id);
        return new MenuResponse
        {
            Id = menu!.Id,
            Name = menu.Name,
            Path = menu.Path,
            Icon = menu.Icon,
            ParentId = menu.ParentId,
            SortOrder = menu.SortOrder,
            PermissionCode = menu.PermissionCode,
            Component = menu.Component,
            OpenType = menu.OpenType,
            IsVisible = menu.IsVisible,
            IsBuiltIn = menu.IsBuiltIn,
        };
    }

    // ========== 用户角色分配 ==========

    public async Task AssignUserRolesAsync(int userId, List<int> roleIds)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            throw new InvalidOperationException("用户不存在");

        // 删除旧的角色分配
        var existingRoles = _db.UserRoles.Where(ur => ur.UserId == userId);
        _db.UserRoles.RemoveRange(existingRoles);

        // 添加新的角色分配
        foreach (var roleId in roleIds)
        {
            if (await _db.Roles.AnyAsync(r => r.Id == roleId))
            {
                _db.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<string>> GetUserPermissionCodesAsync(int userId)
    {
        // 角色权限
        var roleCodes = await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync();

        // 直接权限
        var directCodes = await _db.UserPermissions
            .Where(up => up.UserId == userId)
            .Select(up => up.Permission.Code)
            .Distinct()
            .ToListAsync();

        return roleCodes.Union(directCodes).Distinct().ToList();
    }

    public async Task<UserWithRolesResponse?> GetUserWithRolesAsync(int userId)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return null;

        return new UserWithRolesResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            RoleIds = user.UserRoles.Select(ur => ur.RoleId).ToList(),
            RoleNames = user.UserRoles.Select(ur => ur.Role.Name).ToList()
        };
    }

    // ========== 直接用户权限管理 ==========

    /// <summary>获取用户的直接权限 ID 列表</summary>
    public async Task<List<int>> GetUserDirectPermissionsAsync(int userId)
    {
        return await _db.UserPermissions
            .Where(up => up.UserId == userId)
            .Select(up => up.PermissionId)
            .ToListAsync();
    }

    /// <summary>获取用户权限汇总（角色权限 + 直接权限）</summary>
    public async Task<UserPermissionSummaryResponse?> GetUserPermissionSummaryAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return null;

        var rolePermIds = await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToListAsync();

        var directPermIds = await _db.UserPermissions
            .Where(up => up.UserId == userId)
            .Select(up => up.PermissionId)
            .ToListAsync();

        return new UserPermissionSummaryResponse
        {
            UserId = user.Id,
            Username = user.Username,
            RolePermissionIds = rolePermIds,
            DirectPermissionIds = directPermIds,
            AllPermissionIds = rolePermIds.Union(directPermIds).Distinct().ToList()
        };
    }

    /// <summary>批量授予用户直接权限</summary>
    public async Task GrantUserPermissionsAsync(int userId, List<int> permissionIds, int grantedBy)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            throw new InvalidOperationException("用户不存在");

        foreach (var permId in permissionIds)
        {
            if (!await _db.Permissions.AnyAsync(p => p.Id == permId))
                continue;

            // 已存在则跳过
            var exists = await _db.UserPermissions.AnyAsync(up => up.UserId == userId && up.PermissionId == permId);
            if (!exists)
            {
                _db.UserPermissions.Add(new UserPermission
                {
                    UserId = userId,
                    PermissionId = permId,
                    GrantedBy = grantedBy,
                    GrantedAt = DateTime.Now
                });
            }
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>撤销用户直接权限</summary>
    public async Task RevokeUserPermissionsAsync(int userId, List<int> permissionIds)
    {
        var toRemove = await _db.UserPermissions
            .Where(up => up.UserId == userId && permissionIds.Contains(up.PermissionId))
            .ToListAsync();

        _db.UserPermissions.RemoveRange(toRemove);
        await _db.SaveChangesAsync();
    }

    // ========== 权限项 CRUD ==========

    /// <summary>分页获取权限列表（含角色数、直接用户数）</summary>
    public async Task<(List<PermissionPaginatedResponse> List, int Total)> GetAllPermissionsPaginatedAsync(
        string? keyword, int page = 1, int pageSize = 20)
    {
        var query = _db.Permissions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(kw)
                || p.Code.ToLower().Contains(kw)
                || (p.Description != null && p.Description.ToLower().Contains(kw)));
        }

        var total = await query.CountAsync();

        var list = await query
            .OrderBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PermissionPaginatedResponse
            {
                Id = p.Id,
                Name = p.Name,
                Code = p.Code,
                Description = p.Description,
                RoleCount = p.RolePermissions.Count,
                UserCount = 0 // 下面计算
            })
            .ToListAsync();

        // 补充直接用户数统计
        var permIds = list.Select(p => p.Id).ToList();
        var userCounts = await _db.UserPermissions
            .Where(up => permIds.Contains(up.PermissionId))
            .GroupBy(up => up.PermissionId)
            .Select(g => new { PermissionId = g.Key, Count = g.Count() })
            .ToListAsync();

        var countMap = userCounts.ToDictionary(c => c.PermissionId, c => c.Count);
        foreach (var item in list)
        {
            item.UserCount = countMap.TryGetValue(item.Id, out var cnt) ? cnt : 0;
        }

        return (list, total);
    }

    /// <summary>创建权限项</summary>
    public async Task<PermissionResponse> CreatePermissionAsync(CreatePermissionRequest request)
    {
        if (await _db.Permissions.AnyAsync(p => p.Code == request.Code))
            throw new InvalidOperationException("权限编码已存在");

        var perm = new Permission
        {
            Name = request.Name,
            Code = request.Code,
            Description = request.Description
        };

        _db.Permissions.Add(perm);
        await _db.SaveChangesAsync();

        return new PermissionResponse
        {
            Id = perm.Id,
            Name = perm.Name,
            Code = perm.Code,
            Description = perm.Description
        };
    }

    /// <summary>更新权限项</summary>
    public async Task<PermissionResponse?> UpdatePermissionAsync(int id, UpdatePermissionRequest request)
    {
        var perm = await _db.Permissions.FindAsync(id);
        if (perm == null) return null;

        if (await _db.Permissions.AnyAsync(p => p.Code == request.Code && p.Id != id))
            throw new InvalidOperationException("权限编码已被其他权限使用");

        perm.Name = request.Name;
        perm.Code = request.Code;
        perm.Description = request.Description;

        await _db.SaveChangesAsync();

        return new PermissionResponse
        {
            Id = perm.Id,
            Name = perm.Name,
            Code = perm.Code,
            Description = perm.Description
        };
    }

    /// <summary>删除权限项（同时清除关联的角色权限和直接用户权限）</summary>
    public async Task<bool> DeletePermissionAsync(int id)
    {
        var perm = await _db.Permissions.FindAsync(id);
        if (perm == null) return false;

        // 删除角色关联
        var rolePerms = _db.RolePermissions.Where(rp => rp.PermissionId == id);
        _db.RolePermissions.RemoveRange(rolePerms);

        // 删除直接用户权限关联
        var userPerms = _db.UserPermissions.Where(up => up.PermissionId == id);
        _db.UserPermissions.RemoveRange(userPerms);

        _db.Permissions.Remove(perm);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>获取所有权限编码列表（用于菜单权限选择）</summary>
    public async Task<List<PermissionResponse>> GetAllPermissionCodesAsync()
    {
        return await _db.Permissions
            .Select(p => new PermissionResponse
            {
                Id = p.Id,
                Name = p.Name,
                Code = p.Code,
                Description = p.Description
            })
            .ToListAsync();
    }
}
