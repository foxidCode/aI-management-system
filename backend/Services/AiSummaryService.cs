using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Models;

namespace backend.Services;

/// <summary>
/// 每日 AI 总结服务：生成总结、审批管理、知识库归档
/// </summary>
public class AiSummaryService
{
    private readonly AppDbContext _db;
    private readonly AiKnowledgeService _knowledgeService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApiKeyEncryptionService _encryption;

    public AiSummaryService(AppDbContext db, AiKnowledgeService knowledgeService, IHttpClientFactory httpClientFactory, ApiKeyEncryptionService encryption)
    {
        _db = db;
        _knowledgeService = knowledgeService;
        _httpClientFactory = httpClientFactory;
        _encryption = encryption;
    }

    // ========== List ==========

    public async Task<DailySummaryListResponse> GetListAsync(int page = 1, int pageSize = 20)
    {
        var query = _db.DailySummaries.OrderByDescending(s => s.SummaryDate);

        var total = await query.CountAsync();
        var list = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new DailySummaryResponse
            {
                Id = s.Id,
                SummaryDate = s.SummaryDate,
                Content = s.Content,
                SessionCount = s.SessionCount,
                MessageCount = s.MessageCount,
                Status = s.Status,
                ReviewedBy = s.ReviewedBy,
                ReviewerName = s.ReviewedBy != null
                    ? _db.Users.Where(u => u.Id == s.ReviewedBy).Select(u => u.Username).FirstOrDefault()
                    : null,
                ReviewComment = s.ReviewComment,
                CreatedAt = s.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                ReviewedAt = s.ReviewedAt.HasValue ? s.ReviewedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : null
            })
            .ToListAsync();

