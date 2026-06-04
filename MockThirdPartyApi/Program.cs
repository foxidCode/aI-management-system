// ============================================
//   Mock Third-Party API — 模拟三方系统
// ============================================
//  启动: dotnet run --urls http://localhost:5100
// ============================================

using System.Collections.Concurrent;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// 存储有效的 appToken / accessToken
var appTokens = new ConcurrentDictionary<string, DateTime>();
var accessTokens = new ConcurrentDictionary<string, DateTime>();

// ============================================================
//  原有接口
// ============================================================

var validTokens = new Dictionary<string, DateTime>();
var tokenLock = new object();

// ========== 1. 登录获取 Token ==========
app.MapPost("/api/auth/login", (LoginRequest req) =>
{
    if (req.Username == "admin" && req.Password == "password")
    {
        var token = Guid.NewGuid().ToString("N");
        lock (tokenLock) { validTokens[token] = DateTime.Now.AddHours(2); }
        return Results.Ok(new { success = true, data = new { token, expiresIn = 7200 } });
    }
    return Results.Ok(new { success = false, message = "用户名或密码错误" });
});

// ========== 2. 接收入库单（需 Token 认证，兼容数组和单对象） ==========
app.MapPost("/api/inbound-orders", async (HttpContext http) =>
{
    using var reader = new StreamReader(http.Request.Body);
    var rawBody = await reader.ReadToEndAsync();
    if (rawBody.TrimStart().StartsWith("["))
    {
        var arr = JsonSerializer.Deserialize<List<InboundOrderRequest>>(rawBody);
        if (arr != null && arr.Count > 0)
            rawBody = JsonSerializer.Serialize(arr[0]);
        else
            return Results.Json(new { success = false, message = "请求数据为空" }, statusCode: 400);
    }
    var req = JsonSerializer.Deserialize<InboundOrderRequest>(rawBody);
    if (req == null)
        return Results.Json(new { success = false, message = "请求数据格式错误" }, statusCode: 400);

    var authHeader = http.Request.Headers["Authorization"].FirstOrDefault();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        return Results.Json(new { success = false, message = "缺少认证Token" }, statusCode: 401);

    var token = authHeader["Bearer ".Length..];
    lock (tokenLock)
    {
        if (!validTokens.TryGetValue(token, out var expiry) || expiry < DateTime.Now)
            return Results.Json(new { success = false, message = "Token无效或已过期" }, statusCode: 401);
    }

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 收到入库单: 单号={req.OrderCode}, 编制人={req.PreparedBy}, 供应商={req.SupplierName}, 库房={req.StorageName}");
    if (req.Items?.Count > 0)
        Console.WriteLine($"  明细: {req.Items.Count}条, 首条={req.Items[0].MaterialDesc}");

    return Results.Ok(new
    {
        success = true,
        message = $"入库单 {req.OrderCode} 已接收",
        data = new { receivedId = Guid.NewGuid().ToString("N"), receivedAt = DateTime.Now }
    });
});

// ============================================================
//  新增接口（来自 jkdoc.docx 文档）
// ============================================================

// ========== 1. 获取 AppToken ==========
// POST /ierp/api/getAppToken.do
app.MapPost("/ierp/api/getAppToken.do", (GetAppTokenRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.AppId) || string.IsNullOrWhiteSpace(req.AppSecret))
    {
        return Results.Ok(new { errorCode = "400", message = "appId和appSecret不能为空", status = false });
    }

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 获取AppToken: appId={req.AppId}, tenantid={req.TenantId}");

    // 生成模拟 appToken
    var appToken = $"app_{Guid.NewGuid():N}_{DateTimeOffset.Now.ToUnixTimeSeconds()}";
    appTokens[appToken] = DateTime.Now.AddHours(2);

    return Results.Ok(new
    {
        errorCode = "0",
        message = "success",
        status = true,
        data = new
        {
            appToken,
            expiresIn = 7200
        }
    });
});

// ========== 2. 获取 AccessToken ==========
// POST /ierp/api/login.do
app.MapPost("/ierp/api/login.do", (GetAccessTokenRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.User) || string.IsNullOrWhiteSpace(req.AppToken))
    {
        return Results.Ok(new { errorCode = "400", message = "用户和appToken不能为空", status = false });
    }

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 获取AccessToken: user={req.User}, usertype={req.UserType}, tenantid={req.TenantId}");

    // 生成模拟 accessToken
    var accessToken = $"at_{Guid.NewGuid():N}_{DateTimeOffset.Now.ToUnixTimeSeconds()}";
    accessTokens[accessToken] = DateTime.Now.AddHours(2);

    return Results.Ok(new
    {
        errorCode = "0",
        message = "success",
        status = true,
        data = new
        {
            accessToken,
            expiresIn = 7200,
            tokenType = "Bearer"
        }
    });
});

