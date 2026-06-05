using Microsoft.AspNetCore.SignalR;
using backend.Hubs;

namespace backend.Services;

public class NotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>通知用户有新的待办任务</summary>
    public async Task NotifyNewTaskAsync(int userId, string instanceName, string nodeName, int instanceId)
    {
        await _hubContext.Clients.Group($"user_{userId}").SendAsync("NewTask", new
        {
            instanceId,
            instanceName,
            nodeName,
            message = $"您有一个新的审批任务：{instanceName} - {nodeName}",
            timestamp = DateTime.Now
        });
    }

    /// <summary>通知申请人流程状态变更</summary>
    public async Task NotifyInstanceStatusAsync(int userId, string instanceName, string status, int instanceId)
    {
        var statusText = status switch
        {
            "Completed" => "已通过",
            "Rejected" => "已驳回",
            "Cancelled" => "已取消",
            _ => status
        };

        await _hubContext.Clients.Group($"user_{userId}").SendAsync("InstanceStatusChanged", new
        {
            instanceId,
            instanceName,
            status,
            message = $"您的申请「{instanceName}」{statusText}",
            timestamp = DateTime.Now
        });
    }
}
