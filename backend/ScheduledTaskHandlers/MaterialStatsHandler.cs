using Microsoft.EntityFrameworkCore;
using backend.Data;

namespace backend.ScheduledTaskHandlers;

/// <summary>
/// 统计系统数据
/// 参数: includeDetail=true (是否包含明细)
/// </summary>
public class MaterialStatsHandler : IScheduledTaskHandler
{
    public async Task<string> ExecuteAsync(IServiceProvider services, Dictionary<string, string?> parameters, CancellationToken cancellation)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var total = await db.MaterialDictionaries.CountAsync(cancellation);
        var inboundCount = await db.InboundOrders.CountAsync(cancellation);
        var userCount = await db.Users.CountAsync(cancellation);

        var detail = parameters.GetValueOrDefault("includeDetail", "false") == "true";
        var result = $"物料{total}条, 入库单{inboundCount}条, 用户{userCount}人, 统计时间{DateTime.Now:HH:mm:ss}";

        if (detail)
        {
            var lastInbound = await db.InboundOrders.OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync(cancellation);
            result += $" — 最新入库单: {lastInbound?.OrderCode ?? "无"}";
        }

        return result;
    }

    public List<HandlerParameterMeta> GetParameterMetas() => new()
    {
        new() { Key = "includeDetail", Label = "包含明细", Description = "是否在统计结果中显示最新入库单详情", DefaultValue = "false" },
    };
}
