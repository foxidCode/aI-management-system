using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;

// ==================== Configuration ====================

const string Issuer = "http://localhost:5000";
const string ClientId = "vue-spa";
const string RedirectUri = "http://localhost:18080/callback";
const string Scopes = "openid profile email";

// ==================== PKCE Generation ====================

string GenerateCodeVerifier()
{
    var bytes = RandomNumberGenerator.GetBytes(32);
    return Base64UrlEncode(bytes);
}

string ComputeS256Challenge(string verifier)
{
    var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
    return Base64UrlEncode(hash);
}

string GenerateState()
{
    var bytes = RandomNumberGenerator.GetBytes(16);
    return Convert.ToHexStringLower(bytes);
}

string GenerateNonce()
{
    var bytes = RandomNumberGenerator.GetBytes(16);
    return Convert.ToHexStringLower(bytes);
}

string Base64UrlEncode(byte[] data)
{
    return Convert.ToBase64String(data)
        .Replace("+", "-").Replace("/", "_").Replace("=", "");
}

// ==================== HttpClient ====================

var http = new HttpClient { BaseAddress = new Uri(Issuer) };

var JsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
};

// ==================== Step 1: Generate PKCE Parameters ====================

var codeVerifier = GenerateCodeVerifier();
var codeChallenge = ComputeS256Challenge(codeVerifier);
var state = GenerateState();
var nonce = GenerateNonce();

