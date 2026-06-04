using System.Data;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.DTOs;
using backend.Models;

namespace backend.Services;

public class IntegrationService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    // Token 缓存（用于自动登录的连接）
    private static readonly Dictionary<string, (string token, DateTime expiry)> _tokenCache = new();
    private static readonly object _tokenLock = new();

    public IntegrationService(AppDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    // ========== 连接管理 ==========

    public async Task<List<IntegrationConnectionResponse>> GetConnectionsAsync()
    {
        return await _db.IntegrationConnections.OrderBy(c => c.Id).Select(c => new IntegrationConnectionResponse
        {
            Id = c.Id, Name = c.Name, Description = c.Description,
            BaseUrl = c.BaseUrl, AuthType = c.AuthType,
            DefaultHeaders = c.DefaultHeaders, CreatedAt = c.CreatedAt,
        }).ToListAsync();
    }

    public async Task<IntegrationConnectionResponse?> CreateConnectionAsync(IntegrationConnectionRequest req)
    {
        var config = new IntegrationConnection
        {
            Name = req.Name, Description = req.Description, BaseUrl = req.BaseUrl.TrimEnd('/'),
            AuthType = req.AuthType, AuthConfig = req.AuthConfig, DefaultHeaders = req.DefaultHeaders,
            CreatedAt = DateTime.Now,
        };
        _db.IntegrationConnections.Add(config);
        await _db.SaveChangesAsync();
        return Map(config);
    }

    public async Task<IntegrationConnectionResponse?> UpdateConnectionAsync(int id, IntegrationConnectionRequest req)
    {
        var c = await _db.IntegrationConnections.FindAsync(id);
        if (c == null) return null;
        c.Name = req.Name; c.Description = req.Description; c.BaseUrl = req.BaseUrl.TrimEnd('/');
        c.AuthType = req.AuthType; c.AuthConfig = req.AuthConfig; c.DefaultHeaders = req.DefaultHeaders;
        await _db.SaveChangesAsync();
        return Map(c);
    }

    public async Task<bool> DeleteConnectionAsync(int id)
    {
        var c = await _db.IntegrationConnections.FindAsync(id);
        if (c == null) return false;
        _db.IntegrationConnections.Remove(c);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> TestConnectionAsync(IntegrationConnectionRequest req)
    {
        var client = await CreateHttpClientAsync(req.AuthType, req.AuthConfig, req.DefaultHeaders, req.BaseUrl);
        var url = req.BaseUrl.TrimEnd('/');
        var response = await client.GetAsync(url);
        return response.IsSuccessStatusCode;
    }

    // ========== 任务管理 ==========

    public async Task<List<IntegrationTaskResponse>> GetTasksAsync()
    {
        var conns = await _db.IntegrationConnections.ToDictionaryAsync(c => c.Id, c => c.Name);
        return await _db.IntegrationTasks.OrderBy(t => t.Id).Select(t => new IntegrationTaskResponse
        {
            Id = t.Id, Name = t.Name, Description = t.Description,
            SourceConnectionId = t.SourceConnectionId, SourcePath = t.SourcePath,
            SourceMethod = t.SourceMethod, SourceContentType = t.SourceContentType,
            SourceBody = t.SourceBody, ResponseDataPath = t.ResponseDataPath,
            TargetType = t.TargetType, TargetConnectionId = t.TargetConnectionId,
            TargetPath = t.TargetPath, TargetContentType = t.TargetContentType,
            TargetMethod = t.TargetMethod,
            DbTableName = t.DbTableName, DbChildConfig = t.DbChildConfig,
            FieldMappings = t.FieldMappings, CodeHandler = t.CodeHandler,
            BeforeExecute = t.BeforeExecute, AfterExecute = t.AfterExecute,
            IsActive = t.IsActive, CreatedAt = t.CreatedAt, UpdatedAt = t.UpdatedAt,
        }).ToListAsync().ContinueWith(t =>
        {
            var list = t.Result;
            foreach (var item in list)
            {
                if (item.SourceConnectionId.HasValue) item.SourceConnectionName = conns.GetValueOrDefault(item.SourceConnectionId.Value);
                if (item.TargetConnectionId.HasValue) item.TargetConnectionName = conns.GetValueOrDefault(item.TargetConnectionId.Value);
            }
            return list;
        });
    }

    public async Task<IntegrationTaskResponse?> CreateTaskAsync(IntegrationTaskRequest req)
    {
        var task = new IntegrationTask
        {
            Name = req.Name, Description = req.Description,
            SourceConnectionId = req.SourceConnectionId, SourcePath = req.SourcePath,
            SourceMethod = req.SourceMethod, SourceContentType = req.SourceContentType,
            SourceBody = req.SourceBody, ResponseDataPath = req.ResponseDataPath,
            TargetType = req.TargetType, TargetConnectionId = req.TargetConnectionId,
            TargetPath = req.TargetPath, TargetContentType = req.TargetContentType,
            TargetMethod = req.TargetMethod,
            DbTableName = req.DbTableName, DbChildConfig = req.DbChildConfig,
            FieldMappings = req.FieldMappings, CodeHandler = req.CodeHandler,
            BeforeExecute = req.BeforeExecute, AfterExecute = req.AfterExecute,
            IsActive = req.IsActive, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now,
        };
        _db.IntegrationTasks.Add(task);
        await _db.SaveChangesAsync();
        return (await GetTasksAsync()).FirstOrDefault(t => t.Id == task.Id);
    }

    public async Task<IntegrationTaskResponse?> UpdateTaskAsync(int id, IntegrationTaskRequest req)
    {
        var t = await _db.IntegrationTasks.FindAsync(id);
        if (t == null) return null;
        t.Name = req.Name; t.Description = req.Description;
        t.SourceConnectionId = req.SourceConnectionId; t.SourcePath = req.SourcePath;
        t.SourceMethod = req.SourceMethod; t.SourceContentType = req.SourceContentType;
        t.SourceBody = req.SourceBody; t.ResponseDataPath = req.ResponseDataPath;
        t.TargetType = req.TargetType; t.TargetConnectionId = req.TargetConnectionId;
        t.TargetPath = req.TargetPath; t.TargetContentType = req.TargetContentType;
        t.TargetMethod = req.TargetMethod;
        t.DbTableName = req.DbTableName; t.DbChildConfig = req.DbChildConfig;
        t.FieldMappings = req.FieldMappings; t.CodeHandler = req.CodeHandler;
        t.BeforeExecute = req.BeforeExecute; t.AfterExecute = req.AfterExecute;
        t.IsActive = req.IsActive; t.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return (await GetTasksAsync()).FirstOrDefault(x => x.Id == id);
    }

    public async Task<bool> DeleteTaskAsync(int id)
    {
        var t = await _db.IntegrationTasks.FindAsync(id);
        if (t == null) return false;
        _db.IntegrationTasks.Remove(t);
        await _db.SaveChangesAsync();
        return true;
    }

    // ========== 执行任务 ==========

    public async Task<ExecuteTaskResponse> ExecuteTaskAsync(int taskId)
    {
        var sw = Stopwatch.StartNew();
        var task = await _db.IntegrationTasks.FindAsync(taskId)
            ?? throw new InvalidOperationException("任务不存在");

        var result = new ExecuteTaskResponse();

        try
        {
            // 0. 调用前事件
            if (!string.IsNullOrEmpty(task.BeforeExecute))
                await ExecuteCodeHandlerAsync(task.BeforeExecute, null);

            // 1. 拉取数据
            JsonElement? pullData = null;
            if (task.SourceConnectionId.HasValue && !string.IsNullOrEmpty(task.SourcePath))
            {
                var srcConn = await _db.IntegrationConnections.FindAsync(task.SourceConnectionId.Value)
                    ?? throw new InvalidOperationException("来源连接不存在");

                var srcClient = await CreateHttpClientAsync(srcConn.AuthType, srcConn.AuthConfig, srcConn.DefaultHeaders, srcConn.BaseUrl);
                var srcUrl = $"{srcConn.BaseUrl}{task.SourcePath}";

                HttpResponseMessage pullResponse;
                string? pullRequestBody = null;
                if (task.SourceMethod == "POST")
                {
                    pullRequestBody = task.SourceBody ?? (task.SourceContentType?.Contains("xml") == true ? "<root/>" : "{}");
                    var srcContentType = string.IsNullOrWhiteSpace(task.SourceContentType) ? "application/json" : task.SourceContentType;
                    var content = new StringContent(pullRequestBody, Encoding.UTF8, srcContentType);
                    pullResponse = await srcClient.PostAsync(srcUrl, content);
                }
                else
                {
                    pullResponse = await srcClient.GetAsync(srcUrl);
                }

                var pullBody = await pullResponse.Content.ReadAsStringAsync();
                result.PullResponse = pullBody;

                // 记录拉取日志（含请求头，方便排查认证问题）
                var srcHeaders = new List<string>();
                foreach (var h in srcClient.DefaultRequestHeaders)
                    srcHeaders.Add($"{h.Key}: {string.Join(", ", h.Value)}");
                AddLog(taskId, "Pull", pullResponse.IsSuccessStatusCode ? "Success" : "Fail",
                    srcUrl, pullRequestBody, pullBody, string.Join("\n", srcHeaders),
                    pullResponse.IsSuccessStatusCode ? null : $"HTTP {pullResponse.StatusCode}",
                    sw.ElapsedMilliseconds);

                if (!pullResponse.IsSuccessStatusCode)
                    throw new InvalidOperationException($"拉取失败: HTTP {pullResponse.StatusCode} - {Truncate(pullBody, 500)}");

                if (!string.IsNullOrWhiteSpace(pullBody))
                    pullData = JsonSerializer.Deserialize<JsonElement>(pullBody);

                // 根据 ResponseDataPath 从响应中提取数据
                // 如配置 "data.rows" → 从 {data:{rows:[...]}} 中提取数组
                // 如配置 "data" → 从 {data:[...]} 中提取数组
                // 留空 → 自动尝试常见解包
                if (pullData != null && pullData.Value.ValueKind == JsonValueKind.Object)
                {
                    if (!string.IsNullOrWhiteSpace(task.ResponseDataPath))
                    {
                        pullData = ExtractJsonElementByPath(pullData.Value, task.ResponseDataPath);
                    }
                    else
                    {
                        // 自动解包 {success:true, data:[...]}
                        if (pullData.Value.TryGetProperty("data", out var dataProp)
                            && dataProp.ValueKind == JsonValueKind.Array)
                            pullData = dataProp;
                        // 自动解包 {data: {rows: [...]}}
                        else if (pullData.Value.TryGetProperty("data", out var dataObj)
                            && dataObj.ValueKind == JsonValueKind.Object
                            && dataObj.TryGetProperty("rows", out var rowsProp)
                            && rowsProp.ValueKind == JsonValueKind.Array)
                            pullData = rowsProp;
                    }
                }
            }

            // 2. 数据转换（保存原始数据副本用于子表提取）
            JsonElement? transformed = pullData;
            var originalPullData = pullData; // 子表字段需要从原始数据中提取

            // 应用字段映射
            if (pullData != null && !string.IsNullOrEmpty(task.FieldMappings))
            {
                var mappings = JsonSerializer.Deserialize<List<FieldMappingDef>>(task.FieldMappings,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new List<FieldMappingDef>();

                if (mappings.Count > 0)
                {
                    transformed = await ApplyFieldMappingsAsync(pullData.Value, mappings);
                    result.TransformedData = JsonSerializer.Serialize(transformed);
                }
            }

            // 执行代码处理器
            if (!string.IsNullOrEmpty(task.CodeHandler))
            {
                transformed = await ExecuteCodeHandlerAsync(task.CodeHandler, transformed ?? pullData);
                if (transformed != null)
                    result.TransformedData = JsonSerializer.Serialize(transformed);
            }

            // 3. 推送数据
            if (transformed != null)
            {
                if (task.TargetType == "Database" && !string.IsNullOrEmpty(task.DbTableName))
                {
                    // 存入本系统数据库（传入原始数据用于子表字段提取）
                    var saveResult = await SaveToDatabaseAsync(task, transformed.Value, originalPullData, taskId);
                    result.PushResponse = saveResult;
                }
                else if (task.TargetConnectionId.HasValue && !string.IsNullOrEmpty(task.TargetPath))
                {
                    // 推送到外部API
                    var tgtConn = await _db.IntegrationConnections.FindAsync(task.TargetConnectionId.Value)
                        ?? throw new InvalidOperationException("目标连接不存在");

                    var tgtClient = await CreateHttpClientAsync(tgtConn.AuthType, tgtConn.AuthConfig, tgtConn.DefaultHeaders, tgtConn.BaseUrl);
                    var tgtUrl = $"{tgtConn.BaseUrl}{task.TargetPath}";
                    // 使用 GetRawText() 直接获取 JsonElement 的原始 JSON
                    var pushData = transformed.Value;
                    if (pushData.ValueKind == JsonValueKind.Array)
                    {
                        var arr = pushData.EnumerateArray().ToList();
                        pushData = arr.Count == 1 ? arr[0] : pushData;
                    }

                    // 如果配置了子表，从本地数据库查询明细并附加到 Items 字段
                    if (!string.IsNullOrEmpty(task.DbChildConfig))
                    {
                        pushData = await AttachChildDataForApiAsync(task, pushData, originalPullData);
                    }

                    var pushRequestBody = pushData.GetRawText();
                    var tgtContentType = string.IsNullOrWhiteSpace(task.TargetContentType) ? "application/json" : task.TargetContentType;
                    var pushContent = new StringContent(pushRequestBody, Encoding.UTF8, tgtContentType);
                    var pushResponse = await tgtClient.PostAsync(tgtUrl, pushContent);
                    var pushBody = await pushResponse.Content.ReadAsStringAsync();
                    result.PushResponse = pushBody;

                    var tgtHeaders = new List<string>();
                    foreach (var h in tgtClient.DefaultRequestHeaders)
                        tgtHeaders.Add($"{h.Key}: {string.Join(", ", h.Value)}");
                    AddLog(taskId, "Push", pushResponse.IsSuccessStatusCode ? "Success" : "Fail",
                        tgtUrl, pushRequestBody, pushBody, string.Join("\n", tgtHeaders),
                        pushResponse.IsSuccessStatusCode ? null : $"HTTP {pushResponse.StatusCode}",
                        sw.ElapsedMilliseconds);

                    if (!pushResponse.IsSuccessStatusCode)
                        throw new InvalidOperationException($"推送失败: HTTP {pushResponse.StatusCode} - {Truncate(pushBody, 500)}");
                }
            }

            // 4. 调用后事件
            if (!string.IsNullOrEmpty(task.AfterExecute))
                await ExecuteCodeHandlerAsync(task.AfterExecute, transformed ?? pullData);

            await _db.SaveChangesAsync();
            sw.Stop();
            result.Success = true;
            result.Message = "执行成功";
            result.DurationMs = sw.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.Success = false;
            result.Message = ex.Message;
            result.DurationMs = sw.ElapsedMilliseconds;

            _db.IntegrationLogs.Add(new IntegrationLog
            {
                TaskId = taskId, Direction = "Execute", Status = "Fail",
                RequestUrl = result.PullResponse != null ? "见 PullResponse" : null,
                RequestBody = Truncate(task.SourceBody),
                ResponseData = Truncate(result.PullResponse ?? result.PushResponse),
                ErrorMessage = ex.Message,
                DurationMs = sw.ElapsedMilliseconds,
                ExecutedAt = DateTime.Now,
            });
            await _db.SaveChangesAsync();
        }

        return result;
    }

    // ========== 执行日志 ==========

    public async Task<IntegrationLogListResponse> GetLogsAsync(int? taskId, int page = 1, int pageSize = 20)
    {
        var q = _db.IntegrationLogs.AsQueryable();
        if (taskId.HasValue) q = q.Where(l => l.TaskId == taskId.Value);
        var total = await q.CountAsync();
        var list = await q.OrderByDescending(l => l.ExecutedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new IntegrationLogResponse
            {
                Id = l.Id, TaskId = l.TaskId,
                Direction = l.Direction, Status = l.Status,
                RequestUrl = l.RequestUrl, RequestBody = l.RequestBody,
                ResponseData = l.ResponseData, RequestHeaders = l.RequestHeaders,
                ErrorMessage = l.ErrorMessage,
                DurationMs = l.DurationMs, ExecutedAt = l.ExecutedAt,
            }).ToListAsync();

        var taskNames = await _db.IntegrationTasks.ToDictionaryAsync(t => t.Id, t => t.Name);
        foreach (var l in list) l.TaskName = taskNames.GetValueOrDefault(l.TaskId);

        return new IntegrationLogListResponse { List = list, Total = total };
    }

    // ========== 辅助方法 ==========

    private async Task<HttpClient> CreateHttpClientAsync(string authType, string? authConfig, string? defaultHeaders, string? baseUrl = null)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        // 认证
        if (!string.IsNullOrEmpty(authConfig))
        {
            try
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, object>>(authConfig,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                switch (authType)
                {
                    case "Basic":
                        var user = config?.GetValueOrDefault("username")?.ToString() ?? "";
                        var pass = config?.GetValueOrDefault("password")?.ToString() ?? "";
                        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
                        break;
                    case "Bearer":
                        var token = await GetBearerTokenAsync(config, baseUrl);
                        if (!string.IsNullOrEmpty(token))
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        break;
                    case "ApiKey":
                        var key = config?.GetValueOrDefault("key")?.ToString() ?? "";
                        var value = config?.GetValueOrDefault("value")?.ToString() ?? "";
                        var location = config?.GetValueOrDefault("in")?.ToString() ?? "Header";
                        if (location == "Header") client.DefaultRequestHeaders.Add(key, value);
                        break;
                    case "Chain":
                        // 链式认证：按序执行多个步骤，提取变量，支持 {{var}} 模板替换
                        // Config: { "steps":[{ "url":"...", "method":"POST", "body":"...", "extractField":"data.xxx", "saveAs":"varName" }], "headerName":"X-Token", "headerTemplate":"{{varName}}" }
                        var chainConfig = JsonSerializer.Deserialize<ChainAuthConfig>(authConfig,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (chainConfig?.Steps != null && chainConfig.Steps.Count > 0)
                        {
                            var variables = new Dictionary<string, string>();
                            using var chainClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

                            foreach (var step in chainConfig.Steps)
                            {
                                // 替换 body 中的模板变量
                                var stepBody = step.Body ?? "{}";
                                foreach (var kv in variables)
                                    stepBody = stepBody.Replace($"{{{{{kv.Key}}}}}", kv.Value);

                                var fullUrl = (step.Url ?? "").StartsWith("http") ? step.Url : $"{baseUrl?.TrimEnd('/')}{step.Url}";
                                var method = (step.Method ?? "POST").ToUpper();
                                HttpResponseMessage stepResp;
                                if (method == "GET")
                                    stepResp = await chainClient.GetAsync(fullUrl);
                                else
                                    stepResp = await chainClient.PostAsync(fullUrl,
                                        new StringContent(stepBody, Encoding.UTF8, "application/json"));

                                var stepJson = await stepResp.Content.ReadAsStringAsync();
                                if (!string.IsNullOrEmpty(step.ExtractField) && !string.IsNullOrEmpty(step.SaveAs))
                                {
                                    var extracted = ExtractJsonPath(stepJson, step.ExtractField);
                                    if (!string.IsNullOrEmpty(extracted))
                                        variables[step.SaveAs] = extracted;
                                }
                            }

                            // 应用最终请求头
                            if (!string.IsNullOrEmpty(chainConfig.HeaderName))
                            {
                                var headerValue = chainConfig.HeaderTemplate ?? "";
                                foreach (var kv in variables)
                                    headerValue = headerValue.Replace($"{{{{{kv.Key}}}}}", kv.Value);
                                client.DefaultRequestHeaders.TryAddWithoutValidation(chainConfig.HeaderName, headerValue);
                            }
                        }
                        break;
                }
            }
            catch { /* 配置解析失败不阻塞 */ }
        }

        // 默认请求头
        if (!string.IsNullOrEmpty(defaultHeaders))
        {
            try
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(defaultHeaders);
                if (headers != null)
                    foreach (var h in headers)
                        client.DefaultRequestHeaders.TryAddWithoutValidation(h.Key, h.Value);
            }
            catch { }
        }

        return client;
    }

    /// <summary>获取 Bearer Token（支持缓存和自动登录）</summary>
    private static async Task<string?> GetBearerTokenAsync(Dictionary<string, object>? config, string? baseUrl)
    {
        if (config == null) return null;

        // 直接配置的 token
        if (config.TryGetValue("token", out var directToken))
            return directToken?.ToString();

        // 自动登录获取 token
        var loginUrl = config.GetValueOrDefault("loginUrl")?.ToString();
        var loginBody = config.GetValueOrDefault("loginBody")?.ToString();
        if (string.IsNullOrEmpty(loginUrl) || string.IsNullOrEmpty(loginBody)) return null;

        var tokenField = config.GetValueOrDefault("tokenField")?.ToString() ?? "data.token";
        var cacheKey = $"{baseUrl}:{loginUrl}:{loginBody}";

        // 检查缓存
        lock (_tokenLock)
        {
            if (_tokenCache.TryGetValue(cacheKey, out var cached) && cached.expiry > DateTime.Now.AddMinutes(1))
                return cached.token;
        }

        // 调用登录接口
        try
        {
            using var loginClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var fullLoginUrl = loginUrl.StartsWith("http") ? loginUrl : $"{baseUrl?.TrimEnd('/')}{loginUrl}";
            var content = new StringContent(loginBody, Encoding.UTF8, "application/json");
            var response = await loginClient.PostAsync(fullLoginUrl, content);
            var body = await response.Content.ReadAsStringAsync();

            // 从响应中提取 token（支持嵌套路径如 data.token）
            var token = ExtractJsonPath(body, tokenField);

            if (!string.IsNullOrEmpty(token))
            {
                lock (_tokenLock) { _tokenCache[cacheKey] = (token, DateTime.Now.AddMinutes(50)); }
                return token;
            }
        }
        catch { /* 登录失败 */ }

        return null;
    }

    /// <summary>按 JSON 路径（如 data.rows、data.items）从 JsonElement 中提取子元素</summary>
    private static JsonElement? ExtractJsonElementByPath(JsonElement root, string path)
    {
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        JsonElement current = root;
        foreach (var part in parts)
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var child))
                current = child;
            else
                return null;
        }
        return current;
    }

    /// <summary>从 JSON 响应中提取嵌套路径的值，如 data.token</summary>
    private static string? ExtractJsonPath(string json, string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var parts = path.Split('.');
            JsonElement current = doc.RootElement;
            foreach (var part in parts)
            {
                if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var child))
                    current = child;
                else return null;
            }
            return current.GetString() ?? current.GetRawText().Trim('"');
        }
        catch { return null; }
    }

    private async Task<JsonElement> ApplyFieldMappingsAsync(JsonElement data, List<FieldMappingDef> mappings)
    {
        if (data.ValueKind == JsonValueKind.Array)
        {
            var items = new List<Dictionary<string, object?>>();
            foreach (var item in data.EnumerateArray())
            {
                items.Add(await MapItemAsync(item, mappings));
            }
            var json = JsonSerializer.Serialize(items);
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        else
        {
            var mapped = await MapItemAsync(data, mappings);
            var json = JsonSerializer.Serialize(mapped);
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
    }

    private async Task<Dictionary<string, object?>> MapItemAsync(JsonElement item, List<FieldMappingDef> mappings)
    {
        var result = new Dictionary<string, object?>();
        foreach (var m in mappings)
        {
            var value = await GetNestedValueAsync(item, m.SourceField);
            value = ApplyTransform(value, m);
            // 支持嵌套 targetField（如 data.bm → {data:{bm:value}}）
            SetNestedValue(result, m.TargetField, value);
        }
        return result;
    }

    private static void SetNestedValue(Dictionary<string, object?> root, string path, object? value)
    {
        var parts = path.Split('.');
        var current = root;
        for (int i = 0; i < parts.Length; i++)
        {
            if (i == parts.Length - 1)
            {
                current[parts[i]] = value;
            }
            else
            {
                if (!current.TryGetValue(parts[i], out var existing) || existing is not Dictionary<string, object?>)
                {
                    var nested = new Dictionary<string, object?>();
                    current[parts[i]] = nested;
                    current = nested;
                }
                else
                {
                    current = (Dictionary<string, object?>)existing;
                }
            }
        }
    }

    private async Task<object?> GetNestedValueAsync(JsonElement element, string path)
    {
        // 特殊前缀：@lookup:TABLE:RETURN_COL:WHERE_COL — 从本地数据库查询
        if (path.StartsWith("@lookup:"))
        {
            var parts = path.Split(':');
            if (parts.Length >= 4)
            {
                var table = parts[1];
                var returnCol = parts[2];
                var whereCol = parts[3];
                // WHERE 值从当前元素中取（取同一行的某个字段值）
                var whereValue = path.Length > path.IndexOf(whereCol) + whereCol.Length + 1
                    ? path[(path.IndexOf(whereCol) + whereCol.Length + 1)..] : null;

                // 如果 whereValue 是 {CreatedBy} 这样的模板，从 element 取值
                JsonElement? lookupElement = element.ValueKind != JsonValueKind.Undefined ? element : null;
                var sql = $"SELECT \"{returnCol}\" FROM \"{table}\" WHERE \"{whereCol}\" = @val LIMIT 1";
                try
                {
                    var conn = _db.Database.GetDbConnection();
                    if (conn.State != ConnectionState.Open) await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = sql;
                    var p = cmd.CreateParameter(); p.ParameterName = "@val";
                    // 从当前数据元素中获取 where 值
                    if (lookupElement != null && lookupElement.Value.ValueKind == JsonValueKind.Object
                        && lookupElement.Value.TryGetProperty(whereCol, out var whereVal))
                    {
                        p.Value = whereVal.ValueKind == JsonValueKind.Number ? whereVal.GetInt64()
                            : whereVal.GetString() ?? (object)"";
                    }
                    else
                    {
                        p.Value = whereValue ?? "";
                    }
                    cmd.Parameters.Add(p);
                    var result = await cmd.ExecuteScalarAsync();
                    return result?.ToString();
                }
                catch { return null; }
            }
            return null;
        }

        // @value:xxx — 直接使用固定值
        if (path.StartsWith("@value:"))
            return path[7..];

        // 普通字段路径
        var fieldParts = path.Split('.');
        JsonElement current = element;
        foreach (var part in fieldParts)
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var child))
                current = child;
            else
                return null;
        }
        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.TryGetInt64(out var l) ? l : current.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => current.GetRawText(),
        };
    }

    private static object? ApplyTransform(object? value, FieldMappingDef mapping)
    {
        if (value == null && mapping.DefaultValue != null)
            value = mapping.DefaultValue;

        return mapping.Transform switch
        {
            "none" => value,
            "toString" => value?.ToString(),
            "toNumber" => decimal.TryParse(value?.ToString(), out var d) ? d : (mapping.DefaultValue != null ? decimal.Parse(mapping.DefaultValue) : 0m),
            "toDate" => DateTime.TryParse(value?.ToString(), out var dt) ? dt.ToString("yyyy-MM-dd HH:mm:ss") : value?.ToString(),
            "toBoolean" => value?.ToString()?.ToLower() is "true" or "1" or "yes",
            _ => value,
        };
    }

    private static async Task<JsonElement?> ExecuteCodeHandlerAsync(string code, JsonElement? data)
    {
        try
        {
            var dataJson = data != null ? JsonSerializer.Serialize(data) : "[]";
            var globals = new ScriptGlobals { DataJson = dataJson };

            var script = $@"
                using System.Text.Json;
                var data = JsonSerializer.Deserialize<JsonElement>(DataJson);
                {code}
                return JsonSerializer.Serialize(data);
            ";

            var result = await Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript.EvaluateAsync<string>(
                script,
                Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default
                    .WithReferences(typeof(JsonSerializer).Assembly, typeof(ScriptGlobals).Assembly)
                    .WithImports("System", "System.Text.Json", "System.Collections.Generic", "System.Linq"),
                globals: globals);
            return JsonSerializer.Deserialize<JsonElement>(result ?? "[]");
        }
        catch
        {
            // 代码执行失败返回原始数据
            return data;
        }
    }

    // ========== 存入本系统数据库 ==========

    private async Task<string> SaveToDatabaseAsync(IntegrationTask task, JsonElement data, JsonElement? originalData, int taskId)
    {
        var result = new List<string>();
        var items = data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray().ToList()
            : new List<JsonElement> { data };

        // 原始数据列表（用于子表字段提取）
        var originalItems = (originalData != null && originalData.Value.ValueKind == JsonValueKind.Array)
            ? originalData.Value.EnumerateArray().ToList()
            : new List<JsonElement>();

        // 解析子表配置
        var childConfigs = new List<DbChildTableConfig>();
        if (!string.IsNullOrEmpty(task.DbChildConfig))
        {
            childConfigs = JsonSerializer.Deserialize<List<DbChildTableConfig>>(task.DbChildConfig,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }

        for (int itemIdx = 0; itemIdx < items.Count; itemIdx++)
        {
            var item = items[itemIdx];
        {
            // 1. 构建主表 INSERT（通用：自动填充 CreatedAt / UpdatedAt）
            var columns = new List<string>();
            var values = new List<string>();
            var parameters = new List<object>();

            var nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (var prop in item.EnumerateObject())
            {
                // 跳过子表数据字段
                if (childConfigs.Any(c => c.SourceField == prop.Name))
                    continue;

                columns.Add(QuoteIdentifier(prop.Name));
                var val = JsonElementToDbValue(prop.Value);
                values.Add(val.sql);
                parameters.AddRange(val.parameters);
            }

            // 通用：自动填充时间戳字段（如果映射没有提供）
            if (!columns.Contains(QuoteIdentifier("CreatedAt")))
            { var idx = Interlocked.Increment(ref _paramCounter); columns.Add(QuoteIdentifier("CreatedAt")); values.Add($"@p{idx}"); parameters.Add(nowStr); }
            if (!columns.Contains(QuoteIdentifier("UpdatedAt")))
            { var idx = Interlocked.Increment(ref _paramCounter); columns.Add(QuoteIdentifier("UpdatedAt")); values.Add($"@p{idx}"); parameters.Add(nowStr); }

            if (columns.Count == 0) continue;

            // 使用 INSERT OR REPLACE 处理主键/唯一键冲突（通用）
            var parentSql = $"INSERT OR REPLACE INTO {QuoteIdentifier(task.DbTableName!)} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)}); SELECT last_insert_rowid();";
            var parentId = await ExecuteInsertAsync(parentSql, parameters);
            result.Add($"主表 {task.DbTableName} id={parentId}");

            // 2. 处理子表（从原始数据中提取子表字段）
            var origItem = itemIdx < originalItems.Count ? originalItems[itemIdx] : item;
            foreach (var childConfig in childConfigs)
            {
                if (!origItem.TryGetProperty(childConfig.SourceField, out var childData)) continue;
                var children = childData.ValueKind == JsonValueKind.Array
                    ? childData.EnumerateArray().ToList()
                    : new List<JsonElement> { childData };

                var childMappings = JsonSerializer.Deserialize<List<FieldMappingDef>>(childConfig.Mappings,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

                foreach (var child in children)
                {
                    var childColumns = new List<string> { QuoteIdentifier(childConfig.ForeignKey) };
                    var childValues = new List<string> { parentId.ToString() };
                    var childParams = new List<object>();

                    // 如果子表有字段映射，先应用映射
                    var childItem = child;
                    if (childMappings.Count > 0)
                    {
                        var mapped = await MapItemAsync(childItem, childMappings);
                        var json = JsonSerializer.Serialize(mapped);
                        childItem = JsonSerializer.Deserialize<JsonElement>(json);
                    }

                    foreach (var prop in childItem.EnumerateObject())
                    {
                        if (prop.Name == childConfig.ForeignKey) continue;
                        childColumns.Add(QuoteIdentifier(prop.Name));
                        var val = JsonElementToDbValue(prop.Value);
                        childValues.Add(val.sql);
                        childParams.AddRange(val.parameters);
                    }

                    // 通用：子表自动填充时间戳
                    if (!childColumns.Contains(QuoteIdentifier("CreatedAt")))
                    { var idx = Interlocked.Increment(ref _paramCounter); childColumns.Add(QuoteIdentifier("CreatedAt")); childValues.Add($"@p{idx}"); childParams.Add(nowStr); }
                    if (!childColumns.Contains(QuoteIdentifier("UpdatedAt")))
                    { var idx = Interlocked.Increment(ref _paramCounter); childColumns.Add(QuoteIdentifier("UpdatedAt")); childValues.Add($"@p{idx}"); childParams.Add(nowStr); }

                    var childSql = $"INSERT OR REPLACE INTO {QuoteIdentifier(childConfig.TableName)} ({string.Join(", ", childColumns)}) VALUES ({string.Join(", ", childValues)})";
                    await ExecuteInsertAsync(childSql, childParams);
                }
                result.Add($"子表 {childConfig.TableName} {children.Count}条");
            }
        }
        } // end for loop

        var msg = $"成功写入 {string.Join("; ", result)}";
        _db.IntegrationLogs.Add(new IntegrationLog
        {
            TaskId = taskId, Direction = "Push", Status = "Success",
            RequestUrl = $"DB→{task.DbTableName}",
            ResponseData = Truncate(msg), DurationMs = 0, ExecutedAt = DateTime.Now,
        });
        await _db.SaveChangesAsync();
        return msg;
    }

    private static string QuoteIdentifier(string name) => $"\"{name.Replace("\"", "\"\"")}\"";

    private static int _paramCounter = 0;

    private static (string sql, object[] parameters) JsonElementToDbValue(JsonElement element)
    {
        var idx = Interlocked.Increment(ref _paramCounter);
        return element.ValueKind switch
        {
            JsonValueKind.String => ($"@p{idx}", new object[] { element.GetString()! }),
            JsonValueKind.Number => ($"@p{idx}", new object[] { element.TryGetInt64(out var l) ? l : element.GetDecimal() }),
            JsonValueKind.True => ($"@p{idx}", new object[] { 1 }),
            JsonValueKind.False => ($"@p{idx}", new object[] { 0 }),
            JsonValueKind.Null => ("NULL", Array.Empty<object>()),
            _ => ($"@p{idx}", new object[] { element.GetRawText() }),
        };
    }

    private async Task<long> ExecuteInsertAsync(string sql, List<object> parameters)
    {
        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        // 用正则提取所有 @p{N} 参数，按顺序绑定
        var matches = System.Text.RegularExpressions.Regex.Matches(sql, @"@p(\d+)");
        for (int i = 0; i < matches.Count && i < parameters.Count; i++)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = $"@p{matches[i].Groups[1].Value}";
            p.Value = parameters[i] ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        var result = await cmd.ExecuteScalarAsync();
        return result != null ? Convert.ToInt64(result) : 0;
    }

    private void AddLog(int taskId, string direction, string status, string? url, string? reqBody, string? respData, string? headers, string? error, long ms, string type = "")
    {
        _db.IntegrationLogs.Add(new IntegrationLog
        {
            TaskId = taskId, Direction = direction, Status = status,
            RequestUrl = url, RequestBody = Truncate(reqBody), ResponseData = Truncate(respData),
            RequestHeaders = Truncate(headers), ErrorMessage = error,
            LogType = type, DurationMs = ms, ExecutedAt = DateTime.Now,
        });
    }

    /// <summary>为 API 推送附加本地数据库的子表数据</summary>
    private async Task<JsonElement> AttachChildDataForApiAsync(IntegrationTask task, JsonElement pushData, JsonElement? originalData)
    {
        var childConfigs = JsonSerializer.Deserialize<List<DbChildTableConfig>>(task.DbChildConfig!,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        if (childConfigs.Count == 0) return pushData;

        // 从 pushData 或 originalData 获取父记录 ID
        var parentId = GetJsonValue(pushData, "id") ?? GetJsonValue(pushData, "Id");
        if (parentId == null && originalData != null)
        {
            var origItems = originalData.Value.ValueKind == JsonValueKind.Array
                ? originalData.Value.EnumerateArray().ToList() : new List<JsonElement> { originalData.Value };
            if (origItems.Count > 0)
                parentId = GetJsonValue(origItems[0], "id") ?? GetJsonValue(origItems[0], "Id");
        }
        if (parentId == null) return pushData;

        // 构建结果对象（递归转换嵌套对象）
        object? ConvertJsonElement(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDecimal(),
            JsonValueKind.True => true, JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => el.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _ => el.GetRawText()
        };

        var resultDict = new Dictionary<string, object?>();
        foreach (var prop in pushData.EnumerateObject())
            resultDict[prop.Name] = ConvertJsonElement(prop.Value);

        // 查询并附加子表数据
        foreach (var childConfig in childConfigs)
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT * FROM \"{childConfig.TableName}\" WHERE \"{childConfig.ForeignKey}\" = @pid";
            var p = cmd.CreateParameter(); p.ParameterName = "@pid"; p.Value = parentId; cmd.Parameters.Add(p);

            var childItems = new List<Dictionary<string, object?>>();
            using var reader = await cmd.ExecuteReaderAsync();
            var childMappings = string.IsNullOrEmpty(childConfig.Mappings) ? new List<FieldMappingDef>()
                : JsonSerializer.Deserialize<List<FieldMappingDef>>(childConfig.Mappings,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            while (await reader.ReadAsync())
            {
                var item = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var colName = reader.GetName(i);
                    if (reader.IsDBNull(i)) { item[colName] = null; continue; }
                    // SQLite 数字字段是 string，需要转换
                    var rawVal = reader.GetValue(i);
                    if (rawVal is string s && decimal.TryParse(s, out var d))
                        item[colName] = d;
                    else if (rawVal is string s2 && long.TryParse(s2, out var l))
                        item[colName] = l;
                    else
                        item[colName] = rawVal;
                }

                // 应用字段映射
                if (childMappings.Count > 0)
                {
                    var json = JsonSerializer.Serialize(item);
                    var element = JsonSerializer.Deserialize<JsonElement>(json);
                    var mapped = await MapItemAsync(element, childMappings);
                    item = mapped;
                }
                childItems.Add(item);
            }

            // 附加到结果（支持嵌套路径如 data.clmx）
            var fieldName = !string.IsNullOrEmpty(childConfig.SourceField) ? childConfig.SourceField : "Items";
            SetNestedValue(resultDict, fieldName, childItems);
        }

        var resultJson = JsonSerializer.Serialize(resultDict);
        return JsonSerializer.Deserialize<JsonElement>(resultJson);
    }

    private static object? GetJsonValue(JsonElement element, string propName)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propName, out var val))
            return val.ValueKind switch { JsonValueKind.Number => val.TryGetInt64(out var l) ? l : val.GetDecimal(), JsonValueKind.String => val.GetString(), _ => val.GetRawText() };
        return null;
    }

    private static string? Truncate(string? value, int maxLength = 10000)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength] + $"...(截断，原长度{value.Length})";
    }

    private static IntegrationConnectionResponse Map(IntegrationConnection c) => new()
    {
        Id = c.Id, Name = c.Name, Description = c.Description,
        BaseUrl = c.BaseUrl, AuthType = c.AuthType,
        DefaultHeaders = c.DefaultHeaders, CreatedAt = c.CreatedAt,
    };

    public class ScriptGlobals
    {
        public string DataJson { get; set; } = "[]";
    }
}