        return new DailySummaryListResponse { List = list, Total = total };
    }

    public async Task<DailySummaryResponse?> GetByIdAsync(int id)
    {
        var s = await _db.DailySummaries.FindAsync(id);
        if (s == null) return null;

        return new DailySummaryResponse
        {
            Id = s.Id,
            SummaryDate = s.SummaryDate,
            Content = s.Content,
            SessionCount = s.SessionCount,
            MessageCount = s.MessageCount,
            Status = s.Status,
            ReviewedBy = s.ReviewedBy,
            ReviewerName = s.ReviewedBy != null
                ? await _db.Users.Where(u => u.Id == s.ReviewedBy).Select(u => u.Username).FirstOrDefaultAsync()
                : null,
            ReviewComment = s.ReviewComment,
            CreatedAt = s.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            ReviewedAt = s.ReviewedAt.HasValue ? s.ReviewedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : null
        };
    }

    // ========== Generate ==========

    /// <summary>
    /// 为指定日期生成总结（如不指定则默认为今天）
    /// </summary>
    public async Task<DailySummaryResponse?> GenerateAsync(string? date = null)
    {
        var targetDate = date ?? DateTime.Now.ToString("yyyy-MM-dd");
        var targetDateObj = DateTime.Parse(targetDate);
        var nextDay = targetDateObj.AddDays(1);

        // 如果已存在总结（无论是否空），先删除再重新生成
        var existing = await _db.DailySummaries.FirstOrDefaultAsync(s => s.SummaryDate == targetDate);
        if (existing != null)
        {
            _db.DailySummaries.Remove(existing);
            await _db.SaveChangesAsync();
        }

        // 获取当天所有用户消息（仅用于判断是否有对话）
        var userMessages = await _db.ChatMessages
            .Where(m => m.Role == "user" && m.CreatedAt >= targetDateObj && m.CreatedAt < nextDay)
            .ToListAsync();

        var allMessages = await _db.ChatMessages
            .Where(m => m.CreatedAt >= targetDateObj && m.CreatedAt < nextDay)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var sessionIds = allMessages.Select(m => m.SessionId).Distinct().ToList();
        var sessionCount = sessionIds.Count;

        if (userMessages.Count == 0)
        {
            // 当天没有对话，创建空总结
            var emptySummary = new DailySummary
            {
                SummaryDate = targetDate,
                Content = $"## {targetDate} 对话总结\n\n当天没有用户与 AI 进行对话。",
                SessionCount = 0,
                MessageCount = 0,
                Status = "approved", // 空总结自动通过
                CreatedAt = DateTime.Now
            };
            _db.DailySummaries.Add(emptySummary);
            await _db.SaveChangesAsync();
            return await GetByIdAsync(emptySummary.Id);
        }

        // 获取活跃模型配置
        var config = await _db.AiModelConfigs.FirstOrDefaultAsync(c => c.IsActive);
        if (config == null)
        {
            throw new InvalidOperationException("暂未启用任何 AI 模型，请先在「AI配置」中启用一个模型后再生成总结。");
        }

        // 使用 AI 生成总结（传入完整问答对，让 AI 总结问题并给出答案）
        var conversationText = BuildQAPairs(allMessages);
        var systemPrompt = "你是本系统的知识管理助手。以下是一天内用户与 AI 助手的完整对话记录。" +
            "\n\n请完成以下任务：" +
            "\n1. **提取关键问答**：从对话中提取用户的核心问题，并以系统知识库的视角给出**准确、完整的答案**（参考 AI 助手的回复，但可以优化补充）" +
            "\n2. **去重合并**：相似的问题合并为一条，避免重复" +
            "\n3. **知识缺口**：标注对话中 AI 助手未能很好回答的问题，建议补充到知识库" +
            "\n\n输出格式（Markdown）：" +
            "\n```markdown" +
            "\n## {日期} 对话总结" +
            "\n" +
            "\n### 📊 概览" +
            "\n- 会话数：X | 消息总数：X" +
            "\n" +
            "\n### ❓ 关键问答" +
            "\n**Q1: 用户问题**" +
            "\nA: 基于对话和系统知识的回答" +
            "\n" +
            "\n**Q2: ...**" +
            "\n" +
            "\n### ⚠️ 待补充知识" +
            "\n- [ ] 需要补充的主题" +
            "\n```" +
            "\n\n**严格规则**：" +
            "\n- 直接以 `## {日期} 对话总结` 开头输出 Markdown，不要添加任何问候语、开场白或结尾语" +
            "\n- 答案要具体可操作，包含精确的菜单位置和操作步骤" +
            "\n- 如果当天没有任何对话，只输出 `## {日期} 对话总结\\n\\n当天没有用户与 AI 进行对话。`";

        // 将 {日期} 占位符替换为实际日期，避免 LLM 自行解释
        systemPrompt = systemPrompt.Replace("{日期}", targetDate);

        var (summaryContent, errorDetail) = await CallLLmForSummaryWithError(config, systemPrompt, conversationText);

        var dailySummary = new DailySummary
        {
            SummaryDate = targetDate,
            Content = summaryContent ?? (
                $"## {targetDate} 对话总结\n\n" +
                $"⚠️ LLM 总结生成失败\n\n" +
                $"**错误详情**：{errorDetail}\n\n" +
                $"- 会话数：{sessionCount}\n- 消息数：{allMessages.Count}"),
            SessionCount = sessionCount,
            MessageCount = allMessages.Count,
            Status = "pending",
            CreatedAt = DateTime.Now
        };

        _db.DailySummaries.Add(dailySummary);
        await _db.SaveChangesAsync();
        return await GetByIdAsync(dailySummary.Id);
    }

    // ========== Review ==========

    /// <summary>
    /// 管理员审批总结
    /// </summary>
    public async Task<DailySummaryResponse?> ReviewAsync(int id, int reviewerId, ReviewSummaryRequest req)
    {
        var summary = await _db.DailySummaries.FindAsync(id);
        if (summary == null) return null;

        summary.Status = req.Status;
        summary.ReviewedBy = reviewerId;
        summary.ReviewComment = req.ReviewComment;
        summary.ReviewedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        // 审批通过：创建知识库条目
        if (req.Status == "approved")
        {
            await _knowledgeService.CreateFromSummaryAsync(
                summary.Id,
                $"每日总结 - {summary.SummaryDate}",
                summary.Content
            );
        }

        return await GetByIdAsync(summary.Id);
    }

    // ========== Delete ==========

    public async Task<bool> DeleteAsync(int id)
    {
        var summary = await _db.DailySummaries.FindAsync(id);
        if (summary == null) return false;
        _db.DailySummaries.Remove(summary);
        await _db.SaveChangesAsync();
        return true;
    }

    // ========== Private Helpers ==========

    /// <summary>
    /// 从完整消息列表构建 QA 对话文本（用户问题 + AI 回答）
    /// </summary>
    private static string BuildQAPairs(List<ChatMessage> allMessages)
    {
        var sb = new StringBuilder();
        var sessionGroups = allMessages.GroupBy(m => m.SessionId);

        foreach (var session in sessionGroups)
        {
            var msgs = session.OrderBy(m => m.CreatedAt).ToList();
            sb.AppendLine("--- 会话 ---");
            for (int i = 0; i < msgs.Count; i++)
            {
                var msg = msgs[i];
                if (msg.Role == "user")
                {
                    sb.AppendLine($"**用户**: {msg.Content}");
                    sb.AppendLine();
                }
                else if (msg.Role == "assistant")
                {
                    // 截断过长的 AI 回复（保留前800字以控制 token）
                    var content = msg.Content.Length > 800
                        ? msg.Content[..800] + "...(已截断)"
                        : msg.Content;
                    sb.AppendLine($"**AI**: {content}");
                    sb.AppendLine();
                }
            }
        }
        return sb.ToString();
    }

    private async Task<(string? content, string error)> CallLLmForSummaryWithError(AiModelConfig config, string systemPrompt, string conversationText)
    {
        var lastError = "";
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(3);

            var userMessage = $"请总结以下对话内容：\n\n{conversationText}";

            object requestBody;
            string endpoint;
            string authHeader;
            string authValue;

            if (config.Provider == "anthropic")
            {
                // Anthropic: system 是顶层字段，messages 只含 user/assistant
                requestBody = new
                {
                    model = config.ModelName,
                    system = systemPrompt,
                    messages = new[]
                    {
                        new { role = "user", content = userMessage }
                    },
                    max_tokens = 4096,
                    temperature = 0.5
                };
                endpoint = config.ApiEndpoint.TrimEnd('/') + "/v1/messages";
                authHeader = "x-api-key";
                authValue = _encryption.Decrypt(config.ApiKey);
            }
            else
            {
                // OpenAI 兼容: system 作为消息角色
                requestBody = new
                {
                    model = config.ModelName,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userMessage }
                    },
                    max_tokens = 2000,
                    temperature = 0.5
                };
                endpoint = config.ApiEndpoint.TrimEnd('/') + "/v1/chat/completions";
                authHeader = "Authorization";
                authValue = $"Bearer {_encryption.Decrypt(config.ApiKey)}";
            }

            var jsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            var json = JsonSerializer.Serialize(requestBody, jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
            request.Headers.Add(authHeader, authValue);
            if (config.Provider == "anthropic")
                request.Headers.Add("anthropic-version", "2023-06-01");

            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                lastError = $"API {response.StatusCode}: {TruncateStr(responseBody, 500)}";
                Console.Error.WriteLine($"[AiSummary] {lastError}");
                return (null, lastError);
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (config.Provider == "anthropic")
            {
                // Anthropic / DeepSeek 兼容响应解析
                // 响应格式：{ content: [{ type: "text"/"thinking", text/thinking: "..." }] }
                if (root.TryGetProperty("content", out var contentList) && contentList.GetArrayLength() > 0)
                {
                    // 遍历所有 content block，优先取 text 类型，其次 thinking
                    string? thinkingText = null;
                    foreach (var block in contentList.EnumerateArray())
                    {
                        var blockType = block.TryGetProperty("type", out var t) ? t.GetString() : "";
                        if (blockType == "text" && block.TryGetProperty("text", out var textProp))
                            return (textProp.GetString() ?? "", "");
                        if (blockType == "thinking" && block.TryGetProperty("thinking", out var thinkProp))
                            thinkingText = thinkProp.GetString();
                    }
                    if (!string.IsNullOrEmpty(thinkingText))
                        return (thinkingText, "");
                }
                // 尝试 OpenAI 兼容格式（部分兼容接口可能返回此格式）
                if (root.TryGetProperty("choices", out var altChoices) && altChoices.GetArrayLength() > 0)
                {
                    if (altChoices[0].TryGetProperty("message", out var altMsg) &&
                        altMsg.TryGetProperty("content", out var altContent))
                        return (altContent.GetString() ?? "", "");
                }
                // Check for error
                if (root.TryGetProperty("error", out var errProp))
                {
                    lastError = $"Anthropic error: {errProp.GetRawText()}";
                    Console.Error.WriteLine($"[AiSummary] {lastError}");
                    return (null, lastError);
                }
            }
            else
            {
                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    if (choices[0].TryGetProperty("message", out var msg) &&
                        msg.TryGetProperty("content", out var msgContent))
                        return (msgContent.GetString() ?? "", "");
                }
            }

            lastError = $"Unexpected response format: {TruncateStr(responseBody, 300)}";
            Console.Error.WriteLine($"[AiSummary] {lastError}");
            return (null, lastError);
        }
        catch (Exception ex)
        {
            lastError = $"Exception: {ex.GetType().Name}: {ex.Message}\n\nStack: {ex.StackTrace}";
            Console.Error.WriteLine($"[AiSummary] {lastError}");
            return (null, lastError);
        }
    }

    private static string TruncateStr(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..maxLen] + "...";
}
