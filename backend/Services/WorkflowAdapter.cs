using System.Text.Json;

namespace backend.Services;

/// <summary>
/// LogicFlow JSON ↔ Workflow Core DSL 适配转换层
///
/// LogicFlow 格式（前端产出）:
/// { "nodes": [{ "id":"approval_1","type":"approval","label":"管理员审批","config":{...} }],
///   "edges": [{ "source":"start_1","target":"approval_1","condition":"always" }] }
///
/// Workflow Core DSL 格式:
/// { "Id":"workflow_1","Version":1,
///   "Steps": [{ "Id":"approval_1","StepType":"backend.Steps.ApprovalStep,backend",
///               "NextStepId":"end_1","Inputs":{...} }] }
/// </summary>
public static class WorkflowAdapter
{
    /// <summary>将 LogicFlow JSON 转换为 Workflow Core DSL JSON</summary>
    public static string ConvertToWorkflowCoreDsl(string logicFlowJson, string workflowId, int version)
    {
        var data = WorkflowEngine.ParseNodeData(logicFlowJson);
        var steps = new List<object>();

        foreach (var node in data.Nodes)
        {
            var step = new Dictionary<string, object?>
            {
                ["Id"] = node.Id,
                ["StepType"] = MapStepType(node.Type),
                ["NextStepId"] = GetNextStepId(data, node.Id),
            };

            // 输入参数
            var inputs = new Dictionary<string, object?>();
            if (node.Config.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in node.Config.EnumerateObject())
                {
                    inputs[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Number => prop.Value.GetRawText(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => prop.Value.GetRawText()
                    };
                }
            }
            if (inputs.Count > 0) step["Inputs"] = inputs;

            // 每个审批节点的驳回出线作为 ErrorBehavior
            if (node.Type == "approval")
            {
                var rejectTarget = data.Edges
                    .FirstOrDefault(e => e.Source == node.Id && e.Condition == "rejected")?.Target;
                if (!string.IsNullOrEmpty(rejectTarget))
                {
                    step["ErrorBehavior"] = "Compensate";
                    step["CompensateStepId"] = rejectTarget;
                }
            }

            steps.Add(step);
        }

        var dsl = new Dictionary<string, object?>
        {
            ["Id"] = workflowId,
            ["Version"] = version,
            ["Steps"] = steps,
        };

        return JsonSerializer.Serialize(dsl, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string MapStepType(string logicFlowType) => logicFlowType switch
    {
        "start" => "backend.Steps.StartStep, backend",
        "end" => "backend.Steps.EndStep, backend",
        "approval" => "backend.Steps.ApprovalStep, backend",
        "cc" => "backend.Steps.CcStep, backend",
        "condition" => "backend.Steps.ConditionStep, backend",
        _ => "backend.Steps.GenericStep, backend",
    };

    private static string? GetNextStepId(NodeData data, string currentNodeId)
    {
        var outgoing = data.Edges
            .Where(e => e.Source == currentNodeId && (e.Condition == "always" || e.Condition == "approved" || e.Condition == "matched"))
            .Select(e => e.Target)
            .ToList();
        return outgoing.Count > 0 ? outgoing[0] : null;
    }
}
