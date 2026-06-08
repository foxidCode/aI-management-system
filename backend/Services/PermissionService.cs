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
        // 获取用户的所有权限编码
        var permissionCodes = await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync();

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

        // 递归删除子菜单
        var children = await _db.Menus.Where(m => m.ParentId == id).ToListAsync();
        foreach (var child in children)
            await DeleteMenuAsync(child.Id);

        _db.Menus.Remove(menu);
        await _db.SaveChangesAsync();
        return true;
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
        return await _db.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync();
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
}
