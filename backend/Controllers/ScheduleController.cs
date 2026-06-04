using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Models;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ScheduleController : ControllerBase
{
    private readonly AppDbContext _db;

    public ScheduleController(AppDbContext db) => _db = db;

    // ========== 辅助映射 ==========

    private async Task<ScheduledTaskResponse> MapAsync(ScheduledTask s)
    {
        var tasks = await _db.IntegrationTasks.ToDictionaryAsync(t => t.Id, t => t.Name);
        return new ScheduledTaskResponse
        {
            Id = s.Id, Name = s.Name, Description = s.Description,
            IntegrationTaskId = s.IntegrationTaskId,
            IntegrationTaskName = s.IntegrationTaskId > 0 ? tasks.GetValueOrDefault(s.IntegrationTaskId) : (string.IsNullOrEmpty(s.CodeHandler) ? null : "自定义代码"),
            RunMode = s.RunOnceAt != null ? "once" : "cron",
            CronExpression = s.CronExpression,
            RunOnceAt = s.RunOnceAt,
            CodeHandler = s.CodeHandler,
            HandlerClass = s.HandlerClass,
            HandlerParameters = s.HandlerParameters,
            IsEnabled = s.IsEnabled,
            LastRunAt = s.LastRunAt, NextRunAt = s.NextRunAt,
            LastRunStatus = s.LastRunStatus, LastRunDurationMs = s.LastRunDurationMs,
            LastRunMessage = s.LastRunMessage,
            CreatedAt = s.CreatedAt, UpdatedAt = s.UpdatedAt,
            CronDescription = s.RunOnceAt != null
                ? $"单次执行：{s.RunOnceAt.Value.ToLocalTime():yyyy-MM-dd HH:mm}"
                : DescribeCron(s.CronExpression ?? ""),
        };
    }

    // ========== CRUD ==========

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = new List<ScheduledTaskResponse>();
        foreach (var s in await _db.ScheduledTasks.OrderBy(s => s.Id).ToListAsync())
            list.Add(await MapAsync(s));
        return Ok(new { success = true, data = list });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var s = await _db.ScheduledTasks.FindAsync(id);
        if (s == null) return NotFound(new { success = false, message = "计划任务不存在" });
        return Ok(new { success = true, data = await MapAsync(s) });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ScheduledTaskRequest req)
    {
        if (req == null) return BadRequest(new { success = false, message = "请求体为空" });
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { success = false, message = "名称为必填" });
        if (req.IntegrationTaskId <= 0 && string.IsNullOrWhiteSpace(req.CodeHandler) && string.IsNullOrWhiteSpace(req.HandlerClass))
            return BadRequest(new { success = false, message = "请选择关联任务、编写自定义代码或配置执行器类" });

        var entity = new ScheduledTask
        {
            Name = req.Name, Description = req.Description,
            IntegrationTaskId = req.IntegrationTaskId,
            CodeHandler = req.CodeHandler,
            HandlerClass = req.HandlerClass,
            HandlerParameters = req.HandlerParameters,
            IsEnabled = req.IsEnabled,
            CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now,
        };

        if (req.RunMode == "once" && req.RunOnceAt.HasValue)
        {
            // 仅执行一次
            entity.RunOnceAt = req.RunOnceAt.Value.ToUniversalTime();
            entity.NextRunAt = entity.RunOnceAt;
            entity.CronExpression = "";
        }
        else
        {
            // 定时重复
            var cron = string.IsNullOrWhiteSpace(req.CronExpression) ? "0 0 * * *" : req.CronExpression;
            try { ScheduleService.GetNextOccurrence(cron, DateTime.Now); }
            catch { return BadRequest(new { success = false, message = "Cron 表达式格式无效" }); }
            entity.CronExpression = cron;
            entity.NextRunAt = ScheduleService.GetNextOccurrence(cron, DateTime.Now);
        }

        _db.ScheduledTasks.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, data = await MapAsync(entity) });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ScheduledTaskRequest req)
    {
        if (req == null) return BadRequest(new { success = false, message = "请求体为空" });

        var entity = await _db.ScheduledTasks.FindAsync(id);
        if (entity == null) return NotFound(new { success = false, message = "计划任务不存在" });

        entity.Name = req.Name; entity.Description = req.Description;
        entity.IntegrationTaskId = req.IntegrationTaskId;
        entity.CodeHandler = req.CodeHandler;
        entity.HandlerClass = req.HandlerClass;
        entity.HandlerParameters = req.HandlerParameters;
        entity.IsEnabled = req.IsEnabled;
        entity.UpdatedAt = DateTime.Now;

        if (req.RunMode == "once" && req.RunOnceAt.HasValue)
        {
            entity.RunOnceAt = req.RunOnceAt.Value.ToUniversalTime();
            entity.NextRunAt = entity.RunOnceAt;
            entity.CronExpression = "";
        }
        else
        {
            var cron = string.IsNullOrWhiteSpace(req.CronExpression) ? "0 0 * * *" : req.CronExpression;
            try { ScheduleService.GetNextOccurrence(cron, DateTime.Now); }
            catch { return BadRequest(new { success = false, message = "Cron 表达式格式无效" }); }
            entity.RunOnceAt = null;
            entity.CronExpression = cron;
            entity.NextRunAt = ScheduleService.GetNextOccurrence(cron, DateTime.Now);
        }

        await _db.SaveChangesAsync();
        return Ok(new { success = true, data = await MapAsync(entity) });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.ScheduledTasks.FindAsync(id);
        if (entity == null) return NotFound(new { success = false, message = "计划任务不存在" });
        _db.ScheduledTasks.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, message = "计划任务已删除" });
    }

    [HttpPost("{id:int}/trigger")]
    public async Task<IActionResult> TriggerNow(int id, [FromServices] IntegrationService integrationService, [FromServices] IServiceScopeFactory scopeFactory)
    {
        var entity = await _db.ScheduledTasks.FindAsync(id);
        if (entity == null) return NotFound(new { success = false, message = "计划任务不存在" });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var success = true;
        var msg = "";

        try
        {
            // 1) 执行集成任务
            if (entity.IntegrationTaskId > 0)
            {
                var result = await integrationService.ExecuteTaskAsync(entity.IntegrationTaskId);
                success = result.Success;
                msg = result.Message ?? "";
            }

            // 2) 执行自定义代码
            if (!string.IsNullOrWhiteSpace(entity.CodeHandler))
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var codeResult = await ScheduleService.ExecuteCodeAsync(entity.CodeHandler, scope);
                    msg = (string.IsNullOrEmpty(msg) ? "" : msg + "; ") + "代码执行成功";
                    if (!string.IsNullOrEmpty(codeResult)) msg += $" — {codeResult}";
                }
                catch (Exception ex)
                {
                    success = false;
                    msg = "代码执行失败: " + ex.Message;
                }
            }

            // 3) 执行 HandlerClass
            if (!string.IsNullOrWhiteSpace(entity.HandlerClass))
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var handlerResult = await ScheduleService.ExecuteHandlerAsync(entity.HandlerClass, entity.HandlerParameters, scope);
                    msg = (string.IsNullOrEmpty(msg) ? "" : msg + "; ") + handlerResult;
                }
                catch (Exception ex)
                {
                    success = false;
                    msg = "HandlerClass 执行失败: " + ex.Message;
                }
            }

            sw.Stop();
            entity.LastRunAt = DateTime.Now;
            entity.LastRunStatus = success ? "success" : "fail";
            entity.LastRunDurationMs = sw.ElapsedMilliseconds;
            entity.LastRunMessage = success ? (string.IsNullOrEmpty(msg) ? "手动触发执行成功" : msg) : msg;
            entity.UpdatedAt = DateTime.Now;
            if (entity.RunOnceAt != null) entity.IsEnabled = false;
            await _db.SaveChangesAsync();

            return Ok(new { success, message = msg });
        }
        catch (Exception ex)
        {
            sw.Stop();
            entity.LastRunAt = DateTime.Now;
            entity.LastRunStatus = "fail";
            entity.LastRunDurationMs = sw.ElapsedMilliseconds;
            entity.LastRunMessage = ex.Message.Length > 450 ? ex.Message[..450] + "..." : ex.Message;
            entity.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>发现所有实现了 IScheduledTaskHandler 的类</summary>
    [HttpGet("discover-handlers")]
    [AllowAnonymous]
    public IActionResult DiscoverHandlers()
    {
        var list = ScheduleService.DiscoverHandlers();
        return Ok(new { success = true, data = list });
    }

    // ========== Cron 描述 ==========

    public static string DescribeCron(string cron)
    {
        try
        {
            var parts = cron.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5) return cron;
            var m = parts[0]; var h = parts[1]; var d = parts[2]; var mo = parts[3]; var w = parts[4];
            if (m == "0" && h == "0" && d == "*" && mo == "*" && w == "*") return "每天 00:00";
            if (m == "0" && h.StartsWith("*/") && d == "*" && mo == "*" && w == "*") return $"每 {h[2..]} 小时整点";
            if (m == "0" && h != "*" && d == "*" && mo == "*" && w == "*") return $"每天 {h.PadLeft(2, '0')}:00";
            if (m.StartsWith("*/") && h == "*" && d == "*" && mo == "*" && w == "*") return $"每 {m[2..]} 分钟";
            if (m == "*" && h == "*" && d == "*" && mo == "*" && w != "*") return $"每周 {WeekDayName(w)}";
            if (m == "0" && h == "0" && d == "1" && mo == "*" && w == "*") return "每月 1 号 00:00";
            return cron;
        }
        catch { return cron; }
    }

    private static string WeekDayName(string f) => f switch { "0" => "日", "1" => "一", "2" => "二", "3" => "三", "4" => "四", "5" => "五", "6" => "六", _ => f };
}