// ========== 3. 物料主数据信息查询 ==========
// POST /ierp/kapi/v2/ctgp/basedata/queryMaterials
app.MapPost("/ierp/kapi/v2/ctgp/basedata/queryMaterials", async (HttpContext http) =>
{
    // 验证 access_token header
    var accessTokenHeader = http.Request.Headers["access_token"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(accessTokenHeader))
    {
        return Results.Ok(new { errorCode = "401", message = "缺少access_token", status = false });
    }

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 物料主数据查询: access_token={accessTokenHeader[..Math.Min(20, accessTokenHeader.Length)]}...");

    // 读取请求体
    using var reader = new StreamReader(http.Request.Body);
    var rawBody = await reader.ReadToEndAsync();
    MaterialQueryRequest? queryReq;
    try
    {
        queryReq = JsonSerializer.Deserialize<MaterialQueryRequest>(rawBody);
    }
    catch
    {
        return Results.Ok(new { errorCode = "400", message = "请求数据格式错误", status = false });
    }

    if (queryReq == null)
        return Results.Ok(new { errorCode = "400", message = "请求数据为空", status = false });

    var pageSize = queryReq.PageSize > 0 ? queryReq.PageSize : 10;
    var pageNo = queryReq.PageNo > 0 ? queryReq.PageNo : 1;

    // 生成模拟物料数据
    var allMaterials = GenerateMockMaterials();
    var totalCount = allMaterials.Count;
    var lastPage = pageNo * pageSize >= totalCount;
    var pagedRows = allMaterials.Skip((pageNo - 1) * pageSize).Take(pageSize).ToList();

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 返回物料数据: pageNo={pageNo}, pageSize={pageSize}, totalCount={totalCount}");

    return Results.Ok(new
    {
        data = new
        {
            filter = $"start_date >= '{queryReq.Data?.start_date}' AND end_date <= '{queryReq.Data?.end_date}'",
            lastPage,
            pageNo,
            pageSize,
            rows = pagedRows,
            totalCount
        },
        errorCode = "0",
        message = (string?)null,
        status = true
    });
});

// ========== 4. 采购订单同步接口 ==========
// POST /ierp/kapi/v2/ctgp/pssc/pm_requirapplybill/sf_ST_im_reqapplication
app.MapPost("/ierp/kapi/v2/ctgp/pssc/pm_requirapplybill/sf_ST_im_reqapplication", async (HttpContext http) =>
{
    // 验证 access_token header
    var accessTokenHeader = http.Request.Headers["access_token"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(accessTokenHeader))
    {
        return Results.Ok(new { code = 401, message = "缺少access_token" });
    }

    using var reader = new StreamReader(http.Request.Body);
    var rawBody = await reader.ReadToEndAsync();
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 采购订单同步: body length={rawBody.Length}");

    try
    {
        var order = JsonSerializer.Deserialize<InboundOrderApiRequest>(rawBody);
        if (order != null)
        {
            Console.WriteLine($"  单号={order.BillNo}, 单据类型={order.BillType}, 业务类型={order.BizType}");
            Console.WriteLine($"  创建人={order.Creator}, 组织={order.Org}, 明细条数={order.BillEntry?.Count ?? 0}");
        }
    }
    catch { /* ignore parse errors in mock */ }

    return Results.Ok(new
    {
        code = 200,
        message = "SUCCESS",
        result = new[]
        {
            new { receivedId = Guid.NewGuid().ToString("N"), receivedAt = DateTime.Now }
        },
        timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
    });
});

app.Run();

