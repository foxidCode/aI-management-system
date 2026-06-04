using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Services;

/// <summary>
/// 工作流引擎 — LogicFlow JSON 驱动的轻量级状态机
/// 支持 Workflow Core DSL 格式的输出（通过 WorkflowAdapter），执行使用自建引擎
/// </summary>
public class WorkflowEngine
{
    private readonly AppDbContext _db;

    public WorkflowEngine(AppDbContext db)
    {
        _db = db;
    }

    // ========== 启动流程 ==========

    public async Task<WorkflowInstance> StartWorkflowAsync(int definitionId, string moduleName, string relatedId, int userId)
    {
        var def = await _db.WorkflowDefinitions
            .Where(d => d.Id == definitionId && d.Status == "published")
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("流程定义不存在或未发布");

        // 验证可转为 Workflow Core DSL（确保格式正确）
        WorkflowAdapter.ConvertToWorkflowCoreDsl(def.NodeData, $"wf_{definitionId}", def.Version);

        // 创建实例
        var instance = new WorkflowInstance
        {
            DefinitionId = def.Id,
            DefinitionVersion = def.Version,
            ModuleName = moduleName,
            RelatedId = relatedId,
            CurrentNodeIds = "[]",
            Status = "running",
            StartedBy = userId,
            StartedAt = DateTime.Now,
            NodeData = def.NodeData,
        };
        _db.WorkflowInstances.Add(instance);
        await _db.SaveChangesAsync();

        // 推进到第一个审批节点
        await AdvanceFromStartAsync(instance, def.NodeData);
        return instance;
    }

    /// <summary>从开始节点推进到第一个实际节点</summary>
    private async Task AdvanceFromStartAsync(WorkflowInstance instance, string nodeDataJson)
    {
        var data = ParseNodeData(nodeDataJson);
        var startNode = data.Nodes.FirstOrDefault(n => n.Type == "start");
        if (startNode == null) return;

        var nextNodes = GetNextNodes(data, startNode.Id, "always");
        instance.CurrentNodeIds = JsonSerializer.Serialize(nextNodes.Select(n => n.Id));
        await _db.SaveChangesAsync();

        foreach (var node in nextNodes)
        {
            var nodeDef = data.Nodes.First(n => n.Id == node.Id);
            if (nodeDef.Type == "approval")
                await CreateTasksForNodeAsync(instance, nodeDef);
            if (nodeDef.Type == "cc")
                await CreateCcTasksAsync(instance, nodeDef);
            if (nodeDef.Type == "end")
            {
                instance.Status = "approved";
                instance.CompletedAt = DateTime.Now;
                instance.CurrentNodeIds = "[]";
                await _db.SaveChangesAsync();
            }
        }
    }

    // ========== 审批 ==========

    public async Task<string> ApproveAsync(int instanceId, string nodeId, int userId, string? comment)
    {
        var instance = await _db.WorkflowInstances.FindAsync(instanceId)
            ?? throw new InvalidOperationException("流程实例不存在");
        if (instance.Status != "running") throw new InvalidOperationException("流程已结束");

        var task = await _db.WorkflowTasks
            .Where(t => t.InstanceId == instanceId && t.NodeId == nodeId && t.AssigneeId == userId && t.Status == "pending")
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("您不是该节点的审批人");

        task.Status = "approved";
        task.Comment = comment;
        task.CompletedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        // 检查节点是否完成
        var data = ParseNodeData(instance.NodeData);
        var node = data.Nodes.FirstOrDefault(n => n.Id == nodeId)
            ?? throw new InvalidOperationException("节点不存在");
        var strategy = GetConfigValue(node.Config, "strategy") ?? "any";

        var allTasks = await _db.WorkflowTasks
            .Where(t => t.InstanceId == instanceId && t.NodeId == nodeId).ToListAsync();
        bool nodeCompleted = strategy == "all"
            ? allTasks.All(t => t.Status == "approved")
            : allTasks.Any(t => t.Status == "approved");

        if (!nodeCompleted)
            return $"审批通过，等待其他审批人（{allTasks.Count(t => t.Status == "approved")}/{allTasks.Count}）";

        // 推进到下一节点
        var nextNodes = GetNextNodes(data, nodeId, "approved");
        if (nextNodes.Count == 0)
        {
            instance.Status = "approved"; instance.CompletedAt = DateTime.Now;
            instance.CurrentNodeIds = "[]"; await _db.SaveChangesAsync();
            return "流程审批已通过 ✓";
        }

        instance.CurrentNodeIds = JsonSerializer.Serialize(nextNodes.Select(n => n.Id));
        await _db.SaveChangesAsync();

        foreach (var next in nextNodes)
        {
            var nextNode = data.Nodes.First(n => n.Id == next.Id);
            if (nextNode.Type == "approval") await CreateTasksForNodeAsync(instance, nextNode);
            if (nextNode.Type == "cc") await CreateCcTasksAsync(instance, nextNode);
            if (nextNode.Type == "end")
            {
                instance.Status = "approved"; instance.CompletedAt = DateTime.Now;
                instance.CurrentNodeIds = "[]"; await _db.SaveChangesAsync();
                return "流程审批已通过 ✓";
            }
        }
        return "已推进到下一节点";
    }

