using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using backend.Data;
using backend.Services;
using backend.Hubs;
using backend;

var builder = WebApplication.CreateBuilder(args);

// SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis
var redisConfig = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(new ConfigurationOptions
    {
        EndPoints = { redisConfig },
        AbortOnConnectFail = false
    }));

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // SignalR WebSocket 通过 query string 传 token
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = async ctx =>
            {
                var redis = ctx.HttpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>();
                var db = redis.GetDatabase();
                var tokenKey = $"token:{ctx.SecurityToken.UnsafeToString()}";
                if (!await db.KeyExistsAsync(tokenKey))
                {
                    ctx.Fail("Token 已被撤销");
                }
            }
        };
    })
    .AddCookie("IdpSession", options =>
    {
        options.Cookie.Name = ".Idp.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None; // 开发环境
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = 302;
            ctx.Response.Headers.Location = "http://localhost:5173/login";
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = 403;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

// Services
builder.Services.AddSingleton<RsaKeyProvider>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<InboundOrderService>();
builder.Services.AddScoped<AttachmentService>();
builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<IntegrationService>();
builder.Services.AddHostedService<ScheduleService>();
builder.Services.AddScoped<SsoService>();
builder.Services.AddScoped<OAuthService>();
builder.Services.AddScoped<OidcService>();
builder.Services.AddSingleton<MinioService>();
builder.Services.AddScoped<WorkflowDefinitionService>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<NotificationService>();

// SignalR 实时通知
builder.Services.AddSignalR();

builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "管理系统 API",
        Version = "v1",
        Description = "统一后台管理系统接口文档，支持 JWT Bearer 认证。\n\n" +
                      "使用步骤：\n" +
                      "1. 调用 /api/auth/login 获取 Token\n" +
                      "2. 点击右上角 Authorize 按钮\n" +
                      "3. 输入 Bearer {token} 完成认证",
    });

    // JWT Bearer 认证配置
    var securityScheme = new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "输入 JWT Token（只需输入 token 本身，不要加 Bearer 前缀）。\n获取方式：先调用 /api/Auth/login，从返回的 token 字段复制值填入此处。",
    };
    options.AddSecurityDefinition("Bearer", securityScheme);

    // 全局应用 Bearer 认证
    options.AddSecurityRequirement(doc => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", doc, null)] = new List<string>(),
    });

    // 包含 XML 注释
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

