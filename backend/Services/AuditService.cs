using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Models;

namespace backend.Services;

/// <summary>
/// 安全审计服务：操作日志记录、权限变更日志、异常检测
/// </summary>
public class AuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db)
    {
        _db = db;
    }

    // ========== 操作日志记录 ==========

    /// <summary>记录操作日志</summary>
    public async Task LogOperationAsync(int userId, string username, string? ipAddress,
        string action, string? targetType, string? targetId, string? targetName,
        string? detail, bool isSensitive = false)
    {
        var log = new OperationLog
        {
            UserId = userId,
            Username = username,
            IpAddress = ipAddress,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            TargetName = targetName,
            Detail = detail,
            IsSensitive = isSensitive,
            CreatedAt = DateTime.Now
        };

        _db.OperationLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    // ========== 权限变更日志 ==========

    /// <summary>记录权限变更</summary>
    public async Task LogPermissionChangeAsync(
        int? targetUserId, string? targetUsername,
        string changeType, string permissionCode, string? permissionName,
        int operatorId, string operatorName, string? operatorIp, string? detail)
    {
        var log = new PermissionChangeLog
        {
            TargetUserId = targetUserId,
            TargetUsername = targetUsername,
            ChangeType = changeType,
            PermissionCode = permissionCode,
            PermissionName = permissionName,
            OperatorId = operatorId,
            OperatorName = operatorName,
            OperatorIp = operatorIp,
            Detail = detail,
            CreatedAt = DateTime.Now
        };

        _db.PermissionChangeLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    // ========== 操作日志查询 ==========

    public async Task<OperationLogListResponse> GetOperationLogsAsync(OperationLogQueryRequest req)
    {
        var query = _db.OperationLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.Keyword))
        {
            var kw = req.Keyword.Trim();
            query = query.Where(l => l.Username.Contains(kw)
                || (l.Action != null && l.Action.Contains(kw))
                || (l.TargetName != null && l.TargetName.Contains(kw))
                || (l.Detail != null && l.Detail.Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(req.Action))
            query = query.Where(l => l.Action == req.Action);

        if (req.UserId.HasValue)
            query = query.Where(l => l.UserId == req.UserId.Value);

        if (!string.IsNullOrWhiteSpace(req.StartDate) && DateTime.TryParse(req.StartDate, out var start))
            query = query.Where(l => l.CreatedAt >= start);

        if (!string.IsNullOrWhiteSpace(req.EndDate) && DateTime.TryParse(req.EndDate, out var end))
            query = query.Where(l => l.CreatedAt <= end.AddDays(1));

        var total = await query.CountAsync();

        var list = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(l => new OperationLogResponse
            {
                Id = l.Id,
                UserId = l.UserId,
                Username = l.Username,
                IpAddress = l.IpAddress,
                Action = l.Action,
                TargetType = l.TargetType,
                TargetId = l.TargetId,
                TargetName = l.TargetName,
                Detail = l.Detail,
                IsSensitive = l.IsSensitive,
                CreatedAt = l.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            })
            .ToListAsync();

        return new OperationLogListResponse { List = list, Total = total };
    }

    // ========== 权限变更日志查询 ==========

    public async Task<PermissionChangeLogListResponse> GetPermissionChangeLogsAsync(PermissionChangeLogQueryRequest req)
    {
        var query = _db.PermissionChangeLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.Keyword))
        {
            var kw = req.Keyword.Trim();
            query = query.Where(l => (l.TargetUsername != null && l.TargetUsername.Contains(kw))
                || l.PermissionCode.Contains(kw)
                || (l.PermissionName != null && l.PermissionName.Contains(kw))
                || l.OperatorName.Contains(kw));
        }

        if (req.TargetUserId.HasValue)
            query = query.Where(l => l.TargetUserId == req.TargetUserId.Value);

        if (!string.IsNullOrWhiteSpace(req.ChangeType))
            query = query.Where(l => l.ChangeType == req.ChangeType);

        if (!string.IsNullOrWhiteSpace(req.StartDate) && DateTime.TryParse(req.StartDate, out var start))
            query = query.Where(l => l.CreatedAt >= start);

        if (!string.IsNullOrWhiteSpace(req.EndDate) && DateTime.TryParse(req.EndDate, out var end))
            query = query.Where(l => l.CreatedAt <= end.AddDays(1));

        var total = await query.CountAsync();

        var list = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(l => new PermissionChangeLogResponse
            {
                Id = l.Id,
                TargetUserId = l.TargetUserId,
                TargetUsername = l.TargetUsername,
                ChangeType = l.ChangeType,
                PermissionCode = l.PermissionCode,
                PermissionName = l.PermissionName,
                OperatorId = l.OperatorId,
                OperatorName = l.OperatorName,
                OperatorIp = l.OperatorIp,
                Detail = l.Detail,
                CreatedAt = l.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            })
            .ToListAsync();

        return new PermissionChangeLogListResponse { List = list, Total = total };
    }

    // ========== 操作统计 ==========

    public async Task<OperationStatsResponse> GetOperationStatsAsync(int days = 7)
    {
        var since = DateTime.Now.AddDays(-days);
        var todayStart = DateTime.Now.Date;

        var totalOps = await _db.OperationLogs.CountAsync();
        var sensitiveOps = await _db.OperationLogs.CountAsync(l => l.IsSensitive);
        var todayOps = await _db.OperationLogs.CountAsync(l => l.CreatedAt >= todayStart);

        // 异常告警数（最近7天的敏感操作）
        var anomalyCount = await _db.OperationLogs
            .CountAsync(l => l.IsSensitive && l.CreatedAt >= since);

        // 操作类型分布（最近7天 Top 10）
        var distribution = await _db.OperationLogs
            .Where(l => l.CreatedAt >= since)
            .GroupBy(l => l.Action)
            .Select(g => new ActionDistribution { Action = g.Key, Count = g.Count() })
            .OrderByDescending(a => a.Count)
            .Take(10)
            .ToListAsync();

        return new OperationStatsResponse
        {
            TotalOperations = totalOps,
            SensitiveOperations = sensitiveOps,
            TodayOperations = todayOps,
            AnomalyCount = anomalyCount,
            ActionDistribution = distribution
        };
    }

    // ========== 异常检测 ==========

    /// <summary>
    /// 检测异常行为，返回告警列表
    /// </summary>
    public async Task<List<AnomalyAlertResponse>> DetectAnomaliesAsync()
    {
        var alerts = new List<AnomalyAlertResponse>();
        var now = DateTime.Now;

        // 规则1：非工作时间敏感操作（22:00-06:00）
        try
        {
            var todayStart = now.Date;
            var yesterdayStart = todayStart.AddDays(-1);
            var nightSensitiveOps = await _db.OperationLogs
                .Where(l => l.IsSensitive && l.CreatedAt >= yesterdayStart)
                .ToListAsync();

            foreach (var op in nightSensitiveOps)
            {
                var hour = op.CreatedAt.Hour;
                if (hour >= 22 || hour < 6)
                {
                    alerts.Add(new AnomalyAlertResponse
                    {
                        AlertType = "非工作时间敏感操作",
                        Severity = "danger",
                        Message = $"用户 {op.Username} 在 {op.CreatedAt:HH:mm:ss} 执行了敏感操作「{op.Action}」",
                        RelatedUserId = op.UserId,
                        RelatedUsername = op.Username,
                        RelatedLogId = op.Id,
                        DetectedAt = now.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }
            }
        }
        catch { /* 忽略查询异常 */ }

        // 规则2：短时间内大量权限变更（10分钟内超过10次）
        try
        {
            var tenMinAgo = now.AddMinutes(-10);
            var rapidPermChanges = await _db.OperationLogs
                .Where(l => l.Action.StartsWith("permission:") && l.CreatedAt >= tenMinAgo)
                .GroupBy(l => l.UserId)
                .Select(g => new { UserId = g.Key, Username = g.First().Username, Count = g.Count() })
                .ToListAsync();

            foreach (var g in rapidPermChanges.Where(g => g.Count > 10))
            {
                alerts.Add(new AnomalyAlertResponse
                {
                    AlertType = "频繁权限变更",
                    Severity = "warning",
                    Message = $"用户 {g.Username} 在10分钟内执行了 {g.Count} 次权限变更操作",
                    RelatedUserId = g.UserId,
                    RelatedUsername = g.Username,
                    DetectedAt = now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
        }
        catch { }

        // 规则3：一小时内大量导出操作（超过5次）
        try
        {
            var oneHourAgo = now.AddHours(-1);
            var frequentExports = await _db.OperationLogs
                .Where(l => l.Action == "export" && l.CreatedAt >= oneHourAgo)
                .GroupBy(l => l.UserId)
                .Select(g => new { UserId = g.Key, Username = g.First().Username, Count = g.Count() })
                .ToListAsync();

            foreach (var g in frequentExports.Where(g => g.Count > 5))
            {
                alerts.Add(new AnomalyAlertResponse
                {
                    AlertType = "频繁导出数据",
                    Severity = "warning",
                    Message = $"用户 {g.Username} 在1小时内执行了 {g.Count} 次数据导出操作",
                    RelatedUserId = g.UserId,
                    RelatedUsername = g.Username,
                    DetectedAt = now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
        }
        catch { }

        // 规则4：一小时内批量删除操作
        try
        {
            var oneHourAgo = now.AddHours(-1);
            var batchDeletes = await _db.OperationLogs
                .Where(l => l.Action == "batch_delete" && l.CreatedAt >= oneHourAgo)
                .ToListAsync();

            if (batchDeletes.Count > 0)
            {
                foreach (var op in batchDeletes)
                {
                    alerts.Add(new AnomalyAlertResponse
                    {
                        AlertType = "批量删除操作",
                        Severity = "warning",
                        Message = $"用户 {op.Username} 在 {op.CreatedAt:HH:mm:ss} 执行了批量删除操作：{op.Detail}",
                        RelatedUserId = op.UserId,
                        RelatedUsername = op.Username,
                        RelatedLogId = op.Id,
                        DetectedAt = now.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }
            }
        }
        catch { }

        return alerts;
    }
}
