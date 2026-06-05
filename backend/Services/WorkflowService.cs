using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Services;

public class WorkflowService
{
    private readonly AppDbContext _db;
    private readonly NotificationService? _notify;

    public WorkflowService(AppDbContext db, NotificationService? notify = null)
    {
        _db = db;
        _notify = notify;
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // ==================== 便捷反序列化 ====================

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<JsonElement>(json, JsonOpts); }
        catch { return null; }
    }

    // ==================== 提交实例 ====================

    public async Task<WorkflowInstance> SubmitInstanceAsync(int definitionId, string? formDataJson, int applicantId)
    {
        var def = await _db.WorkflowDefinitions.FindAsync(definitionId)
            ?? throw new InvalidOperationException("流程定义不存在");

        if (!def.IsActive)
            throw new InvalidOperationException("流程定义未发布");

        var nodesJson = ParseJson(def.Nodes);
        if (nodesJson == null || nodesJson.Value.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("流程定义缺少节点数据");

        var nodes = nodesJson.Value.EnumerateArray()
            .Select(n => DeserializeFlowNode(n.GetRawText()))
            .Where(n => n != null)
            .ToList();

        var startNode = nodes.FirstOrDefault(n => n!.NodeType == 1)
            ?? throw new InvalidOperationException("流程定义缺少开始节点");

        var firstNodeIds = startNode.NodeTo ?? new List<string>();
        if (firstNodeIds.Count == 0)
            throw new InvalidOperationException("开始节点未连接后续节点");

        var instance = new WorkflowInstance
        {
            DefinitionId = definitionId,
            DefinitionName = def.Name,
            Version = def.Version,
            ApplicantId = applicantId,
            FormData = formDataJson,
            Status = "Running",
            CurrentNodeId = firstNodeIds.FirstOrDefault(),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        _db.WorkflowInstances.Add(instance);
        await _db.SaveChangesAsync();

        // 记录提交历史
        var applicant = await _db.Users.FindAsync(applicantId);
        _db.WorkflowHistories.Add(new WorkflowHistory
        {
            InstanceId = instance.Id,
            ActionType = "submitted",
            ActorId = applicantId,
            ActorName = applicant?.Username ?? "",
            Comment = null,
            FormDataSnapshot = formDataJson,
            FromNodeId = startNode.NodeId,
            ToNodeId = firstNodeIds.FirstOrDefault(),
            CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();

        // 创建第一个节点的任务
        foreach (var nodeId in firstNodeIds)
        {
            await CreateTasksFromNodeAsync(instance.Id, nodeId, nodes, formDataJson);
        }

        // 如果起始节点有多个出口且都没创建任务，检查是否已完成
        var hasTasks = await _db.WorkflowTasks.AnyAsync(t => t.InstanceId == instance.Id && t.Status == "Pending");
        if (!hasTasks)
        {
            instance.Status = "Completed";
            instance.CompletedAt = DateTime.Now;
            instance.CurrentNodeId = null;
            instance.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }

        return instance;
    }

    // ==================== 审批通过 ====================

    public async Task ApproveTaskAsync(int taskId, string? comment, string? formDataJson, int actorId)
    {
        var task = await _db.WorkflowTasks
            .Include(t => t.Instance)
            .FirstOrDefaultAsync(t => t.Id == taskId)
            ?? throw new InvalidOperationException("任务不存在");

        if (task.Status != "Pending")
            throw new InvalidOperationException("该任务已处理");

        if (task.AssigneeId != actorId)
            throw new InvalidOperationException("您不是该任务的审批人");

        var instance = task.Instance!;
        var def = await _db.WorkflowDefinitions.FindAsync(instance.DefinitionId)
            ?? throw new InvalidOperationException("流程定义不存在");

        var nodes = ParseNodeList(def.Nodes);
        if (nodes == null) throw new InvalidOperationException("流程定义缺少节点数据");

        // 更新表单数据（如果有）
        if (!string.IsNullOrWhiteSpace(formDataJson))
        {
            instance.FormData = formDataJson;
        }

        // 完成任务
        task.Status = "Completed";
        task.Comment = comment;
        task.CompletedAt = DateTime.Now;
        instance.UpdatedAt = DateTime.Now;

        // 记录历史
        var actor = await _db.Users.FindAsync(actorId);
        var currentNode = nodes.FirstOrDefault(n => n?.NodeId == task.NodeId);
        var nextNodeIds = currentNode?.NodeTo ?? new List<string>();

        _db.WorkflowHistories.Add(new WorkflowHistory
        {
            InstanceId = instance.Id,
            TaskId = task.Id,
            ActionType = "approved",
            ActorId = actorId,
            ActorName = actor?.Username ?? "",
            Comment = comment,
            FormDataSnapshot = instance.FormData,
            FromNodeId = task.NodeId,
            ToNodeId = nextNodeIds.FirstOrDefault(),
            CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();

        // 检查并行节点：当前节点是否属于并行分支
        if (currentNode != null && currentNode.NodeType == 7) // ParallelApproveNode
        {
            // 找到父并行网关，检查所有并行子任务是否都完成了
            var parentWayNode = nodes.FirstOrDefault(n => n?.NodeType == 5 && n!.NodeTo?.Contains(currentNode.NodeId) == true);
            if (parentWayNode != null)
            {
                var parallelNodeIds = parentWayNode.NodeTo ?? new List<string>();
                bool allDone = true;
                foreach (var pnId in parallelNodeIds)
                {
                    var pTask = await _db.WorkflowTasks
                        .FirstOrDefaultAsync(t => t.InstanceId == instance.Id && t.NodeId == pnId && t.Status == "Pending");
                    if (pTask != null) { allDone = false; break; }
                }

                if (!allDone)
                {
                    // 还有并行任务未完成，不流转
                    await _db.SaveChangesAsync();
                    return;
                }

                // 所有并行任务完成，找到网关后的节点
                nextNodeIds = parentWayNode.NodeTo?.Except(parallelNodeIds).ToList() ?? new List<string>();
            }
        }

        // 流转到下一节点
        if (nextNodeIds.Count == 0)
        {
            // 无下一节点，流程结束
            instance.Status = "Completed";
            instance.CompletedAt = DateTime.Now;
            instance.CurrentNodeId = null;
            instance.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            if (_notify != null) _ = _notify.NotifyInstanceStatusAsync(instance.ApplicantId, instance.DefinitionName, "Completed", instance.Id);
            return;
        }

        instance.CurrentNodeId = nextNodeIds.FirstOrDefault();
        await _db.SaveChangesAsync();

        // 为下一节点创建任务
        foreach (var nodeId in nextNodeIds)
        {
            await CreateTasksFromNodeAsync(instance.Id, nodeId, nodes, instance.FormData);
        }

        // 检查是否创建了新任务
        var hasPending = await _db.WorkflowTasks.AnyAsync(t => t.InstanceId == instance.Id && t.Status == "Pending");
        if (!hasPending)
        {
            instance.Status = "Completed";
            instance.CompletedAt = DateTime.Now;
            instance.CurrentNodeId = null;
            instance.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            if (_notify != null) _ = _notify.NotifyInstanceStatusAsync(instance.ApplicantId, instance.DefinitionName, "Completed", instance.Id);
        }
    }

    // ==================== 驳回 ====================

    public async Task RejectTaskAsync(int taskId, string? comment, int actorId)
    {
        var task = await _db.WorkflowTasks
            .Include(t => t.Instance)
            .FirstOrDefaultAsync(t => t.Id == taskId)
            ?? throw new InvalidOperationException("任务不存在");

        if (task.Status != "Pending")
            throw new InvalidOperationException("该任务已处理");

        if (task.AssigneeId != actorId)
            throw new InvalidOperationException("您不是该任务的审批人");

        var instance = task.Instance!;

        // 完成任务
        task.Status = "Completed";
        task.Comment = comment;
        task.CompletedAt = DateTime.Now;

        // 取消所有其他待处理任务
        var otherTasks = await _db.WorkflowTasks
            .Where(t => t.InstanceId == instance.Id && t.Status == "Pending" && t.Id != taskId)
            .ToListAsync();
        foreach (var ot in otherTasks)
        {
            ot.Status = "Cancelled";
            ot.CompletedAt = DateTime.Now;
        }

        // 更新实例状态
        instance.Status = "Rejected";
        instance.CompletedAt = DateTime.Now;
        instance.CurrentNodeId = null;
        instance.UpdatedAt = DateTime.Now;

        // 记录历史
        var actor = await _db.Users.FindAsync(actorId);
        _db.WorkflowHistories.Add(new WorkflowHistory
        {
            InstanceId = instance.Id,
            TaskId = task.Id,
            ActionType = "rejected",
            ActorId = actorId,
            ActorName = actor?.Username ?? "",
            Comment = comment,
            FormDataSnapshot = instance.FormData,
            FromNodeId = task.NodeId,
            CreatedAt = DateTime.Now
        });

        await _db.SaveChangesAsync();
        if (_notify != null) _ = _notify.NotifyInstanceStatusAsync(instance.ApplicantId, instance.DefinitionName, "Rejected", instance.Id);
    }

    // ==================== 转办 ====================

    public async Task TransferTaskAsync(int taskId, int toUserId, string? comment, int actorId)
    {
        var task = await _db.WorkflowTasks
            .Include(t => t.Instance)
            .FirstOrDefaultAsync(t => t.Id == taskId)
            ?? throw new InvalidOperationException("任务不存在");

        if (task.Status != "Pending")
            throw new InvalidOperationException("该任务已处理");

        if (task.AssigneeId != actorId)
            throw new InvalidOperationException("您不是该任务的审批人");

        var toUser = await _db.Users.FindAsync(toUserId)
            ?? throw new InvalidOperationException("目标用户不存在");

        var actor = await _db.Users.FindAsync(actorId);

        // 记录转办历史
        var instance = task.Instance!;
        _db.WorkflowHistories.Add(new WorkflowHistory
        {
            InstanceId = instance.Id,
            TaskId = task.Id,
            ActionType = "transferred",
            ActorId = actorId,
            ActorName = actor?.Username ?? "",
            Comment = $"转办给 {toUser.Username}" + (string.IsNullOrEmpty(comment) ? "" : $": {comment}"),
            FormDataSnapshot = instance.FormData,
            FromNodeId = task.NodeId,
            CreatedAt = DateTime.Now
        });

        // 更改任务负责人
        task.AssigneeId = toUserId;
        task.AssigneeType = 1; // 转办后变为指定成员
        task.Comment = comment;

        await _db.SaveChangesAsync();
    }

    // ==================== 核心：递归创建任务 ====================

    private async Task CreateTasksFromNodeAsync(int instanceId, string nodeId, List<FlowNode?> nodes, string? formDataJson)
    {
        var node = nodes.FirstOrDefault(n => n?.NodeId == nodeId);
        if (node == null) return;

        var instance = await _db.WorkflowInstances.FindAsync(instanceId);
        if (instance == null) return;

        switch (node.NodeType)
        {
            case 4: // ApproveNode
            case 6: // CopyNode
            case 7: // ParallelApproveNode
                await CreateAssigneeTasksAsync(instanceId, node, formDataJson);
                // 继续处理 childNode（线性链）
                if (node.NodeTo?.Count > 0)
                {
                    foreach (var toId in node.NodeTo)
                    {
                        // nodeTo 可能指向条件节点或下一审批节点，但不在 childNode 里的不应递归
                        // 这里我们按 nodeTo 但排除已经在其他递归中处理的
                    }
                }
                break;

            case 2: // GatewayNode - 条件网关
                await ProcessGatewayAsync(instanceId, node, nodes, formDataJson);
                break;

            case 5: // ParallelApproveWayNode - 并行网关
                             // 为每个并行分支创建任务
                if (node.NodeTo?.Count > 0)
                {
                    foreach (var toId in node.NodeTo)
                    {
                        var childNode = nodes.FirstOrDefault(n => n?.NodeId == toId);
                        if (childNode != null && childNode.NodeType == 7) // ParallelApproveNode
                        {
                            await CreateAssigneeTasksAsync(instanceId, childNode, formDataJson);
                        }
                    }
                }
                break;
        }
    }

    /// <summary>
    /// 处理条件网关：评估各条件分支
    /// </summary>
    private async Task ProcessGatewayAsync(int instanceId, FlowNode gateway, List<FlowNode?> nodes, string? formDataJson)
    {
        var formData = ParseJson(formDataJson);
        var conditionNodes = new List<FlowNode?>();

        // 从 nodes 中找出属于该网关的条件节点
        if (gateway.NodeTo?.Count > 0)
        {
            foreach (var toId in gateway.NodeTo)
            {
                var child = nodes.FirstOrDefault(n => n?.NodeId == toId && n?.NodeType == 3);
                if (child != null) conditionNodes.Add(child);
            }
        }

        FlowNode? matchedCondition = null;
        FlowNode? defaultCondition = null;

        foreach (var cn in conditionNodes)
        {
            if (cn == null) continue;
            if (cn.NodeProperty?.IsDefault == 1)
            {
                defaultCondition = cn;
                continue;
            }

            // 评估条件
            if (EvaluateCondition(cn, formData))
            {
                matchedCondition = cn;
                break;
            }
        }

        // 命中的条件分支或默认分支
        var targetCondition = matchedCondition ?? defaultCondition;
        if (targetCondition?.NodeTo?.Count > 0)
        {
            foreach (var toId in targetCondition.NodeTo)
            {
                await CreateTasksFromNodeAsync(instanceId, toId, nodes, formDataJson);
            }
        }
    }

    /// <summary>
    /// 为审批/抄送节点创建任务
    /// </summary>
    private async Task CreateAssigneeTasksAsync(int instanceId, FlowNode node, string? formDataJson)
    {
        var assigneeIds = await ResolveAssigneesAsync(node, instanceId);
        var actionType = node.NodeType switch
        {
            6 => "copy",  // CopyNode
            _ => null
        };

        var instance = await _db.WorkflowInstances.FindAsync(instanceId);
        foreach (var aId in assigneeIds)
        {
            _db.WorkflowTasks.Add(new WorkflowTask
            {
                InstanceId = instanceId,
                NodeId = node.NodeId ?? "",
                NodeName = node.NodeDisplayName ?? node.NodeName,
                NodeType = node.NodeType,
                AssigneeId = aId,
                AssigneeType = node.NodeProperty?.AssigneeType ?? 1,
                ActionType = actionType,
                Status = "Pending",
                FormData = formDataJson,
                CreatedAt = DateTime.Now
            });
        }

        await _db.SaveChangesAsync();

        // 通知新任务（非抄送节点）
        if (_notify != null && actionType != "copy" && instance != null)
        {
            foreach (var aId in assigneeIds)
            {
                _ = _notify.NotifyNewTaskAsync(aId, instance.DefinitionName, node.NodeDisplayName ?? node.NodeName ?? "审批节点", instanceId);
            }
        }
    }

    // ==================== 审批人解析 ====================

    private async Task<List<int>> ResolveAssigneesAsync(FlowNode node, int instanceId)
    {
        var instance = (await _db.WorkflowInstances.FindAsync(instanceId))!;
        var assigneeType = node.NodeProperty?.AssigneeType ?? 1;
        var assigneeList = node.NodeProperty?.AssigneeList ?? new List<AssigneeInfo>();
        var ids = new List<int>();

        switch (assigneeType)
        {
            case 1: // 指定成员
                foreach (var a in assigneeList)
                {
                    var uid = a.GetTargetIdAsInt();
                    if (uid > 0 && !ids.Contains(uid))
                        ids.Add(uid);
                }
                break;

            case 2: // 指定角色
                foreach (var a in assigneeList)
                {
                    var rid = a.GetTargetIdAsInt();
                    if (rid > 0)
                    {
                        var userIds = await _db.UserRoles
                            .Where(ur => ur.RoleId == rid)
                            .Select(ur => ur.UserId)
                            .ToListAsync();
                        foreach (var uid in userIds)
                        {
                            if (!ids.Contains(uid)) ids.Add(uid);
                        }
                    }
                }
                break;

            case 3: // 直属领导
                var applicant = await _db.Users.FindAsync(instance.ApplicantId);
                if (applicant?.LeaderId != null)
                    ids.Add(applicant.LeaderId.Value);
                break;

            case 5: // 发起人自己
                ids.Add(instance.ApplicantId);
                break;

            case 6: // 指定层级（沿领导链向上N层）
                var directorLevel = node.NodeProperty?.DirectorLevel ?? 1;
                var current = await _db.Users.FindAsync(instance.ApplicantId);
                for (int i = 0; i < directorLevel && current?.LeaderId != null; i++)
                {
                    current = await _db.Users.FindAsync(current.LeaderId.Value);
                }
                if (current != null && current.Id != instance.ApplicantId)
                    ids.Add(current.Id);
                break;

            case 7: // 层层审批（暂简化为直属领导）
                var app = await _db.Users.FindAsync(instance.ApplicantId);
                if (app?.LeaderId != null)
                    ids.Add(app.LeaderId.Value);
                break;

            case 8: // 发起人自选
                     // 从 formData.__approvers__[nodeId] 读取
                var formData = ParseJson(instance.FormData);
                if (formData?.TryGetProperty("__approvers__", out var approvers) == true
                    && approvers.TryGetProperty(node.NodeId ?? "", out var nodeApprovers))
                {
                    foreach (var item in nodeApprovers.EnumerateArray())
                    {
                        if (item.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var sid))
                            ids.Add(sid);
                    }
                }
                break;
        }

        // 去重
        if (node.NodeProperty?.IsDistinct == 1)
        {
            ids = ids.Distinct().ToList();
        }

        return ids;
    }

    // ==================== 条件评估 ====================

    private static bool EvaluateCondition(FlowNode conditionNode, JsonElement? formData)
    {
        if (formData == null) return false;

        try
        {
            var groups = conditionNode.NodeProperty?.GroupConditions;
            if (groups == null || groups.Count == 0) return false;

            // groupRelation: 各组之间的关系 (true=AND, false=OR)
            bool groupRelationAnd = conditionNode.NodeProperty?.GroupRelation ?? true;

            foreach (var group in groups)
            {
                bool groupResult = EvaluateConditionGroup(group, formData.Value);
                if (groupRelationAnd && !groupResult) return false;  // AND: 一组为假则整体假
                if (!groupRelationAnd && groupResult) return true;    // OR: 一组为真则整体真
            }

            return groupRelationAnd; // AND模式全真 → true, OR模式全假 → false
        }
        catch
        {
            return false;
        }
    }

    private static bool EvaluateConditionGroup(ConditionGroup group, JsonElement formData)
    {
        if (group.ConditionList == null || group.ConditionList.Count == 0) return false;

        // condRelation: 组内条件关系 (true=AND, false=OR)
        bool condRelationAnd = group.CondRelation;

        foreach (var cond in group.ConditionList)
        {
            bool condResult = EvaluateSingleCondition(cond, formData);
            if (condRelationAnd && !condResult) return false;
            if (!condRelationAnd && condResult) return true;
        }

        return condRelationAnd;
    }

    private static bool EvaluateSingleCondition(ConditionItem cond, JsonElement formData)
    {
        // 从表单数据中获取字段值
        if (!formData.TryGetProperty(cond.Key ?? "", out var fieldEl))
            return false;

        var fieldValue = fieldEl.ValueKind == JsonValueKind.String
            ? fieldEl.GetString() ?? ""
            : fieldEl.GetRawText();

        var targetValue = cond.Zdy1 ?? "";
        var targetValue2 = cond.Zdy2 ?? "";

        int optType = cond.OptType ?? 4;
        var type = cond.Type ?? "input";

        // 尝试数值比较
        decimal fv = 0, tv = 0;
        bool numOk = type == "number"
            && decimal.TryParse(fieldValue, out fv)
            && decimal.TryParse(targetValue, out tv);

        return optType switch
        {
            1 => numOk ? fv < tv : string.Compare(fieldValue, targetValue, StringComparison.Ordinal) < 0,
            2 => numOk ? fv > tv : string.Compare(fieldValue, targetValue, StringComparison.Ordinal) > 0,
            3 => numOk ? fv <= tv : string.Compare(fieldValue, targetValue, StringComparison.Ordinal) <= 0,
            4 => string.Equals(fieldValue, targetValue, StringComparison.OrdinalIgnoreCase),
            5 => numOk ? fv >= tv : string.Compare(fieldValue, targetValue, StringComparison.Ordinal) >= 0,
            6 => numOk && decimal.TryParse(targetValue2, out var tv2)
                ? fv >= tv && fv <= tv2
                : string.Compare(fieldValue, targetValue, StringComparison.Ordinal) >= 0
                  && string.Compare(fieldValue, targetValue2, StringComparison.Ordinal) <= 0,
            _ => false
        };
    }

    // ==================== 辅助方法 ====================

    private static List<FlowNode?>? ParseNodeList(string? nodesJson)
    {
        if (string.IsNullOrWhiteSpace(nodesJson)) return null;
        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(nodesJson, JsonOpts);
            if (parsed.ValueKind != JsonValueKind.Array) return null;
            return parsed.EnumerateArray()
                .Select(n => DeserializeFlowNode(n.GetRawText()))
                .Where(n => n != null)
                .ToList();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 反序列化单个节点，处理 nodeProperty 可能是 JSON 字符串的情况
    /// （前端 formatCommitData.cleanNodeList 会 stringify nodeProperty）
    /// </summary>
    private static FlowNode? DeserializeFlowNode(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var dict = new Dictionary<string, object?>();

            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name == "nodeProperty" && prop.Value.ValueKind == JsonValueKind.String)
                {
                    // nodeProperty 是字符串，尝试解析为对象
                    var str = prop.Value.GetString();
                    if (!string.IsNullOrEmpty(str))
                    {
                        try { dict[prop.Name] = JsonSerializer.Deserialize<NodeProperty>(str, JsonOpts); }
                        catch { dict[prop.Name] = null; }
                    }
                    else dict[prop.Name] = null;
                }
                else
                {
                    dict[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Number => prop.Value.GetDecimal(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Array => JsonSerializer.Deserialize<List<string>>(prop.Value.GetRawText(), JsonOpts),
                        _ => null
                    };
                }
            }

            // 将字典序列化为 JSON 再反序列化为 FlowNode
            var json = JsonSerializer.Serialize(dict, JsonOpts);
            return JsonSerializer.Deserialize<FlowNode>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    // ==================== 内部数据类（与前端 nodeUtils 结构对齐） ====================

    public class FlowNode
    {
        public string? NodeId { get; set; }
        public string? NodeName { get; set; }
        public string? NodeDisplayName { get; set; }
        public int NodeType { get; set; }
        public string? NodeFrom { get; set; }
        public List<string>? NodeTo { get; set; }
        public int NodeWeight { get; set; }
        public bool Error { get; set; }
        public NodeProperty? NodeProperty { get; set; }
    }

    public class NodeProperty
    {
        public int AssigneeType { get; set; } = 1;
        public int SignType { get; set; } = 1;
        public int NoHeaderAction { get; set; } = 1;
        public int DirectorLevel { get; set; } = 0;
        public int IsDistinct { get; set; } = 0;
        public int Sort { get; set; } = 0;
        public List<AssigneeInfo>? AssigneeList { get; set; }
        public int IsDefault { get; set; } = 0;
        public bool GroupRelation { get; set; } = false;
        public List<ConditionGroup>? GroupConditions { get; set; }
    }

    public class AssigneeInfo
    {
        /// <summary>审批人/角色 ID（前端传来可能是数字或字符串）</summary>
        public JsonElement? TargetId { get; set; }
        public string? Name { get; set; }

        /// <summary>获取 TargetId 的整数表示</summary>
        public int GetTargetIdAsInt()
        {
            if (TargetId == null) return 0;
            if (TargetId.Value.ValueKind == JsonValueKind.Number)
                return TargetId.Value.GetInt32();
            if (TargetId.Value.ValueKind == JsonValueKind.String
                && int.TryParse(TargetId.Value.GetString(), out var id))
                return id;
            return 0;
        }
    }

    public class ConditionGroup
    {
        public string? GroupId { get; set; }
        public bool CondRelation { get; set; } = true;
        public int Sort { get; set; } = 0;
        public List<ConditionItem>? ConditionList { get; set; }
    }

    public class ConditionItem
    {
        public string? Key { get; set; }
        public string? Label { get; set; }
        public string? Type { get; set; }
        public int? OptType { get; set; }
        public string? Zdy1 { get; set; }
        public string? Opt1 { get; set; }
        public string? Zdy2 { get; set; }
        public string? Opt2 { get; set; }
        public string? ColumnDbName { get; set; }
        public string? ColumnType { get; set; }
        public string? FixedDownBoxValue { get; set; }
    }
}