builder.Services.AddHttpClient();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:8080")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Auto-migrate database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // 种子数据
    if (!db.Roles.Any())
    {
        SeedData.Initialize(db);
    }

    // 每次启动确保新菜单存在
    SeedData.EnsureMenus(db);

    // 确保新权限存在
    SeedData.EnsurePermissions(db);

    // 确保材料字典种子数据
    SeedData.EnsureMaterials(db);

    // 增量创建新表（EnsureCreated 不会更新已存在的数据库）
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS MaterialDictionaries (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Code TEXT NOT NULL,
            Name TEXT NOT NULL,
            Specification TEXT,
            Model TEXT,
            Unit TEXT,
            Remark TEXT,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_MaterialDictionaries_Code ON MaterialDictionaries(Code);

        CREATE TABLE IF NOT EXISTS InboundOrders (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            OrderCode TEXT NOT NULL,
            WarehouseName TEXT,
            Supplier TEXT,
            Contract TEXT,
            TotalTaxIncludedAmount TEXT NOT NULL DEFAULT '0',
            TotalCostAmount TEXT NOT NULL DEFAULT '0',
            TotalTaxAmount TEXT NOT NULL DEFAULT '0',
            TaxRate TEXT NOT NULL DEFAULT '0',
            Remark TEXT,
            CreatedBy INTEGER NOT NULL DEFAULT 0,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            UpdatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            IsDeleted INTEGER NOT NULL DEFAULT 0
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_InboundOrders_OrderCode ON InboundOrders(OrderCode);

        CREATE TABLE IF NOT EXISTS InboundOrderDetails (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            InboundOrderId INTEGER NOT NULL,
            MaterialCode TEXT NOT NULL,
            MaterialName TEXT NOT NULL,
            Specification TEXT,
            Model TEXT,
            Unit TEXT,
            Quantity TEXT NOT NULL DEFAULT '0',
            UnitPrice TEXT NOT NULL DEFAULT '0',
            TaxIncludedAmount TEXT NOT NULL DEFAULT '0',
            TaxAmount TEXT NOT NULL DEFAULT '0',
            CostAmount TEXT NOT NULL DEFAULT '0',
            TaxRate TEXT NOT NULL DEFAULT '0',
            Remark TEXT,
            FOREIGN KEY (InboundOrderId) REFERENCES InboundOrders(Id) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS IX_InboundOrderDetails_InboundOrderId ON InboundOrderDetails(InboundOrderId);

        CREATE TABLE IF NOT EXISTS InboundOrderAttachments (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            InboundOrderId INTEGER NOT NULL,
            FileName TEXT NOT NULL,
            ObjectKey TEXT NOT NULL,
            FileSize INTEGER NOT NULL DEFAULT 0,
            ContentType TEXT,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            FOREIGN KEY (InboundOrderId) REFERENCES InboundOrders(Id) ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS IX_InboundOrderAttachments_OrderId ON InboundOrderAttachments(InboundOrderId);

        CREATE TABLE IF NOT EXISTS SsoTokens (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Token TEXT NOT NULL,
            UserId INTEGER NOT NULL,
            CreatedBy INTEGER NOT NULL DEFAULT 0,
            ExpiresAt TEXT NOT NULL,
            IsUsed INTEGER NOT NULL DEFAULT 0,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            UsedAt TEXT,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE RESTRICT,
            FOREIGN KEY (CreatedBy) REFERENCES Users(Id) ON DELETE RESTRICT
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_SsoTokens_Token ON SsoTokens(Token);
        CREATE INDEX IF NOT EXISTS IX_SsoTokens_UserId ON SsoTokens(UserId);
    ");

    // 统一附件表
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS Attachments (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ModuleName TEXT NOT NULL,
            RelatedId TEXT NOT NULL,
            RelatedName TEXT,
            FileName TEXT NOT NULL,
            ObjectKey TEXT NOT NULL,
            FileSize INTEGER NOT NULL DEFAULT 0,
            ContentType TEXT,
            UploadedBy INTEGER NOT NULL DEFAULT 0,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            FOREIGN KEY (UploadedBy) REFERENCES Users(Id) ON DELETE RESTRICT
        );
        CREATE INDEX IF NOT EXISTS IX_Attachments_ModuleName_RelatedId ON Attachments(ModuleName, RelatedId);
        CREATE INDEX IF NOT EXISTS IX_Attachments_ModuleName ON Attachments(ModuleName);
    ");

    // 数据迁移：将 InboundOrderAttachments 复制到 Attachments（去重）
    try
    {
        db.Database.ExecuteSqlRaw(@"
            INSERT INTO Attachments (ModuleName, RelatedId, RelatedName, FileName, ObjectKey, FileSize, ContentType, UploadedBy, CreatedAt)
            SELECT 'InboundOrder', CAST(a.InboundOrderId AS TEXT), o.OrderCode, a.FileName, a.ObjectKey, a.FileSize, a.ContentType, o.CreatedBy, a.CreatedAt
            FROM InboundOrderAttachments a
            JOIN InboundOrders o ON a.InboundOrderId = o.Id
            WHERE NOT EXISTS (
                SELECT 1 FROM Attachments att
                WHERE att.ModuleName = 'InboundOrder'
                  AND att.RelatedId = CAST(a.InboundOrderId AS TEXT)
                  AND att.ObjectKey = a.ObjectKey
            )
        ");
    }
    catch { /* 表可能不存在，忽略 */ }

    // 增量迁移：SsoToken 新增 AuthCode + Type 字段
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE SsoTokens ADD COLUMN AuthCode TEXT"); } catch { }
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE SsoTokens ADD COLUMN Type TEXT NOT NULL DEFAULT 'link'"); } catch { }
    try { db.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_SsoTokens_AuthCode ON SsoTokens(AuthCode)"); } catch { }

    // 增量迁移：User 新增 HomeConfig 字段
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE Users ADD COLUMN HomeConfig TEXT"); } catch { }

    // 集成平台表
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS IntegrationConnections (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Description TEXT,
            BaseUrl TEXT NOT NULL,
            AuthType TEXT NOT NULL DEFAULT 'None',
            AuthConfig TEXT,
            DefaultHeaders TEXT,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
        );
        CREATE TABLE IF NOT EXISTS IntegrationTasks (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Description TEXT,
            SourceConnectionId INTEGER,
            SourcePath TEXT,
            SourceMethod TEXT NOT NULL DEFAULT 'GET',
            SourceBody TEXT,
            TargetConnectionId INTEGER,
            TargetPath TEXT,
            TargetMethod TEXT NOT NULL DEFAULT 'POST',
            FieldMappings TEXT,
            CodeHandler TEXT,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
        );
        CREATE TABLE IF NOT EXISTS IntegrationLogs (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            TaskId INTEGER NOT NULL,
            Direction TEXT NOT NULL,
            Status TEXT NOT NULL,
            RequestUrl TEXT,
            RequestBody TEXT,
            ResponseData TEXT,
            ErrorMessage TEXT,
            DurationMs INTEGER NOT NULL DEFAULT 0,
            ExecutedAt TEXT NOT NULL DEFAULT (datetime('now'))
        );
        CREATE INDEX IF NOT EXISTS IX_IntegrationLogs_TaskId ON IntegrationLogs(TaskId);
    ");

    // 增量迁移：IntegrationTask 新增 TargetType, DbTableName, DbChildConfig
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE IntegrationTasks ADD COLUMN TargetType TEXT NOT NULL DEFAULT 'Api'"); } catch { }
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE IntegrationTasks ADD COLUMN DbTableName TEXT"); } catch { }
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE IntegrationTasks ADD COLUMN DbChildConfig TEXT"); } catch { }
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE IntegrationTasks ADD COLUMN BeforeExecute TEXT"); } catch { }
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE IntegrationTasks ADD COLUMN AfterExecute TEXT"); } catch { }
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE IntegrationTasks ADD COLUMN SourceContentType TEXT NOT NULL DEFAULT 'application/json'"); } catch { }
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE IntegrationTasks ADD COLUMN TargetContentType TEXT NOT NULL DEFAULT 'application/json'"); } catch { }
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE IntegrationTasks ADD COLUMN ResponseDataPath TEXT"); } catch { }
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE IntegrationLogs ADD COLUMN RequestHeaders TEXT"); } catch { }
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE IntegrationLogs ADD COLUMN LogType TEXT NOT NULL DEFAULT ''"); } catch { }

    // 计划任务表
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ScheduledTasks (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Description TEXT,
            IntegrationTaskId INTEGER NOT NULL DEFAULT 0,
            CronExpression TEXT NOT NULL DEFAULT '',
            RunOnceAt TEXT,
            IsEnabled INTEGER NOT NULL DEFAULT 1,
            LastRunAt TEXT,
            NextRunAt TEXT,
            LastRunStatus TEXT,
            LastRunDurationMs INTEGER,
            LastRunMessage TEXT,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
        );
        CREATE INDEX IF NOT EXISTS IX_ScheduledTasks_IntegrationTaskId ON ScheduledTasks(IntegrationTaskId);
    ");

    // 增量迁移：ScheduledTask 新增 RunOnceAt
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ScheduledTasks ADD COLUMN RunOnceAt TEXT"); } catch { }
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ScheduledTasks ADD COLUMN CodeHandler TEXT"); } catch { }
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ScheduledTasks ADD COLUMN HandlerClass TEXT"); } catch { }
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ScheduledTasks ADD COLUMN HandlerParameters TEXT"); } catch { }

    // 数据库连接配置表
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS DbConnectionConfigs (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            DbType TEXT NOT NULL,
            Host TEXT,
            Port INTEGER NOT NULL DEFAULT 0,
            DatabaseName TEXT,
            Username TEXT,
            EncryptedPassword TEXT,
            ExtraParams TEXT,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
        );
    ");

    // OAuth 2.0 增量表
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS OAuthClients (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            ClientId TEXT NOT NULL,
            ClientSecret TEXT,
            ClientName TEXT NOT NULL,
            RedirectUris TEXT NOT NULL,
            AllowedScopes TEXT NOT NULL DEFAULT 'openid',
            AllowedGrantTypes TEXT NOT NULL DEFAULT 'authorization_code',
            IsFirstParty INTEGER NOT NULL DEFAULT 0,
            RequirePkce INTEGER NOT NULL DEFAULT 1,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_OAuthClients_ClientId ON OAuthClients(ClientId);

        CREATE TABLE IF NOT EXISTS AuthorizationCodes (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Code TEXT NOT NULL,
            UserId INTEGER NOT NULL,
            ClientId INTEGER NOT NULL,
            RedirectUri TEXT NOT NULL,
            CodeChallenge TEXT,
            CodeChallengeMethod TEXT,
            Scopes TEXT NOT NULL DEFAULT 'openid',
            Nonce TEXT,
            IsUsed INTEGER NOT NULL DEFAULT 0,
            ExpiresAt TEXT NOT NULL,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            UsedAt TEXT,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
            FOREIGN KEY (ClientId) REFERENCES OAuthClients(Id) ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_AuthorizationCodes_Code ON AuthorizationCodes(Code);
        CREATE INDEX IF NOT EXISTS IX_AuthorizationCodes_UserId ON AuthorizationCodes(UserId);

        CREATE TABLE IF NOT EXISTS RefreshTokens (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Token TEXT NOT NULL,
            UserId INTEGER NOT NULL,
            ClientId INTEGER NOT NULL,
            Scopes TEXT NOT NULL DEFAULT 'openid',
            IsRevoked INTEGER NOT NULL DEFAULT 0,
            ExpiresAt TEXT NOT NULL,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            RevokedAt TEXT,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
            FOREIGN KEY (ClientId) REFERENCES OAuthClients(Id) ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_RefreshTokens_Token ON RefreshTokens(Token);
        CREATE INDEX IF NOT EXISTS IX_RefreshTokens_UserId ON RefreshTokens(UserId);
    ");

    // 确保 OAuth 权限和客户端种子数据
    SeedData.EnsureOAuthPermissions(db);
    SeedData.EnsureOAuthMenus(db);
    SeedData.EnsureOAuthClients(db);

    // 增量迁移：User 新增 LeaderId 字段（工作流主管审批链）
    try { db.Database.ExecuteSqlRaw(@"ALTER TABLE Users ADD COLUMN LeaderId INTEGER REFERENCES Users(Id)"); } catch { }

    // 工作流相关表
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS WorkflowDefinitions (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Key TEXT NOT NULL,
            GroupId TEXT,
            FlowCode TEXT,
            FrmType INTEGER NOT NULL DEFAULT 1,
            FrmValue TEXT,
            FrmUrl TEXT,
            DistinctType INTEGER NOT NULL DEFAULT 0,
            IsActive INTEGER NOT NULL DEFAULT 1,
            Version TEXT DEFAULT '1.0',
            Remark TEXT,
            Nodes TEXT,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
        );
        CREATE UNIQUE INDEX IF NOT EXISTS IX_WorkflowDefinitions_Key ON WorkflowDefinitions(Key);
    ");

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS WorkflowInstances (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            DefinitionId INTEGER NOT NULL,
            DefinitionName TEXT,
            Version TEXT,
            ApplicantId INTEGER NOT NULL,
            FormData TEXT,
            Status TEXT NOT NULL DEFAULT 'Running',
            CurrentNodeId TEXT,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            UpdatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            CompletedAt TEXT,
            FOREIGN KEY (DefinitionId) REFERENCES WorkflowDefinitions(Id) ON DELETE RESTRICT,
            FOREIGN KEY (ApplicantId) REFERENCES Users(Id) ON DELETE RESTRICT
        );
        CREATE INDEX IF NOT EXISTS IX_WfInstances_DefinitionId ON WorkflowInstances(DefinitionId);
        CREATE INDEX IF NOT EXISTS IX_WfInstances_ApplicantId ON WorkflowInstances(ApplicantId);
        CREATE INDEX IF NOT EXISTS IX_WfInstances_Status ON WorkflowInstances(Status);
    ");

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS WorkflowTasks (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            InstanceId INTEGER NOT NULL,
            NodeId TEXT NOT NULL,
            NodeName TEXT,
            NodeType INTEGER NOT NULL DEFAULT 4,
            AssigneeId INTEGER,
            AssigneeType INTEGER,
            ActionType TEXT,
            Status TEXT NOT NULL DEFAULT 'Pending',
            FormData TEXT,
            Comment TEXT,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            CompletedAt TEXT,
            ParentTaskId INTEGER,
            FOREIGN KEY (InstanceId) REFERENCES WorkflowInstances(Id) ON DELETE CASCADE,
            FOREIGN KEY (AssigneeId) REFERENCES Users(Id) ON DELETE RESTRICT
        );
        CREATE INDEX IF NOT EXISTS IX_WfTasks_InstanceId ON WorkflowTasks(InstanceId);
        CREATE INDEX IF NOT EXISTS IX_WfTasks_AssigneeId ON WorkflowTasks(AssigneeId);
        CREATE INDEX IF NOT EXISTS IX_WfTasks_Status ON WorkflowTasks(Status);
    ");

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS WorkflowHistories (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            InstanceId INTEGER NOT NULL,
            TaskId INTEGER,
            ActionType TEXT NOT NULL,
            ActorId INTEGER NOT NULL,
            ActorName TEXT,
            Comment TEXT,
            FormDataSnapshot TEXT,
            FromNodeId TEXT,
            ToNodeId TEXT,
            CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
            FOREIGN KEY (InstanceId) REFERENCES WorkflowInstances(Id) ON DELETE CASCADE,
            FOREIGN KEY (TaskId) REFERENCES WorkflowTasks(Id) ON DELETE SET NULL,
            FOREIGN KEY (ActorId) REFERENCES Users(Id) ON DELETE RESTRICT
        );
        CREATE INDEX IF NOT EXISTS IX_WfHistory_InstanceId ON WorkflowHistories(InstanceId);
    ");

    // 工作流种子数据
    SeedData.EnsureWorkflowPermissions(db);
    SeedData.EnsureWorkflowMenus(db);
}

// Swagger UI（所有环境可用，用于接口测试和第三方对接文档）
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "管理系统 API v1");
    options.RoutePrefix = "swagger";
});

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

// 在线用户追踪中间件（必须在 UseAuthentication 之后）
app.Use(async (context, next) =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        var nameId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(nameId, out var userId))
        {
            var authService = context.RequestServices.GetRequiredService<AuthService>();
            await authService.RecordUserActivityAsync(userId);
        }
    }
    await next();
});

// OIDC Discovery 端点
app.MapGet("/.well-known/openid-configuration", (OidcService oidc) =>
{
    return Results.Ok(oidc.GetDiscoveryDocument());
});

app.MapGet("/.well-known/jwks.json", (RsaKeyProvider rsa) =>
{
    return Results.Ok(new
    {
        keys = new[] { rsa.GetJwksKey() }
    });
});

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notification");

app.Run();
