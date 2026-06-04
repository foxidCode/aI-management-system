using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Models;

namespace backend.Services;

public class WorkflowService
{
    private readonly AppDbContext _db;

    public WorkflowService(AppDbContext db) => _db = db;

    // ========== WorkflowDefinition CRUD ==========

    public async Task<List<WorkflowDefinitionResponse>> GetDefinitionsAsync(string? category = null)
    {
        var query = _db.WorkflowDefinitions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(d => d.Category == category);

        var defs = await query.OrderByDescending(d => d.Id).ToListAsync();

        // 计算运行中实例数
        var runningCounts = await _db.WorkflowInstances
            .Where(i => i.Status == "running")
            .GroupBy(i => i.DefinitionId)
            .Select(g => new { DefinitionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DefinitionId, x => x.Count);

        return defs.Select(d => new WorkflowDefinitionResponse
        {
            Id = d.Id,
            Name = d.Name,
            Category = d.Category,
            Version = d.Version,
            Status = d.Status,
            NodeData = d.NodeData,
            CreatedAt = d.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            UpdatedAt = d.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            RunningInstanceCount = runningCounts.GetValueOrDefault(d.Id, 0),
        }).ToList();
    }

    public async Task<WorkflowDefinitionResponse> GetDefinitionAsync(int id)
    {
        var d = await _db.WorkflowDefinitions.FindAsync(id)
            ?? throw new InvalidOperationException("流程定义不存在");

        var runningCount = await _db.WorkflowInstances
            .CountAsync(i => i.DefinitionId == id && i.Status == "running");

        return new WorkflowDefinitionResponse
        {
            Id = d.Id, Name = d.Name, Category = d.Category,
            Version = d.Version, Status = d.Status, NodeData = d.NodeData,
            CreatedAt = d.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            UpdatedAt = d.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            RunningInstanceCount = runningCount,
        };
    }

    public async Task<WorkflowDefinitionResponse> CreateDefinitionAsync(CreateWorkflowRequest req)
    {
        var def = new WorkflowDefinition
        {
            Name = req.Name, Category = req.Category,
            Version = 1, Status = "draft", NodeData = req.NodeData,
            CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now,
        };
        _db.WorkflowDefinitions.Add(def);
        await _db.SaveChangesAsync();
        return await GetDefinitionAsync(def.Id);
    }

    public async Task<WorkflowDefinitionResponse> UpdateDefinitionAsync(int id, UpdateWorkflowRequest req)
    {
        var def = await _db.WorkflowDefinitions.FindAsync(id)
            ?? throw new InvalidOperationException("流程定义不存在");

        // 已发布的流程：自动创建新版本
        if (def.Status == "published")
        {
            var latestVersion = await _db.WorkflowDefinitions
                .Where(d => d.Name == def.Name && d.Category == def.Category)
                .MaxAsync(d => (int?)d.Version) ?? 0;

            var newDef = new WorkflowDefinition
            {
                Name = req.Name, Category = req.Category,
                Version = latestVersion + 1, Status = "draft", NodeData = req.NodeData,
                CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now,
            };
            _db.WorkflowDefinitions.Add(newDef);
            await _db.SaveChangesAsync();
            return await GetDefinitionAsync(newDef.Id);
        }

        // 草稿：直接修改
        def.Name = req.Name; def.Category = req.Category;
        def.NodeData = req.NodeData; def.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return await GetDefinitionAsync(def.Id);
    }

    public async Task<string> PublishAsync(int id)
    {
        var def = await _db.WorkflowDefinitions.FindAsync(id)
            ?? throw new InvalidOperationException("流程定义不存在");

        // 校验 NodeData 有效性
        try
        {
            var nodeData = WorkflowEngine.ParseNodeData(def.NodeData);
            if (!nodeData.Nodes.Any(n => n.Type == "start"))
                throw new InvalidOperationException("流程必须包含开始节点");
            if (!nodeData.Nodes.Any(n => n.Type == "end"))
                throw new InvalidOperationException("流程必须包含结束节点");
            if (!nodeData.Nodes.Any(n => n.Type == "approval"))
                throw new InvalidOperationException("流程至少需要一个审批节点");
        }
        catch (InvalidOperationException) { throw; }
        catch { throw new InvalidOperationException("流程节点数据格式无效"); }

        def.Status = "published";
        def.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        // 归档同 Name+Category 的其他已发布版本
        var others = await _db.WorkflowDefinitions
            .Where(d => d.Name == def.Name && d.Category == def.Category && d.Id != def.Id && d.Status == "published")
            .ToListAsync();
        foreach (var o in others) { o.Status = "archived"; o.UpdatedAt = DateTime.Now; }
        await _db.SaveChangesAsync();

        return "已发布";
    }

    public async Task DeleteDefinitionAsync(int id)
    {
        var def = await _db.WorkflowDefinitions.FindAsync(id)
            ?? throw new InvalidOperationException("流程定义不存在");
        if (def.Status == "published")
            throw new InvalidOperationException("已发布的流程不可删除，请先归档");

        var hasRunning = await _db.WorkflowInstances.AnyAsync(i => i.DefinitionId == id && i.Status == "running");
        if (hasRunning)
            throw new InvalidOperationException("该流程有运行中的实例，无法删除");

        _db.WorkflowDefinitions.Remove(def);
        await _db.SaveChangesAsync();
    }

    // ========== 待办/已办/我的申请 ==========

    public async Task<List<WorkflowTaskResponse>> GetTodoTasksAsync(int userId)
    {
        return await _db.WorkflowTasks
            .Where(t => t.AssigneeId == userId && t.Status == "pending")
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => ToTaskResponse(t))
            .ToListAsync();
    }