// ============================================================
//  生成模拟物料主数据
// ============================================================
List<object> GenerateMockMaterials()
{
    var now = DateTime.Now;
    var materials = new List<object>();
    var random = new Random(42); // 固定种子，每次返回一致数据

    var materialDefs = new[]
    {
        new { Code = "MAT-001", Name = "高强度螺栓 M20×80", Spec = "M20×80 8.8级", Model = "GB/T 5782", GroupNum = "100101", GroupName = "紧固件类", Unit = "EA", UnitName = "个" },
        new { Code = "MAT-002", Name = "冷轧钢板 Q235B", Spec = "2.0mm×1250mm×2500mm", Model = "Q235B", GroupNum = "100201", GroupName = "板材类", Unit = "TON", UnitName = "吨" },
        new { Code = "MAT-003", Name = "无缝钢管 Φ108×4.5", Spec = "Φ108×4.5mm", Model = "20#钢 GB/T 8163", GroupNum = "100202", GroupName = "管材类", Unit = "M", UnitName = "米" },
        new { Code = "MAT-004", Name = "焊接焊条 E5015", Spec = "Φ3.2×350mm", Model = "E5015", GroupNum = "100301", GroupName = "焊接材料类", Unit = "KG", UnitName = "千克" },
        new { Code = "MAT-005", Name = "工业齿轮油 L-CKC 220", Spec = "200L/桶", Model = "L-CKC 220", GroupNum = "100401", GroupName = "润滑油类", Unit = "BARREL", UnitName = "桶" },
        new { Code = "MAT-006", Name = "轴承 6308-2RS", Spec = "40×90×23mm", Model = "6308-2RS", GroupNum = "100501", GroupName = "轴承类", Unit = "EA", UnitName = "个" },
        new { Code = "MAT-007", Name = "密封垫片 DN50", Spec = "DN50 PTFE", Model = "HG/T 20606", GroupNum = "100601", GroupName = "密封件类", Unit = "EA", UnitName = "个" },
        new { Code = "MAT-008", Name = "防锈漆 铁红", Spec = "20kg/桶", Model = "C53-31", GroupNum = "100701", GroupName = "涂料类", Unit = "KG", UnitName = "千克" },
        new { Code = "MAT-009", Name = "不锈钢法兰 DN80", Spec = "DN80 PN16", Model = "304 SS", GroupNum = "100801", GroupName = "管件类", Unit = "EA", UnitName = "个" },
        new { Code = "MAT-010", Name = "电缆 YJV-0.6/1kV 3×4", Spec = "3×4mm²", Model = "YJV-0.6/1kV", GroupNum = "100901", GroupName = "电缆类", Unit = "M", UnitName = "米" },
        new { Code = "MAT-011", Name = "液压油 L-HM 46", Spec = "200L/桶", Model = "L-HM 46", GroupNum = "100401", GroupName = "润滑油类", Unit = "BARREL", UnitName = "桶" },
        new { Code = "MAT-012", Name = "圆钢 Φ20 Q235", Spec = "Φ20×6000mm", Model = "Q235B", GroupNum = "101001", GroupName = "型材类", Unit = "TON", UnitName = "吨" },
        new { Code = "MAT-013", Name = "蝶阀 D71X-16 DN100", Spec = "DN100 PN16", Model = "D71X-16", GroupNum = "101101", GroupName = "阀门类", Unit = "EA", UnitName = "个" },
        new { Code = "MAT-014", Name = "联轴器 ML4", Spec = "ML4 45×84/38×58", Model = "ML4", GroupNum = "101201", GroupName = "联轴器类", Unit = "EA", UnitName = "个" },
        new { Code = "MAT-015", Name = "角钢 L50×50×5", Spec = "50×50×5mm", Model = "Q235B GB/T 706", GroupNum = "101001", GroupName = "型材类", Unit = "TON", UnitName = "吨" },
    };

    for (int i = 0; i < materialDefs.Length; i++)
    {
        var def = materialDefs[i];
        var id = 6267173004370770000L + i * 1000;
        var creatorNum = (598566 + random.Next(100)).ToString();
        var approverNum = (79552 + random.Next(100)).ToString();
        var createTime = new DateTime(2022, 4, 18).AddDays(random.Next(0, 800)).AddHours(random.Next(0, 24));
        var approveDate = createTime.AddDays(random.Next(7, 180)).ToString("yyyy-MM-dd");
        var disableDate = createTime.AddYears(3).AddDays(random.Next(0, 30)).ToString("yyyy-MM-dd HH:mm:ss");
        var modifyTime = createTime.AddYears(1).AddDays(random.Next(0, 365)).ToString("yyyy-MM-dd HH:mm:ss");

        materials.Add(new
        {
            id = id.ToString(),
            baseunit_number = def.Unit,
            baseunit_name = def.UnitName,
            helpcode = GenerateHelpCode(def.Name),
            oldnumber = def.Code,
            description = $"{def.Spec} {def.Model}",
            group_number = def.GroupNum,
            group_name = def.GroupName,
            createorg_number = "00",
            createorg_name = "组织编码",
            number = def.Code,
            name = def.Name,
            modelnum = def.Model,
            status = "A",
            creator_number = creatorNum,
            creator_name = $"创建人{random.Next(1, 50)}",
            approverid_number = approverNum,
            approverid_name = $"审批人{random.Next(1, 30)}",
            approvedate = approveDate,
            createtime = createTime.ToString("yyyy-MM-dd HH:mm:ss"),
            disabler_number = (59856 + random.Next(100)).ToString(),
            disabler_name = $"禁用人{random.Next(1, 20)}",
            org_number = "00",
            org_name = "组织编码",
            disabledate = disableDate,
            materialtype = "1",
            isuseauxpty = false,
            isdisposable = (i % 5 == 0),
            taxrate_number = "13V",
            taxrate_name = "13",
            modifier_number = (55655 + random.Next(100)).ToString(),
            modifier_name = $"修改人{random.Next(1, 30)}",
            modifytime = modifyTime,
            auxptyunit_number = "ZA",
            auxptyunit_name = "组",
            unitconvertdir = "1",
            entryentity = new[]
            {
                new
                {
                    id = (2871057333393733632 + i * 1000).ToString(),
                    denominator = 32,
                    measureunitid_number = "14513",
                    measureunitid_name = "单位数量",
                    numerator = 77,
                    desmuid_number = "ZA",
                    desmuid_name = "组",
                    converttype_number = "1",
                    converttype_name = "浮动换算",
                    precision = 2
                }
            },
            auxptyentry = new[]
            {
                new
                {
                    id = (16815161315135150000UL + (ulong)i * 1000).ToString(),
                    auxpty_number = "ZA",
                    auxpty_name = "组"
                }
            },
            entry_groupstandard = new[]
            {
                new
                {
                    id = (890349491587243008 + i * 1000).ToString(),
                    standardid_number = "1",
                    standardid_name = "标准编码",
                    groupid_number = "1",
                    groupid_name = "分组编码"
                }
            },
            Inventoryunit_number = "ZA",
            Inventoryunit_name = "组",
            enable = true
        });
    }

    return materials;
}

