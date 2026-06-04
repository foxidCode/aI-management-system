using Microsoft.EntityFrameworkCore;
using backend.Data;

namespace backend.ScheduledTaskHandlers;

/// <summary>
/// 按条件导出统计数据
/// 参数:
///   table    = Materials / Orders / Users  (要统计的表)
///   topN     = 取前 N 条（默认 5）
///   minDate  = 起始日期（可选，如 2026-01-01）
/// </summary>
public class DataExportHandler : IScheduledTaskHandler
{
    public async Task<string> ExecuteAsync(IServiceProvider services, Dictionary<string, string?> parameters, CancellationToken cancellation)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var table = parameters.GetValueOrDefault("table", "Materials") ?? "Materials";
        var topN = int.TryParse(parameters.GetValueOrDefault("topN", "5"), out var n) ? n : 5;

        return table switch
        {
            "Orders" => await ExportOrders(db, topN, parameters, cancellation),
            "Users" => await ExportUsers(db, topN, cancellation),
            _ => await ExportMaterials(db, topN, cancellation),
        };
    }

    private static async Task<string> ExportMaterials(AppDbContext db, int topN, CancellationToken ct)
    {
        var list = await db.MaterialDictionaries
            .OrderByDescending(m => m.CreatedAt)
            .Take(topN)
            .Select(m => $"{m.Code} {m.Name}({m.Unit})")
            .ToListAsync(ct);

        return $"最近新增物料 Top{topN}: " + (list.Count > 0 ? string.Join("; ", list) : "无数据");
    }

    private static async Task<string> ExportOrders(AppDbContext db, int topN,
        Dictionary<string, string?> parameters, CancellationToken ct)
    {
        var minDateStr = parameters.GetValueOrDefault("minDate", "2026-01-01") ?? "2026-01-01";
        var minDate = DateTime.TryParse(minDateStr, out var d) ? d : new DateTime(2026, 1, 1);

        var list = await db.InboundOrders
            .Where(o => o.CreatedAt >= minDate)
            .OrderByDescending(o => o.TotalTaxIncludedAmount)
            .Take(topN)
            .Select(o => $"{o.OrderCode} ¥{o.TotalTaxIncludedAmount}")
            .ToListAsync(ct);

        return $"入库单({minDate}起)金额 Top{topN}: " + (list.Count > 0 ? string.Join("; ", list) : "无数据");
    }

    private static async Task<string> ExportUsers(AppDbContext db, int topN, CancellationToken ct)
    {
        var list = await db.Users
            .OrderByDescending(u => u.CreatedAt)
            .Take(topN)
            .Select(u => $"{u.Username}({u.Email})")
            .ToListAsync(ct);

        return $"最新用户 Top{topN}: " + (list.Count > 0 ? string.Join("; ", list) : "无数据");
    }

    public List<HandlerParameterMeta> GetParameterMetas() => new()
    {
        new() { Key = "table", Label = "统计对象", Description = "选择要统计的表：Materials(物料) / Orders(入库单) / Users(用户)", DefaultValue = "Materials" },
        new() { Key = "topN", Label = "取前N条", Description = "按排序取前几条记录", DefaultValue = "5" },
        new() { Key = "minDate", Label = "起始日期", Description = "统计入库单(Orders)时的起始日期，格式 yyyy-MM-dd", DefaultValue = "2026-01-01" },
    };
}