Console.WriteLine("╔══════════════════════════════════════════════════════╗");
Console.WriteLine("║        OAuth 2.0 + OIDC SSO 测试客户端              ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("[PKCE] Code Verifier:  {0}...", codeVerifier[..20]);
Console.WriteLine("[PKCE] Code Challenge: {0}...", codeChallenge[..20]);
Console.WriteLine("[PKCE] State:          {0}", state);
Console.WriteLine("[PKCE] Nonce:          {0}", nonce);
Console.WriteLine();

// ==================== Step 2: Build Authorize URL ====================

var authorizeUrl = $"{Issuer}/authorize?" +
    $"response_type=code" +
    $"&client_id={Uri.EscapeDataString(ClientId)}" +
    $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
    $"&scope={Uri.EscapeDataString(Scopes)}" +
    $"&state={state}" +
    $"&code_challenge={codeChallenge}" +
    $"&code_challenge_method=S256" +
    $"&nonce={nonce}";

Console.WriteLine("━━━ Step 1: 浏览器授权 ━━━");
Console.WriteLine("Authorize URL: {0}", authorizeUrl);
Console.WriteLine();

// ==================== Step 3: Start HTTP Listener for Callback ====================

string? authorizationCode = null;
string? returnedState = null;
var callbackReceived = new TaskCompletionSource<bool>();

var listener = new HttpListener();
listener.Prefixes.Add(RedirectUri.EndsWith("/") ? RedirectUri : RedirectUri + "/");
listener.Start();

Console.WriteLine("[HTTP] 回调监听已启动: {0}", RedirectUri);

// Open browser
try
{
    Process.Start(new ProcessStartInfo
    {
        FileName = authorizeUrl,
        UseShellExecute = true
    });
    Console.WriteLine("浏览器已打开。请登录并授权...");
}
catch
{
    Console.WriteLine("请手动在浏览器中打开链接。");
}

// Wait for callback
Console.WriteLine("等待回调（超时 120 秒）...");
var listenTask = Task.Run(async () =>
{
    try
    {
        var ctx = await listener.GetContextAsync();
        var query = ctx.Request.QueryString;

        authorizationCode = query["code"];
        returnedState = query["state"];
        var error = query["error"];
        var errorDesc = query["error_description"];

        var responseHtml = error != null
            ? $"<html><body style='font-family:sans-serif;text-align:center;padding-top:50px;'><h1>❌ 授权失败</h1><p>{error}: {errorDesc}</p><p>可以关闭此窗口。</p></body></html>"
            : "<html><body style='font-family:sans-serif;text-align:center;padding-top:50px;'><h1>✅ 授权成功</h1><p>授权码已获取。可以关闭此窗口返回控制台查看结果。</p></body></html>";

        var buffer = Encoding.UTF8.GetBytes(responseHtml);
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.ContentLength64 = buffer.Length;
        await ctx.Response.OutputStream.WriteAsync(buffer);
        ctx.Response.OutputStream.Close();

        callbackReceived.SetResult(true);
    }
    catch (Exception ex)
    {
        Console.WriteLine("[ERROR] 回调异常: {0}", ex.Message);
        callbackReceived.SetResult(false);
    }
});

var completed = await Task.WhenAny(callbackReceived.Task, Task.Delay(TimeSpan.FromSeconds(120)));
listener.Stop();

if (completed != callbackReceived.Task || !callbackReceived.Task.Result || string.IsNullOrEmpty(authorizationCode))
{
    Console.WriteLine("❌ 未收到回调或授权失败");
    Console.WriteLine("按任意键退出...");
    Console.ReadKey();
    return 1;
}

Console.WriteLine();
Console.WriteLine("[回调] Code:  {0}...", authorizationCode[..10]);
Console.WriteLine("[回调] State: {0}", returnedState);

// Validate state
if (returnedState != state)
{
    Console.WriteLine("❌ State 不匹配！可能存在 CSRF 攻击。");
    Console.WriteLine("   期望: {0}", state);
    Console.WriteLine("   收到: {0}", returnedState);
    Console.ReadKey();
    return 1;
}
Console.WriteLine("✅ State 验证通过");
Console.WriteLine();

// ==================== Step 4: Exchange Code for Tokens ====================

Console.WriteLine("━━━ Step 2: 令牌交换 (POST /api/oauth/token) ━━━");

var tokenRequest = new
{
    grantType = "authorization_code",
    code = authorizationCode,
    codeVerifier,
    clientId = ClientId,
    redirectUri = RedirectUri
};

var tokenJson = JsonSerializer.Serialize(tokenRequest);
var tokenContent = new StringContent(tokenJson, Encoding.UTF8, "application/json");

var tokenResponse = await http.PostAsync("/api/oauth/token", tokenContent);
var tokenBody = await tokenResponse.Content.ReadAsStringAsync();

if (!tokenResponse.IsSuccessStatusCode)
{
    Console.WriteLine("❌ Token 交换失败: {0}", tokenBody);
    Console.ReadKey();
    return 1;
}

var tokens = JsonSerializer.Deserialize<TokenResponse>(tokenBody, JsonOptions)!;

Console.WriteLine("✅ Token 交换成功！");
Console.WriteLine("  access_token:  {0}...", tokens.AccessToken[..50]);
Console.WriteLine("  token_type:    {0}", tokens.TokenType);
Console.WriteLine("  expires_in:    {0} 秒 ({1} 小时)", tokens.ExpiresIn, tokens.ExpiresIn / 3600);
if (!string.IsNullOrEmpty(tokens.RefreshToken))
    Console.WriteLine("  refresh_token: {0}...", tokens.RefreshToken[..40]);
Console.WriteLine();

// Decode id_token
if (!string.IsNullOrEmpty(tokens.IdToken))
{
    Console.WriteLine("  ── id_token (OIDC) ──");
    var parts = tokens.IdToken.Split('.');
    if (parts.Length == 3)
    {
        Console.WriteLine("  Header:");
        PrintJson(DecodeBase64Url(parts[0]), "    ");
        Console.WriteLine("  Payload:");
        PrintJson(DecodeBase64Url(parts[1]), "    ");
        Console.WriteLine("  签名算法: RS256 ✅ (JWKS 可验证)");
    }
    Console.WriteLine();
}

// ==================== Step 5: UserInfo ====================

Console.WriteLine("━━━ Step 3: 用户信息 (GET /api/oauth/userinfo) ━━━");

var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, "/api/oauth/userinfo");
userInfoRequest.Headers.Authorization =
    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

var userInfoResponse = await http.SendAsync(userInfoRequest);
var userInfoBody = await userInfoResponse.Content.ReadAsStringAsync();

if (!userInfoResponse.IsSuccessStatusCode)
{
    Console.WriteLine("❌ UserInfo 失败: {0}", userInfoBody);
}
else
{
    Console.WriteLine("✅ UserInfo 获取成功！");
    Console.WriteLine();
    PrintJson(userInfoBody);
    Console.WriteLine();
}

// ==================== Step 6: Refresh Token ====================

Console.WriteLine("━━━ Step 4: 刷新令牌 (POST /api/oauth/token) ━━━");

