using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.ScheduledTaskHandlers;

namespace backend.Services;

/// <summary>
/// 计划任务后台服务 — 每秒轮询，按 Cron 表达式定时执行集成任务
/// </summary>
public class ScheduleService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduleService> _logger;
    private static readonly HashSet<int> _runningTasks = new();

    public ScheduleService(IServiceScopeFactory scopeFactory, ILogger<ScheduleService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScheduleService 已启动");
        // 启动时计算一次所有任务的 NextRunAt
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tasks = await db.ScheduledTasks.Where(t => t.IsEnabled).ToListAsync(stoppingToken);
            foreach (var t in tasks)
            {
                // 仅执行一次的任务使用 RunOnceAt，不需要计算 NextRunAt
                if (t.RunOnceAt != null) continue;
                if (string.IsNullOrWhiteSpace(t.CronExpression)) continue;
                t.NextRunAt = GetNextOccurrence(t.CronExpression, DateTime.Now);
            }
            await db.SaveChangesAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScheduleService 轮询异常");
            }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.Now;
        var nowPlusMinute = now.AddMinutes(1);

        // 查找所有需要执行的任务（NextRunAt <= now+1min 且 enabled）
        var dueTasks = await db.ScheduledTasks
            .Where(t => t.IsEnabled && t.NextRunAt != null && t.NextRunAt <= nowPlusMinute)
            .ToListAsync(ct);

        foreach (var task in dueTasks)
        {
            if (ct.IsCancellationRequested) return;

            // 防止同一个任务并发执行
            lock (_runningTasks)
            {
                if (_runningTasks.Contains(task.Id)) continue;
                _runningTasks.Add(task.Id);
            }

            // 异步执行（不阻塞轮询）
            _ = ExecuteScheduledTaskAsync(task, now);
        }
    }

    private async Task ExecuteScheduledTaskAsync(ScheduledTask scheduled, DateTime triggerTime)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var integrationService = scope.ServiceProvider.GetRequiredService<IntegrationService>();

            // 执行集成任务 + 自定义代码
            var msg = "";
            var success = true;

            // 1) 执行关联的集成任务
            if (scheduled.IntegrationTaskId > 0)
            {
                _logger.LogInformation("计划任务 [{Name}] 执行 IntegrationTask #{TaskId}", scheduled.Name, scheduled.IntegrationTaskId);
                var result = await integrationService.ExecuteTaskAsync(scheduled.IntegrationTaskId);
                success = result.Success;
                msg = result.Message ?? "";
            }

            // 2) 执行自定义 C# 代码
            if (!string.IsNullOrWhiteSpace(scheduled.CodeHandler))
            {
                _logger.LogInformation("计划任务 [{Name}] 执行自定义代码", scheduled.Name);
                try
                {
                    var codeResult = await ExecuteCodeAsync(scheduled.CodeHandler, scope);
                    msg = (string.IsNullOrEmpty(msg) ? "" : msg + "; ") + "代码执行成功";
                    if (!string.IsNullOrEmpty(codeResult))
                        msg += $" — 输出: {Truncate(codeResult, 200)}";
                }
                catch (Exception ex)
                {
                    success = false;
                    msg = "代码执行失败: " + ex.Message;
                    _logger.LogError(ex, "计划任务 [{Name}] 代码执行异常", scheduled.Name);
                }
            }

            // 3) 执行 HandlerClass（反射调用）
            if (!string.IsNullOrWhiteSpace(scheduled.HandlerClass))
            {
                _logger.LogInformation("计划任务 [{Name}] 执行 HandlerClass: {Class}", scheduled.Name, scheduled.HandlerClass);
                try
                {
                    var handlerResult = await ExecuteHandlerAsync(scheduled.HandlerClass, scheduled.HandlerParameters, scope);
                    msg = (string.IsNullOrEmpty(msg) ? "" : msg + "; ") + handlerResult;
                }
                catch (Exception ex)
                {
                    success = false;
                    msg = "HandlerClass 执行失败: " + ex.Message;
                    _logger.LogError(ex, "计划任务 [{Name}] HandlerClass 执行异常", scheduled.Name);
                }
            }

            sw.Stop();

            // 更新计划任务状态
            var entity = await db.ScheduledTasks.FindAsync(scheduled.IntegrationTaskId) ?? scheduled;
            // 需要重新查找 ScheduledTask
            var st = await db.ScheduledTasks.FindAsync(scheduled.Id);
            if (st != null)
            {
                st.LastRunAt = DateTime.Now;
                // 仅执行一次的任务：执行后自动禁用
                if (st.RunOnceAt != null)
                {
                    st.IsEnabled = false;
                    st.NextRunAt = null;
                }
                else
                {
                    st.NextRunAt = GetNextOccurrence(st.CronExpression, st.NextRunAt!.Value);
                }
                st.LastRunStatus = success ? "success" : "fail";
                st.LastRunDurationMs = sw.ElapsedMilliseconds;
                st.LastRunMessage = success ? (string.IsNullOrEmpty(msg) ? "执行成功" : msg) : (msg ?? "执行失败");
                st.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync();
            }

            _logger.LogInformation("计划任务 [{Name}] 完成: {Status}, 耗时 {Ms}ms",
                scheduled.Name, st?.LastRunStatus, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "计划任务 [{Name}] 异常", scheduled.Name);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var st = await db.ScheduledTasks.FindAsync(scheduled.Id);
                if (st != null)
                {
                    st.LastRunAt = DateTime.Now;
                    if (st.RunOnceAt != null) { st.IsEnabled = false; st.NextRunAt = null; }
                    else { st.NextRunAt = GetNextOccurrence(st.CronExpression, DateTime.Now); }
                    st.LastRunStatus = "fail";
                    st.LastRunDurationMs = sw.ElapsedMilliseconds;
                    st.LastRunMessage = ex.Message.Length > 450 ? ex.Message[..450] + "..." : ex.Message;
                    st.UpdatedAt = DateTime.Now;
                    await db.SaveChangesAsync();
                }
            }
            catch { }
        }
        finally
        {
            lock (_runningTasks) { _runningTasks.Remove(scheduled.Id); }
        }
    }

    /// <summary>
    /// 计算 Cron 表达式的下一次触发时间（5 字段：分 时 日 月 周）
    /// 支持 *, */N, N, N-M, N,M 语法
    /// </summary>
    public static DateTime GetNextOccurrence(string cron, DateTime from)
    {
        var parts = cron.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
            throw new ArgumentException("Cron 表达式必须有 5 个字段：分 时 日 月 周");

        var minute = parts[0];
        var hour = parts[1];
        var day = parts[2];
        var month = parts[3];
        var dayOfWeek = parts[4];

        var result = new DateTime(from.Year, from.Month, from.Day, from.Hour, from.Minute, 0).AddMinutes(1);

        // 最多往未来找 365 天
        for (int i = 0; i < 525600; i++) // 365 * 24 * 60
        {
            if (MatchField(minute, result.Minute, 0, 59) &&
                MatchField(hour, result.Hour, 0, 23) &&
                MatchField(day, result.Day, 1, 31) &&
                MatchField(month, result.Month, 1, 12) &&
                MatchField(dayOfWeek, (int)result.DayOfWeek, 0, 6))
            {
                return result;
            }
            result = result.AddMinutes(1);
        }

        return from.AddDays(365);
    }

    /// <summary>
    /// 执行 C# 自定义代码脚本。代码中可直接使用 db (AppDbContext)。
    /// 支持在代码顶部写 using 语句引入额外命名空间，如 using System.IO;
    /// </summary>
    public static async Task<string?> ExecuteCodeAsync(string code, IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var globals = new ScriptGlobals { db = db };

        // 从代码中提取顶层 using 语句并移除
        var userUsings = new List<string>();
        var codeLines = new List<string>();
        var allLines = code.Replace("\r\n", "\n").Split('\n');
        var assemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in allLines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("using ") && trimmed.EndsWith(";") && !trimmed.Contains('=') && !trimmed.Contains('('))
            {
                // 提取命名空间名称：using Xxx.Yyy; → Xxx.Yyy
                var ns = trimmed[6..^1].Trim();
                userUsings.Add(trimmed);
                // 提取顶层程序集名（第一个点之前的部分）
                var topNs = ns.Split('.')[0];
                assemblyNames.Add(topNs);
            }
            else
            {
                codeLines.Add(line);
            }
        }

        var cleanCode = string.Join("\n", codeLines).Trim();

        // 尝试加载用户引用的程序集
        var references = new List<System.Reflection.Assembly>
        {
            typeof(JsonSerializer).Assembly,
            typeof(AppDbContext).Assembly,
            typeof(IntegrationService).Assembly,
        };

        foreach (var asmName in assemblyNames)
        {
            try
            {
                var asm = System.Reflection.Assembly.Load(asmName);
                if (asm != null) references.Add(asm);
            }
            catch { /* 程序集不可用则忽略 */ }
            try
            {
                // 也尝试常见变体：System.Xxx → System.Xxx.dll
                var asm = System.Reflection.Assembly.Load($"{asmName}.dll");
                if (asm != null) references.Add(asm);
            }
            catch { }
        }

        var script = $@"
            using System;
            using System.Linq;
            using System.Text.Json;
            using System.Collections.Generic;
            using Microsoft.EntityFrameworkCore;
            using backend.Data;
            using backend.Services;
            using backend.Models;
            {string.Join("\n", userUsings)}
            {cleanCode}
            return ""ok"";
        ";

        return await Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript.EvaluateAsync<string>(
            script,
            Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default
                .WithReferences(references.Distinct().ToArray())
                .WithImports("System", "System.Text.Json", "System.Collections.Generic", "System.Linq",
                    "Microsoft.EntityFrameworkCore", "backend.Data", "backend.Services", "backend.Models"),
            globals: globals);
    }

    public class ScriptGlobals
    {
        public AppDbContext db { get; set; } = null!;
    }

    /// <summary>通过反射实例化并执行 HandlerClass</summary>
    public static async Task<string> ExecuteHandlerAsync(string className, string? parametersJson, IServiceScope scope)
    {
        // 解析参数
        var parameters = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(parametersJson))
        {
            try { parameters = JsonSerializer.Deserialize<Dictionary<string, string?>>(parametersJson) ?? new(); }
            catch { /* 参数解析失败则使用空字典 */ }
        }

        // 缓存已解析的类型
        var type = _handlerTypeCache.GetOrAdd(className, name =>
        {
            var t = Type.GetType(name);
            if (t == null)
            {
                // 遍历已加载的程序集查找
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType(name);
                    if (t != null) break;
                }
            }
            if (t == null)
                throw new InvalidOperationException($"找不到类型: {name}");

            if (!typeof(IScheduledTaskHandler).IsAssignableFrom(t))
                throw new InvalidOperationException($"类型 {name} 未实现 IScheduledTaskHandler 接口");

            return t;
        });

        var handler = (IScheduledTaskHandler)Activator.CreateInstance(type)!;
        return await handler.ExecuteAsync(scope.ServiceProvider, parameters, CancellationToken.None);
    }

    private static readonly ConcurrentDictionary<string, Type> _handlerTypeCache = new();

    /// <summary>发现所有实现了 IScheduledTaskHandler 的类及其全路径</summary>
    public static List<HandlerInfo> DiscoverHandlers()
    {
        var list = new List<HandlerInfo>();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var t in asm.GetTypes())
                {
                    if (t.IsClass && !t.IsAbstract && typeof(IScheduledTaskHandler).IsAssignableFrom(t))
                    {
                        var handler = (IScheduledTaskHandler)Activator.CreateInstance(t)!;
                        list.Add(new HandlerInfo
                        {
                            FullName = t.FullName!,
                            Namespace = t.Namespace ?? "",
                            ClassName = t.Name,
                            Parameters = handler.GetParameterMetas(),
                        });
                    }
                }
            }
            catch { /* 某些程序集可能无法加载 */ }
        }
        return list;
    }

    public class HandlerInfo
    {
        public string FullName { get; set; } = "";
        public string Namespace { get; set; } = "";
        public string ClassName { get; set; } = "";
        public List<HandlerParameterMeta> Parameters { get; set; } = new();
    }

    private static string Truncate(string value, int maxLen) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLen ? value : value[..maxLen] + "...";

    private static bool MatchField(string field, int value, int min, int max)
    {
        if (field == "*") return true;

        // 逗号分隔多项
        foreach (var part in field.Split(','))
        {
            var p = part.Trim();

            // */N 步进
            if (p.StartsWith("*/"))
            {
                if (int.TryParse(p[2..], out var step) && step > 0)
                    return (value - min) % step == 0;
                continue;
            }

            // N-M 范围
            if (p.Contains('-'))
            {
                var range = p.Split('-');
                if (range.Length == 2 && int.TryParse(range[0], out var rStart) && int.TryParse(range[1], out var rEnd))
                    return value >= rStart && value <= rEnd;
                continue;
            }

            // 具体值
            if (int.TryParse(p, out var exact))
            {
                if (value == exact) return true;
            }
        }

        return false;
    }
}
