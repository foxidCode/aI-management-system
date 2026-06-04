using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using backend.Data;

namespace backend.Services;

/// <summary>
/// 工作流超时处理服务 — 每30秒检查超时的审批任务，按策略自动处理
/// </summary>
public class WorkflowTimeoutService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowTimeoutService> _logger;

    public WorkflowTimeoutService(IServiceScopeFactory scopeFactory, ILogger<WorkflowTimeoutService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WorkflowTimeoutService 已启动");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTimeoutsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "超时检查异常");
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ProcessTimeoutsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<WorkflowEngine>();
        var now = DateTime.Now;

        // 找出运行中实例的待审批任务
        var runningInstanceIds = await db.WorkflowInstances
            .Where(i => i.Status == "running")
            .Select(i => i.Id)
            .ToListAsync(ct);

        if (runningInstanceIds.Count == 0) return;

        var pendingTasks = await db.WorkflowTasks
            .Where(t => t.Status == "pending" && t.TaskType == "approval" && runningInstanceIds.Contains(t.InstanceId))
            .ToListAsync(ct);

        foreach (var task in pendingTasks)
        {
            if (ct.IsCancellationRequested) return;

            // 获取节点配置中的超时设置
            var instance = await db.WorkflowInstances.FindAsync(task.InstanceId);
            if (instance == null || instance.Status != "running") continue;

            var nodeData = WorkflowEngine.ParseNodeData(instance.NodeData);
            var node = nodeData.Nodes.FirstOrDefault(n => n.Id == task.NodeId);
            if (node == null) continue;

            var timeoutHours = GetTimeoutHours(node.Config);
            if (timeoutHours <= 0) continue;

            var timeout = task.CreatedAt.AddHours(timeoutHours);
            if (now < timeout) continue;

            // 超时处理
            var policy = GetTimeoutPolicy(node.Config); // auto_reject / auto_pass / escalate
            _logger.LogInformation("任务 {TaskId} 超时（{Timeout}h），策略: {Policy}", task.Id, timeoutHours, policy);

            try
            {
                if (policy == "auto_pass")
                {
                    await engine.ApproveAsync(task.InstanceId, task.NodeId, task.AssigneeId, "系统自动通过（超时）");
                }
                else if (policy == "auto_reject")
                {
                    await engine.RejectAsync(task.InstanceId, task.NodeId, task.AssigneeId, "系统自动驳回（超时）");
                }
                // escalate 暂未实现，记录日志提醒管理员
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "超时处理失败 TaskId={TaskId}", task.Id);
            }
        }
    }

    private static int GetTimeoutHours(JsonElement config)
    {
        if (config.ValueKind != JsonValueKind.Object) return 0;
        var val = WorkflowEngine.GetConfigValue(config, "timeoutHours");
        return int.TryParse(val, out var h) ? h : 0;
    }

    private static string GetTimeoutPolicy(JsonElement config)
    {
        return WorkflowEngine.GetConfigValue(config, "timeoutPolicy") ?? "auto_reject";
    }
}