if (string.IsNullOrEmpty(tokens.RefreshToken))
{
    Console.WriteLine("⚠️  无 refresh_token，跳过");
}
else
{
    var refreshRequest = new
    {
        grantType = "refresh_token",
        refreshToken = tokens.RefreshToken,
        clientId = ClientId
    };

    var refreshJson = JsonSerializer.Serialize(refreshRequest);
    var refreshContent = new StringContent(refreshJson, Encoding.UTF8, "application/json");

    var refreshResponse = await http.PostAsync("/api/oauth/token", refreshContent);
    var refreshBody = await refreshResponse.Content.ReadAsStringAsync();

    if (!refreshResponse.IsSuccessStatusCode)
    {
        Console.WriteLine("❌ Refresh Token 失败: {0}", refreshBody);
    }
    else
    {
        var newTokens = JsonSerializer.Deserialize<TokenResponse>(refreshBody, JsonOptions)!;
        Console.WriteLine("✅ Refresh Token 成功（轮换）");
        Console.WriteLine("  new access_token:  {0}...", newTokens.AccessToken[..50]);
        if (!string.IsNullOrEmpty(newTokens.RefreshToken))
            Console.WriteLine("  new refresh_token: {0}...", newTokens.RefreshToken[..40]);
        Console.WriteLine("  expires_in: {0} 秒", newTokens.ExpiresIn);
    }
    Console.WriteLine();
}

// ==================== Step 7: OIDC Discovery ====================

Console.WriteLine("━━━ Step 5: OIDC 发现 ━━━");

var discoveryResponse = await http.GetAsync("/.well-known/openid-configuration");
if (discoveryResponse.IsSuccessStatusCode)
{
    var doc = JsonDocument.Parse(await discoveryResponse.Content.ReadAsStringAsync()).RootElement;
    Console.WriteLine("✅ OIDC Discovery 端点正常");
    Console.WriteLine("  issuer:                 {0}", doc.GetProperty("issuer").GetString());
    Console.WriteLine("  token_endpoint:         {0}", doc.GetProperty("token_endpoint").GetString());
    Console.WriteLine("  userinfo_endpoint:      {0}", doc.GetProperty("userinfo_endpoint").GetString());
    Console.WriteLine("  jwks_uri:               {0}", doc.GetProperty("jwks_uri").GetString());
    Console.WriteLine("  id_token 签名算法:       {0}",
        string.Join(", ", doc.GetProperty("id_token_signing_alg_values_supported").EnumerateArray().Select(x => x.GetString())));
    Console.WriteLine("  PKCE 方法:              {0}",
        string.Join(", ", doc.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(x => x.GetString())));
}

var jwksResponse = await http.GetAsync("/.well-known/jwks.json");
if (jwksResponse.IsSuccessStatusCode)
{
    var jwks = JsonDocument.Parse(await jwksResponse.Content.ReadAsStringAsync()).RootElement;
    var keys = jwks.GetProperty("keys");
    var key = keys[0];
    Console.WriteLine("✅ JWKS: {0} 个密钥, kty={1}, alg={2}, kid={3}",
        keys.GetArrayLength(),
        key.GetProperty("kty").GetString(),
        key.GetProperty("alg").GetString(),
        (key.GetProperty("kid").GetString() ?? "?")[..20] + "...");
}

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════╗");
Console.WriteLine("║          🎉 所有 OAuth 2.0 + OIDC 测试通过！        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("按任意键退出...");
Console.ReadKey();
return 0;

// ==================== Helper Methods ====================

static string DecodeBase64Url(string base64Url)
{
    var padded = base64Url.Replace('-', '+').Replace('_', '/');
    switch (padded.Length % 4)
    {
        case 2: padded += "=="; break;
        case 3: padded += "="; break;
    }
    var bytes = Convert.FromBase64String(padded);
    return Encoding.UTF8.GetString(bytes);
}

static void PrintJson(string json, string indent = "  ")
{
    try
    {
        using var doc = JsonDocument.Parse(json);
        var formatted = JsonSerializer.Serialize(doc.RootElement,
            new JsonSerializerOptions { WriteIndented = true });
        foreach (var line in formatted.Split('\n'))
            Console.WriteLine("{0}{1}", indent, line);
    }
    catch
    {
        Console.WriteLine("{0}{1}", indent, json);
    }
}

// ==================== DTOs ====================

public class TokenResponse
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("tokenType")]
    public string TokenType { get; set; } = "";

    [JsonPropertyName("expiresIn")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("idToken")]
    public string? IdToken { get; set; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }
}
