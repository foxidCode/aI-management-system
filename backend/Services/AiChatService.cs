using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Models;

namespace backend.Services;

/// <summary>
/// AI 对话服务：会话管理、消息持久化、LLM API 调用、流式响应
/// </summary>
public class AiChatService
{
    private readonly AppDbContext _db;
    private readonly AiKnowledgeService _knowledgeService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApiKeyEncryptionService _encryption;

    public AiChatService(AppDbContext db, AiKnowledgeService knowledgeService, IHttpClientFactory httpClientFactory, ApiKeyEncryptionService encryption)
    {
        _db = db;
        _knowledgeService = knowledgeService;
        _httpClientFactory = httpClientFactory;
        _encryption = encryption;
    }

    // ========== Session Management ==========

    public async Task<List<ChatSessionResponse>> GetSessionsAsync(int userId)
    {
        return await _db.ChatSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new ChatSessionResponse
            {
                Id = s.Id,
                Title = s.Title,
                ModelConfigId = s.ModelConfigId,
                ModelName = s.ModelConfigId != null
                    ? _db.AiModelConfigs.Where(c => c.Id == s.ModelConfigId).Select(c => c.ModelName).FirstOrDefault()
                    : null,
                MessageCount = s.MessageCount,
                CreatedAt = s.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                UpdatedAt = s.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            })
            .ToListAsync();
    }

    public async Task<ChatSessionResponse> CreateSessionAsync(int userId, CreateSessionRequest req)
    {
        // 如果未指定模型配置，自动使用当前启用的配置
        var modelConfigId = req.ModelConfigId
            ?? (await _db.AiModelConfigs.FirstOrDefaultAsync(c => c.IsActive))?.Id;

        var session = new ChatSession
        {
            UserId = userId,
            Title = req.Title ?? "新对话",
            ModelConfigId = modelConfigId,
            MessageCount = 0,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        _db.ChatSessions.Add(session);
        await _db.SaveChangesAsync();

        var modelName = modelConfigId != null
            ? await _db.AiModelConfigs.Where(c => c.Id == modelConfigId).Select(c => c.ModelName).FirstOrDefaultAsync()
            : null;

        return new ChatSessionResponse
        {
            Id = session.Id,
            Title = session.Title,
            ModelConfigId = session.ModelConfigId,
            ModelName = modelName,
            MessageCount = 0,
            CreatedAt = session.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            UpdatedAt = session.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        };
    }

    public async Task<bool> DeleteSessionAsync(int sessionId, int userId)
    {
        var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
        if (session == null) return false;
        _db.ChatSessions.Remove(session); // Cascade will delete messages
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<ChatMessageListResponse?> GetMessagesAsync(int sessionId, int userId)
    {
        var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
        if (session == null) return null;

        var messages = await _db.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageResponse
            {
                Id = m.Id,
                SessionId = m.SessionId,
                Role = m.Role,
                Content = m.Content,
                TokenCount = m.TokenCount,
                CreatedAt = m.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            })
            .ToListAsync();

        var modelName = session.ModelConfigId != null
            ? await _db.AiModelConfigs.Where(c => c.Id == session.ModelConfigId).Select(c => c.ModelName).FirstOrDefaultAsync()
            : null;

        return new ChatMessageListResponse { Messages = messages, ModelName = modelName };
    }

    // ========== Core: Streaming Chat ==========

    /// <summary>
    /// 发送消息并流式返回 AI 回复（写入 HttpResponse）
    /// </summary>
    public async Task SendMessageStreamAsync(int userId, SendMessageRequest req, HttpResponse response)
    {
        // 1. 获取会话
        var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == req.SessionId && s.UserId == userId);
        if (session == null)
        {
            await WriteSseError(response, "会话不存在");
            return;
        }

        // 2. 保存用户消息
        var userMsg = new ChatMessage
        {
            SessionId = req.SessionId,
            Role = "user",
            Content = req.Message,
            TokenCount = EstimateTokens(req.Message),
            CreatedAt = DateTime.Now
        };
        _db.ChatMessages.Add(userMsg);
        session.MessageCount++;
        session.UpdatedAt = DateTime.Now;
        // Auto-title: use first message as title
        if (session.Title == "新对话" && session.MessageCount == 1)
        {
            session.Title = req.Message.Length > 50 ? req.Message[..50] + "..." : req.Message;
        }
        await _db.SaveChangesAsync();

        // 3. 获取活跃的模型配置
        var config = await GetActiveConfigAsync(session.ModelConfigId);
        if (config == null)
        {
            await WriteSseError(response, "暂未启用 AI 模型，请联系管理员在「AI配置」中启用一个模型");
            return;
        }

        // 4. 检索知识库
        var knowledgeEntries = await _knowledgeService.SearchAsync(req.Message, topK: 3);

        // 5. 获取历史消息（构建对话上下文）
        var historyMessages = await _db.ChatMessages
            .Where(m => m.SessionId == req.SessionId && m.Id != userMsg.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20) // 最近 20 条
            .OrderBy(m => m.CreatedAt) // 恢复正序
            .ToListAsync();

        // 6. 构建系统提示词（强约束：必须基于知识库回答）
        var defaultSystemPrompt = "你是本管理系统的 AI 助手，专门帮助用户熟悉和使用本系统的功能。" +
            "\n\n## 核心规则（必须严格遵守）" +
            "\n1. **知识库优先**：你的回答必须基于下方提供的「知识库内容」。知识库是系统的权威文档。" +
            "\n2. **禁止编造**：如果知识库中没有相关信息，直接告诉用户「抱歉，我目前的知识库中暂无与此相关的信息，建议联系管理员或查阅系统文档。」不要猜测、编造或使用外部知识。" +
            "\n3. **具体操作**：回答时给出精确的菜单位置（如「左侧菜单 → 用户列表」）和操作步骤。" +
            "\n4. **中文回答**：保持专业、清晰、友好的中文风格。";

        var systemPrompt = !string.IsNullOrWhiteSpace(config.SystemPrompt)
            ? config.SystemPrompt + "\n\n" + defaultSystemPrompt
            : defaultSystemPrompt;

        if (knowledgeEntries.Count > 0)
        {
            systemPrompt += "\n\n## 📚 知识库内容（权威参考，请严格据此回答）\n" +
                "以下内容来自系统知识库，**你必须严格按照以下内容回答用户问题**，不要添加知识库中未提及的功能、按钮或操作步骤。\n\n" +
                string.Join("\n---\n", knowledgeEntries.Select(k => $"### {k.Title}\n{k.Content}"));
        }
        else
        {
            systemPrompt += "\n\n## ⚠️ 未检索到相关知识\n" +
                "当前知识库中未找到与用户问题直接匹配的内容。**请告知用户你暂时无法提供准确答案**，建议用户查阅系统文档或联系管理员。禁止猜测或编造。";
        }

        // 7. 调用 LLM API 流式返回
        await StreamFromLLm(config, systemPrompt, historyMessages, req.Message, response);

        // 8. 读取流式响应中的完整回复（由 StreamFromLLm 在流式过程中已逐步收集）
        // 注意：由于流式响应在 HTTP 层面是实时的，我们无法在此处获取完整内容。
        // 改为在 StreamFromLLm 内部完成后保存消息。
    }

    /// <summary>
    /// 保存 AI 回复消息（由控制器在流式响应完成后调用）
    /// </summary>
    public async Task SaveAssistantMessageAsync(int sessionId, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var msg = new ChatMessage
        {
            SessionId = sessionId,
            Role = "assistant",
            Content = content,
            TokenCount = EstimateTokens(content),
            CreatedAt = DateTime.Now
        };
        _db.ChatMessages.Add(msg);

        var session = await _db.ChatSessions.FindAsync(sessionId);
        if (session != null)
        {
            session.MessageCount++;
            session.UpdatedAt = DateTime.Now;
        }

        await _db.SaveChangesAsync();
    }

    // ========== Private Helpers ==========

    private async Task<AiModelConfig?> GetActiveConfigAsync(int? preferredConfigId)
    {
        if (preferredConfigId.HasValue)
        {
            var config = await _db.AiModelConfigs.FindAsync(preferredConfigId.Value);
            if (config != null) return config;
        }

        return await _db.AiModelConfigs.FirstOrDefaultAsync(c => c.IsActive);
    }

    private async Task StreamFromLLm(AiModelConfig config, string systemPrompt,
        List<ChatMessage> history, string userMessage, HttpResponse response)
    {
        if (config.Provider == "anthropic")
        {
            await StreamFromAnthropic(config, systemPrompt, history, userMessage, response);
        }
        else
        {
            // Default: OpenAI-compatible (DeepSeek, ChatGPT, etc.)
            await StreamFromOpenAiCompatible(config, systemPrompt, history, userMessage, response);
        }
    }

    private async Task StreamFromOpenAiCompatible(AiModelConfig config, string systemPrompt,
        List<ChatMessage> history, string userMessage, HttpResponse response)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(3);

        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        // 添加历史消息
        foreach (var msg in history)
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }

        // 添加当前用户消息
        messages.Add(new { role = "user", content = userMessage });

        var requestBody = new
        {
            model = config.ModelName,
            messages,
            max_tokens = config.MaxTokens,
            temperature = config.Temperature,
            stream = true
        };

        var jsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var json = JsonSerializer.Serialize(requestBody, jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, config.ApiEndpoint.TrimEnd('/') + "/v1/chat/completions")
        {
            Content = content
        };
        request.Headers.Add("Authorization", $"Bearer {_encryption.Decrypt(config.ApiKey)}");

        var httpResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorBody = await httpResponse.Content.ReadAsStringAsync();
            await WriteSseError(response, $"AI API 错误 ({httpResponse.StatusCode}): {Truncate(errorBody, 200)}");
            return;
        }

        await WriteSseEvent(response, new { type = "start" });

        var fullContent = new StringBuilder();
        using var stream = await httpResponse.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..];
            if (data == "[DONE]") break;

            try
            {
                using var doc = JsonDocument.Parse(data);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() == 0) continue;

                var delta = choices[0].GetProperty("delta");
                if (delta.TryGetProperty("content", out var contentProp))
                {
                    var chunk = contentProp.GetString() ?? "";
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        fullContent.Append(chunk);
                        await WriteSseEvent(response, new { type = "chunk", content = chunk });
                    }
                }
            }
            catch (JsonException)
            {
                // 忽略解析错误的行
            }
        }

        await WriteSseEvent(response, new { type = "done", fullContent = fullContent.ToString() });
    }

    private async Task StreamFromAnthropic(AiModelConfig config, string systemPrompt,
        List<ChatMessage> history, string userMessage, HttpResponse response)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(3);

        // Anthropic 消息格式：将历史+当前消息转为 Anthropic 格式
        var anthropicMessages = new List<object>();

        foreach (var msg in history)
        {
            anthropicMessages.Add(new
            {
                role = msg.Role,
                content = msg.Content
            });
        }
        anthropicMessages.Add(new { role = "user", content = userMessage });

        var requestBody = new
        {
            model = config.ModelName,
            max_tokens = config.MaxTokens,
            temperature = config.Temperature,
            system = systemPrompt,
            messages = anthropicMessages,
            stream = true
        };

        var jsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var json = JsonSerializer.Serialize(requestBody, jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, config.ApiEndpoint.TrimEnd('/') + "/v1/messages")
        {
            Content = content
        };
        request.Headers.Add("x-api-key", _encryption.Decrypt(config.ApiKey));
        request.Headers.Add("anthropic-version", "2023-06-01");

        var httpResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorBody = await httpResponse.Content.ReadAsStringAsync();
            await WriteSseError(response, $"Anthropic API 错误 ({httpResponse.StatusCode}): {Truncate(errorBody, 200)}");
            return;
        }

        await WriteSseEvent(response, new { type = "start" });

        var fullContent = new StringBuilder();
        using var stream = await httpResponse.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..];

            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;

                // Anthropic 流式事件类型
                var eventType = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

                if (eventType == "content_block_delta")
                {
                    var delta = root.GetProperty("delta");
                    if (delta.TryGetProperty("text", out var textProp))
                    {
                        var chunk = textProp.GetString() ?? "";
                        if (!string.IsNullOrEmpty(chunk))
                        {
                            fullContent.Append(chunk);
                            await WriteSseEvent(response, new { type = "chunk", content = chunk });
                        }
                    }
                }
                else if (eventType == "message_stop")
                {
                    // 流结束
                    break;
                }
                else if (eventType == "error")
                {
                    var errorMsg = root.TryGetProperty("error", out var errProp)
                        ? errProp.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown"
                        : "Unknown error";
                    await WriteSseError(response, $"Anthropic 错误: {errorMsg}");
                    return;
                }
            }
            catch (JsonException)
            {
                // 忽略解析错误的行
            }
        }

        await WriteSseEvent(response, new { type = "done", fullContent = fullContent.ToString() });
    }

    // ========== SSE Helpers ==========

    private static async Task WriteSseEvent(HttpResponse response, object data)
    {
        var json = JsonSerializer.Serialize(data);
        var bytes = Encoding.UTF8.GetBytes($"data: {json}\n\n");
        await response.Body.WriteAsync(bytes);
        await response.Body.FlushAsync();
    }

    private static async Task WriteSseError(HttpResponse response, string message)
    {
        await WriteSseEvent(response, new { type = "error", content = message });
    }

    private static int EstimateTokens(string text)
    {
        // 粗略估算：中文约 1.5 字符/token，英文约 4 字符/token
        if (string.IsNullOrEmpty(text)) return 0;
        var chineseChars = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
        var otherChars = text.Length - chineseChars;
        return (int)(chineseChars / 1.5 + otherChars / 4.0);
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + "...";

    // ========== Admin Session Management ==========

    /// <summary>管理员获取所有会话（支持按日期和用户筛选，排除空会话）</summary>
    public async Task<AdminSessionListResponse> GetAllSessionsAdminAsync(AdminSessionFilterRequest filter)
    {
        var query = _db.ChatSessions.AsQueryable();

        if (filter.UserId.HasValue)
            query = query.Where(s => s.UserId == filter.UserId.Value);

        if (!string.IsNullOrWhiteSpace(filter.StartDate) && DateTime.TryParse(filter.StartDate, out var startDate))
            query = query.Where(s => s.CreatedAt >= startDate);

        if (!string.IsNullOrWhiteSpace(filter.EndDate) && DateTime.TryParse(filter.EndDate, out var endDate))
            query = query.Where(s => s.CreatedAt < endDate.AddDays(1));

        var total = await query.CountAsync();

        var list = await query
            .OrderByDescending(s => s.UpdatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(s => new AdminSessionResponse
            {
                Id = s.Id,
                Title = s.Title,
                UserId = s.UserId,
                Username = _db.Users.Where(u => u.Id == s.UserId).Select(u => u.Username).FirstOrDefault() ?? "未知",
                ModelName = s.ModelConfigId != null
                    ? _db.AiModelConfigs.Where(c => c.Id == s.ModelConfigId).Select(c => c.ModelName).FirstOrDefault()
                    : null,
                MessageCount = s.MessageCount,
                CreatedAt = s.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                UpdatedAt = s.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            })
            .ToListAsync();

        return new AdminSessionListResponse { List = list, Total = total };
    }

    /// <summary>管理员删除任意会话</summary>
    public async Task<bool> AdminDeleteSessionAsync(int sessionId)
    {
        var session = await _db.ChatSessions.FindAsync(sessionId);
        if (session == null) return false;
        _db.ChatSessions.Remove(session);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>管理员批量删除会话</summary>
    public async Task<int> AdminBatchDeleteSessionsAsync(List<int> ids)
    {
        var sessions = await _db.ChatSessions.Where(s => ids.Contains(s.Id)).ToListAsync();
        _db.ChatSessions.RemoveRange(sessions);
        return await _db.SaveChangesAsync();
    }
}
