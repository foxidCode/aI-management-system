using System.Security.Claims;
using System.Text;
using System.Text.Json;
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
public class AiConfigController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApiKeyEncryptionService _encryption;

    public AiConfigController(AppDbContext db, IHttpClientFactory httpClientFactory, ApiKeyEncryptionService encryption)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _encryption = encryption;
    }

    private bool IsAdmin() =>
        User.Claims.Any(c => c.Type == "permission" && (c.Value == "role:manage" || c.Value == "ai:config"));

    // ========== CRUD ==========

    /// <summary>获取所有模型配置（API Key 始终脱敏）</summary>
    [HttpGet("models")]
    public async Task<IActionResult> GetAll()
    {
        var configs = await _db.AiModelConfigs
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.UpdatedAt)
            .Select(c => new AiModelConfigResponse
            {
                Id = c.Id,
                Name = c.Name,
                Provider = c.Provider,
                ApiEndpoint = c.ApiEndpoint,
                ApiKey = MaskApiKey(c.ApiKey),
                ModelName = c.ModelName,
                MaxTokens = c.MaxTokens,
                Temperature = c.Temperature,
                IsActive = c.IsActive,
                SystemPrompt = c.SystemPrompt,
                CreatedAt = c.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            })
            .ToListAsync();

        return Ok(new { success = true, data = configs });
    }

    /// <summary>获取单个模型配置（API Key 脱敏，不返回真实密钥）</summary>
    [HttpGet("models/{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var c = await _db.AiModelConfigs.FindAsync(id);
        if (c == null) return NotFound(new { success = false, message = "配置不存在" });

        return Ok(new
        {
            success = true,
            data = new AiModelConfigResponse
            {
                Id = c.Id,
                Name = c.Name,
                Provider = c.Provider,
                ApiEndpoint = c.ApiEndpoint,
                ApiKey = MaskApiKey(c.ApiKey), // 永远不暴露真实 Key
                ModelName = c.ModelName,
                MaxTokens = c.MaxTokens,
                Temperature = c.Temperature,
                IsActive = c.IsActive,
                SystemPrompt = c.SystemPrompt,
                CreatedAt = c.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            }
        });
    }

    /// <summary>创建模型配置（API Key 加密入库）</summary>
    [HttpPost("models")]
    public async Task<IActionResult> Create([FromBody] AiModelConfigRequest req)
    {
        if (!IsAdmin()) return Forbid();

        var config = new AiModelConfig
        {
            Name = req.Name,
            Provider = req.Provider,
            ApiEndpoint = req.ApiEndpoint,
            ApiKey = _encryption.Encrypt(req.ApiKey), // 加密存储
            ModelName = req.ModelName,
            MaxTokens = req.MaxTokens > 0 ? req.MaxTokens : 131072,
            Temperature = req.Temperature,
            IsActive = req.IsActive,
            SystemPrompt = req.SystemPrompt,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        // 如果设置为活跃，先取消其他活跃状态
        if (config.IsActive)
        {
            await _db.AiModelConfigs.Where(c => c.IsActive)
                .ExecuteUpdateAsync(calls => calls.SetProperty(x => x.IsActive, false));
        }

        _db.AiModelConfigs.Add(config);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            data = new AiModelConfigResponse
            {
                Id = config.Id,
                Name = config.Name,
                Provider = config.Provider,
                ApiEndpoint = config.ApiEndpoint,
                ApiKey = MaskApiKey(config.ApiKey),
                ModelName = config.ModelName,
                MaxTokens = config.MaxTokens,
                Temperature = config.Temperature,
                IsActive = config.IsActive,
                SystemPrompt = config.SystemPrompt,
                CreatedAt = config.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            }
        });
    }

    /// <summary>更新模型配置（仅在提供新 Key 时加密更新）</summary>
    [HttpPut("models/{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] AiModelConfigRequest req)
    {
        if (!IsAdmin()) return Forbid();

        var config = await _db.AiModelConfigs.FindAsync(id);
        if (config == null) return NotFound(new { success = false, message = "配置不存在" });

        config.Name = req.Name;
        config.Provider = req.Provider;
        config.ApiEndpoint = req.ApiEndpoint;

        // 仅当用户输入了新 Key（非掩码为空）时才更新
        if (!string.IsNullOrWhiteSpace(req.ApiKey) && req.ApiKey != MaskApiKey(config.ApiKey) && !req.ApiKey.Contains("●●"))
        {
            config.ApiKey = _encryption.Encrypt(req.ApiKey);
        }

        config.ModelName = req.ModelName;
        config.MaxTokens = req.MaxTokens > 0 ? req.MaxTokens : 131072;
        config.Temperature = req.Temperature;
        config.SystemPrompt = req.SystemPrompt;
        config.UpdatedAt = DateTime.Now;

        // 如果设置为活跃，先取消其他
        if (req.IsActive && !config.IsActive)
        {
            await _db.AiModelConfigs.Where(c => c.IsActive && c.Id != id)
                .ExecuteUpdateAsync(calls => calls.SetProperty(x => x.IsActive, false));
            config.IsActive = true;
        }
        else if (!req.IsActive)
        {
            config.IsActive = false;
        }

        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>删除模型配置</summary>
    [HttpDelete("models/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdmin()) return Forbid();

        var config = await _db.AiModelConfigs.FindAsync(id);
        if (config == null) return NotFound(new { success = false, message = "配置不存在" });
        _db.AiModelConfigs.Remove(config);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ========== Test ==========

    /// <summary>测试模型连接（优先使用 ConfigId 解密存储的 Key，否则使用请求中的明文 Key）</summary>
    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] AiModelTestRequest req)
    {
        if (!IsAdmin()) return Forbid();

        var testKey = req.ApiKey;

        // 如果传了 ConfigId，从数据库加载并解密 Key（前端无法获取真实 Key）
        if (req.ConfigId.HasValue)
        {
            var storedConfig = await _db.AiModelConfigs.FindAsync(req.ConfigId.Value);
            if (storedConfig != null)
            {
                testKey = _encryption.Decrypt(storedConfig.ApiKey);
            }
        }

        // 兜底：检查是否为脱敏值
        if (string.IsNullOrEmpty(testKey) || testKey.Contains("●●"))
        {
            return Ok(new { success = false, message = "请重新输入完整的 API Key 进行测试" });
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            if (req.Provider == "anthropic")
            {
                var requestBody = new
                {
                    model = req.ModelName,
                    max_tokens = 50,
                    messages = new[] { new { role = "user", content = "Hello, respond with just 'OK'." } }
                };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, req.ApiEndpoint.TrimEnd('/') + "/v1/messages")
                {
                    Content = content
                };
                request.Headers.Add("x-api-key", testKey);
                request.Headers.Add("anthropic-version", "2023-06-01");

                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                    return Ok(new { success = true, message = "连接成功！模型响应正常。" });
                else
                {
                    var errBody = await response.Content.ReadAsStringAsync();
                    return Ok(new { success = false, message = $"API 返回错误 ({response.StatusCode}): {errBody[..Math.Min(errBody.Length, 300)]}" });
                }
            }
            else
            {
                var requestBody = new
                {
                    model = req.ModelName,
                    max_tokens = 50,
                    messages = new[] { new { role = "user", content = "Hello, respond with just 'OK'." } }
                };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, req.ApiEndpoint.TrimEnd('/') + "/v1/chat/completions")
                {
                    Content = content
                };
                request.Headers.Add("Authorization", $"Bearer {testKey}");

                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                    return Ok(new { success = true, message = "连接成功！模型响应正常。" });
                else
                {
                    var errBody = await response.Content.ReadAsStringAsync();
                    return Ok(new { success = false, message = $"API 返回错误 ({response.StatusCode}): {errBody[..Math.Min(errBody.Length, 300)]}" });
                }
            }
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = $"连接失败: {ex.Message}" });
        }
    }

    // ========== Helpers ==========

    private static string MaskApiKey(string key)
    {
        // 加密后的 Key 可能很长（Base64），直接全掩码
        if (string.IsNullOrEmpty(key)) return "●●●●●●";
        if (key.Length <= 8) return "●●●●●●";
        return key[..4] + "●●●●" + key[^4..];
    }
}