    // ========== 驳回 ==========

    public async Task<string> RejectAsync(int instanceId, string nodeId, int userId, string? comment)
    {
        var instance = await _db.WorkflowInstances.FindAsync(instanceId)
            ?? throw new InvalidOperationException("流程实例不存在");
        if (instance.Status != "running") throw new InvalidOperationException("流程已结束");

        var task = await _db.WorkflowTasks
            .Where(t => t.InstanceId == instanceId && t.NodeId == nodeId && t.AssigneeId == userId && t.Status == "pending")
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("您不是该节点的审批人");

        task.Status = "rejected"; task.Comment = comment; task.CompletedAt = DateTime.Now;
        instance.Status = "rejected"; instance.CompletedAt = DateTime.Now;
        instance.CurrentNodeIds = "[]";
        await _db.SaveChangesAsync();

        // 取消同节点其他待办
        var others = await _db.WorkflowTasks
            .Where(t => t.InstanceId == instanceId && t.NodeId == nodeId && t.Status == "pending").ToListAsync();
        foreach (var t in others) { t.Status = "rejected"; t.CompletedAt = DateTime.Now; }
        await _db.SaveChangesAsync();

        return "已驳回";
    }

    public async Task<string> RecallAsync(int instanceId, int userId)
    {
        var instance = await _db.WorkflowInstances.FindAsync(instanceId)
            ?? throw new InvalidOperationException("流程实例不存在");
        if (instance.StartedBy != userId) throw new InvalidOperationException("只有发起人可撤回");
        if (instance.Status != "running") throw new InvalidOperationException("只能撤回运行中的流程");

        instance.Status = "recalled"; instance.CompletedAt = DateTime.Now;
        instance.CurrentNodeIds = "[]";
        var pending = await _db.WorkflowTasks.Where(t => t.InstanceId == instanceId && t.Status == "pending").ToListAsync();
        foreach (var t in pending) { t.Status = "rejected"; t.CompletedAt = DateTime.Now; }
        await _db.SaveChangesAsync();
        return "已撤回";
    }

    // ========== 辅助 ==========

    private async Task CreateTasksForNodeAsync(WorkflowInstance instance, NodeDef node)
    {
        var approverType = GetConfigValue(node.Config, "approverType") ?? "role";
        var approverValue = GetConfigValue(node.Config, "approverValue") ?? "";
        var assigneeIds = approverType == "user"
            ? new() { int.TryParse(approverValue, out var uid) ? uid : 0 }
            : await _db.UserRoles.Where(ur => ur.RoleId.ToString() == approverValue).Select(ur => ur.UserId).ToListAsync();

        foreach (var aid in assigneeIds.Where(id => id > 0))
            _db.WorkflowTasks.Add(new WorkflowTask { InstanceId = instance.Id, NodeId = node.Id, NodeName = node.Label ?? node.Type, TaskType = "approval", AssigneeId = aid, Status = "pending", CreatedAt = DateTime.Now });
        await _db.SaveChangesAsync();
    }

    private async Task CreateCcTasksAsync(WorkflowInstance instance, NodeDef node)
    {
        var raw = GetConfigValue(node.Config, "ccUserIds");
        if (string.IsNullOrEmpty(raw)) return;
        var ids = raw.Split(',').Select(s => int.TryParse(s.Trim(), out var id) ? id : 0).Where(id => id > 0);
        foreach (var aid in ids)
            _db.WorkflowTasks.Add(new WorkflowTask { InstanceId = instance.Id, NodeId = node.Id, NodeName = node.Label ?? node.Type, TaskType = "cc", AssigneeId = aid, Status = "pending", CreatedAt = DateTime.Now });
        await _db.SaveChangesAsync();
    }

    // ========== JSON 工具 ==========

    public static NodeData ParseNodeData(string json)
    {
        try { var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true }; return JsonSerializer.Deserialize<NodeData>(json, opts) ?? new(); }
        catch (Exception ex) { throw new InvalidOperationException($"NodeData解析失败: {ex.Message}", ex); }
    }

    internal static string? GetConfigValue(JsonElement element, string key)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (element.TryGetProperty(key, out var v))
            return v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.Number => v.GetRawText(), JsonValueKind.True => "true", JsonValueKind.False => "false", _ => null };
        return null;
    }

    private static List<NodeDef> GetNextNodes(NodeData data, string sourceNodeId, string condition)
    {
        var targets = data.Edges.Where(e => e.Source == sourceNodeId && (e.Condition == condition || e.Condition == "always")).Select(e => e.Target).ToList();
        return data.Nodes.Where(n => targets.Contains(n.Id)).ToList();
    }
}

// ========== JSON 反序列化类 ==========

public class NodeData { public List<NodeDef> Nodes { get; set; } = new(); public List<EdgeDef> Edges { get; set; } = new(); }
public class NodeDef { public string Id { get; set; } = ""; public string Type { get; set; } = ""; public string? Label { get; set; } public double X { get; set; } public double Y { get; set; } public JsonElement Config { get; set; } public string? Expression { get; set; } }
public class EdgeDef { public string Source { get; set; } = ""; public string Target { get; set; } = ""; public string Condition { get; set; } = "always"; }
