using Microsoft.EntityFrameworkCore;
using backend.Data;

namespace backend.ScheduledTaskHandlers;

/// <summary>
/// 清理指定天数前的集成日志
/// 参数: days=30 (保留天数，默认 30)
/// </summary>
public class CleanupLogsHandler : IScheduledTaskHandler
{
    public async Task<string> ExecuteAsync(IServiceProvider services, Dictionary<string, string?> parameters, CancellationToken cancellation)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var days = int.TryParse(parameters.GetValueOrDefault("days", "30"), out var d) ? d : 30;
        var cutoff = DateTime.Now.AddDays(-days).ToString("yyyy-MM-dd");
        var deleted = await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM IntegrationLogs WHERE ExecutedAt < '{cutoff}'", cancellation);
        return $"清理了 {deleted} 条旧日志（{days} 天前，早于 {cutoff}）";
    }

    public List<HandlerParameterMeta> GetParameterMetas() => new()
    {
        new() { Key = "days", Label = "保留天数", Description = "删除多少天之前的集成日志，默认保留最近 30 天", DefaultValue = "30" },
    };
}