string GenerateHelpCode(string name)
{
    if (string.IsNullOrWhiteSpace(name)) return "XXXX";
    var parts = name.Split(' ');
    var first = parts[0];
    if (first.Length >= 4) return first[..4].ToUpper();
    return first.ToUpper().PadRight(4, 'X');
}

// ============================================================
//  DTOs
// ============================================================

// -- 原有 --
record LoginRequest(string Username, string Password);
record InboundOrderRequest
{
    public string OrderCode { get; set; } = "";
    public string StorageName { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public string ContractNo { get; set; } = "";
    public decimal? TotalAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public string Remark { get; set; } = "";
    public string PreparedBy { get; set; } = "";
    public string PreparedAt { get; set; } = "";
    public List<OrderItemRequest>? Items { get; set; }
}
record OrderItemRequest
{
    public string MaterialCode { get; set; } = "";
    public string MaterialDesc { get; set; } = "";
    public string Spec { get; set; } = "";
    public string ModelNo { get; set; } = "";
    public string UnitName { get; set; } = "";
    public decimal? Qty { get; set; }
    public decimal? Price { get; set; }
}

// -- 新增: AppToken --
record GetAppTokenRequest
{
    public string AppId { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string AccountId { get; set; } = "";
}

// -- 新增: AccessToken --
record GetAccessTokenRequest
{
    public string User { get; set; } = "";
    public string UserType { get; set; } = "Mobile";
    public string AppToken { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string Language { get; set; } = "zh_CN";
}

// -- 新增: 物料查询 --
record MaterialQueryRequest
{
    public MaterialQueryData? Data { get; set; }
    public int PageSize { get; set; } = 10;
    public int PageNo { get; set; } = 1;
}
record MaterialQueryData
{
    public string? start_date { get; set; }
    public string? end_date { get; set; }
}

// -- 新增: 采购订单同步 --
record InboundOrderApiRequest
{
    public string BillNo { get; set; } = "";
    public string BizTime { get; set; } = "";
    public string BillStatus { get; set; } = "";
    public string Creator { get; set; } = "";
    public string CreateTime { get; set; } = "";
    public string Org { get; set; } = "";
    public string BillType { get; set; } = "";
    public string BizType { get; set; } = "";
    public string Dept { get; set; } = "";
    public string Comment { get; set; } = "";
    public string CtgpModId { get; set; } = "";
    public List<BillEntryRequest>? BillEntry { get; set; }
}
record BillEntryRequest
{
    public string Qty { get; set; } = "";
    public string MaterialMasterId { get; set; } = "";
    public string MaterialName { get; set; } = "";
    public string Unit { get; set; } = "";
    public string BaseUnit { get; set; } = "";
    public string BaseQty { get; set; } = "";
    public string EntryBizOrg { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string Project { get; set; } = "";
    public string EntryComment { get; set; } = "";
    public string OriginOrg { get; set; } = "";
    public string CtgpSs { get; set; } = "";
    public string CtgpSsLn { get; set; } = "";
    public string CtgpSSLID { get; set; } = "";
    public string CtgpBuc { get; set; } = "";
}