    public async Task<List<WorkflowTaskResponse>> GetDoneTasksAsync(int userId)
    {
        return await _db.WorkflowTasks
            .Where(t => t.AssigneeId == userId && t.Status != "pending")
            .OrderByDescending(t => t.CompletedAt)
            .Take(100)
            .Select(t => ToTaskResponse(t))
            .ToListAsync();
    }

    public async Task<List<WorkflowTaskResponse>> GetMyApplicationsAsync(int userId)
    {
        var instanceIds = await _db.WorkflowInstances
            .Where(i => i.StartedBy == userId)
            .OrderByDescending(i => i.StartedAt)
            .Take(100)
            .Select(i => i.Id)
            .ToListAsync();

        var tasks = await _db.WorkflowTasks
            .Where(t => instanceIds.Contains(t.InstanceId) && t.TaskType == "approval")
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => ToTaskResponse(t))
            .ToListAsync();

        return tasks;
    }

    // ========== 实例管理 ==========

    public async Task<List<WorkflowInstanceResponse>> GetInstancesAsync(string? status = null, int page = 1, int pageSize = 20)
    {
        var query = _db.WorkflowInstances.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(i => i.Status == status);

        var instances = await query.OrderByDescending(i => i.StartedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        // 批量获取用户名
        var userIds = instances.Select(i => i.StartedBy).Distinct().ToList();
        var userNames = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
        var defNames = await _db.WorkflowDefinitions.ToDictionaryAsync(d => d.Id, d => d.Name);

        return instances.Select(i => new WorkflowInstanceResponse
        {
            Id = i.Id, DefinitionId = i.DefinitionId,
            DefinitionName = defNames.GetValueOrDefault(i.DefinitionId, ""),
            DefinitionVersion = i.DefinitionVersion,
            ModuleName = i.ModuleName, RelatedId = i.RelatedId,
            CurrentNodeIds = i.CurrentNodeIds, Status = i.Status,
            StartedByName = userNames.GetValueOrDefault(i.StartedBy, ""),
            StartedAt = i.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            CompletedAt = i.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
            NodeData = i.NodeData,
        }).ToList();
    }

    public async Task<WorkflowInstanceResponse> GetInstanceAsync(int id)
    {
        var i = await _db.WorkflowInstances.FindAsync(id)
            ?? throw new InvalidOperationException("流程实例不存在");

        var userName = (await _db.Users.FindAsync(i.StartedBy))?.Username ?? "";
        var defName = (await _db.WorkflowDefinitions.FindAsync(i.DefinitionId))?.Name ?? "";

        var tasks = await _db.WorkflowTasks
            .Where(t => t.InstanceId == id)
            .OrderBy(t => t.CreatedAt)
            .Select(t => ToTaskResponse(t))
            .ToListAsync();

        return new WorkflowInstanceResponse
        {
            Id = i.Id, DefinitionId = i.DefinitionId,
            DefinitionName = defName, DefinitionVersion = i.DefinitionVersion,
            ModuleName = i.ModuleName, RelatedId = i.RelatedId,
            CurrentNodeIds = i.CurrentNodeIds, Status = i.Status,
            StartedByName = userName,
            StartedAt = i.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            CompletedAt = i.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
            NodeData = i.NodeData,
            Tasks = tasks,
        };
    }

    // ========== 统计 ==========

    public async Task<WorkflowStatsResponse> GetStatsAsync(int? userId = null)
    {
        return new WorkflowStatsResponse
        {
            TotalDefinitions = await _db.WorkflowDefinitions.CountAsync(),
            PublishedDefinitions = await _db.WorkflowDefinitions.CountAsync(d => d.Status == "published"),
            RunningInstances = await _db.WorkflowInstances.CountAsync(i => i.Status == "running"),
            CompletedToday = await _db.WorkflowInstances.CountAsync(i =>
                i.CompletedAt != null && i.Status == "approved" &&
                i.CompletedAt.Value.Date == DateTime.Now.Date),
            MyPendingTasks = userId.HasValue
                ? await _db.WorkflowTasks.CountAsync(t => t.AssigneeId == userId.Value && t.Status == "pending")
                : 0,
        };
    }

    // ========== 映射 ==========

    private static WorkflowTaskResponse ToTaskResponse(WorkflowTask t)
    {
        // 解析 AssigneeName 和 Instance 关联信息（调用方需确保 Include/Join 或单独查询）
        return new WorkflowTaskResponse
        {
            Id = t.Id, InstanceId = t.InstanceId,
            NodeId = t.NodeId, NodeName = t.NodeName,
            TaskType = t.TaskType, AssigneeId = t.AssigneeId,
            AssigneeName = "", // 由调用方填充
            Status = t.Status, Comment = t.Comment,
            IsRead = t.IsRead,
            CreatedAt = t.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            CompletedAt = t.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
        };
    }

    // 批量填充用户名和实例信息
    public async Task<List<WorkflowTaskResponse>> EnrichTaskResponsesAsync(List<WorkflowTaskResponse> tasks)
    {
        if (tasks.Count == 0) return tasks;

        var userIds = tasks.Select(t => t.AssigneeId).Distinct().ToList();
        var userNames = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);

        var instanceIds = tasks.Select(t => t.InstanceId).Distinct().ToList();
        var instances = await _db.WorkflowInstances.Where(i => instanceIds.Contains(i.Id)).ToListAsync();
        var defIds = instances.Select(i => i.DefinitionId).Distinct().ToList();
        var defNames = await _db.WorkflowDefinitions.Where(d => defIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);

        foreach (var t in tasks)
        {
            t.AssigneeName = userNames.GetValueOrDefault(t.AssigneeId, "");
            var inst = instances.FirstOrDefault(i => i.Id == t.InstanceId);
            if (inst != null)
            {
                t.InstanceModuleName = inst.ModuleName;
                t.InstanceRelatedId = inst.RelatedId;
                t.DefinitionName = defNames.GetValueOrDefault(inst.DefinitionId, "");
            }
        }
        return tasks;
    }
}
