using Microsoft.EntityFrameworkCore;
using backend.Models;

namespace backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<MaterialDictionary> MaterialDictionaries => Set<MaterialDictionary>();
    public DbSet<InboundOrder> InboundOrders => Set<InboundOrder>();
    public DbSet<InboundOrderDetail> InboundOrderDetails => Set<InboundOrderDetail>();
    public DbSet<InboundOrderAttachment> InboundOrderAttachments => Set<InboundOrderAttachment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<DbConnectionConfig> DbConnectionConfigs => Set<DbConnectionConfig>();
    public DbSet<IntegrationConnection> IntegrationConnections => Set<IntegrationConnection>();
    public DbSet<IntegrationTask> IntegrationTasks => Set<IntegrationTask>();
    public DbSet<IntegrationLog> IntegrationLogs => Set<IntegrationLog>();
    public DbSet<ScheduledTask> ScheduledTasks => Set<ScheduledTask>();
    public DbSet<SsoToken> SsoTokens => Set<SsoToken>();
    public DbSet<OAuthClient> OAuthClients => Set<OAuthClient>();
    public DbSet<AuthorizationCode> AuthorizationCodes => Set<AuthorizationCode>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowTask> WorkflowTasks => Set<WorkflowTask>();
    public DbSet<WorkflowHistory> WorkflowHistories => Set<WorkflowHistory>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<OperationLog> OperationLogs => Set<OperationLog>();
    public DbSet<PermissionChangeLog> PermissionChangeLogs => Set<PermissionChangeLog>();
    public DbSet<AiModelConfig> AiModelConfigs => Set<AiModelConfig>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<DailySummary> DailySummaries => Set<DailySummary>();
    public DbSet<KnowledgeEntry> KnowledgeEntries => Set<KnowledgeEntry>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // User 自引用：LeaderId FK
        modelBuilder.Entity<User>()
            .HasOne(u => u.Leader)
            .WithMany()
            .HasForeignKey(u => u.LeaderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Role>()
            .HasIndex(r => r.Name)
            .IsUnique();

        modelBuilder.Entity<Permission>()
            .HasIndex(p => p.Code)
            .IsUnique();

        // RolePermission 复合主键
        modelBuilder.Entity<RolePermission>()
            .HasKey(rp => new { rp.RoleId, rp.PermissionId });

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId);

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId);

        // UserRole 复合主键
        modelBuilder.Entity<UserRole>()
            .HasKey(ur => new { ur.UserId, ur.RoleId });

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId);

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId);

        // 材料字典：编码唯一
        modelBuilder.Entity<MaterialDictionary>()
            .HasIndex(m => m.Code)
            .IsUnique();

        // 入库单：单据编码唯一
        modelBuilder.Entity<InboundOrder>()
            .HasIndex(o => o.OrderCode)
            .IsUnique();

        // 入库单明细：按入库单 ID 索引（大数据量查询关键）
        modelBuilder.Entity<InboundOrderDetail>()
            .HasIndex(d => d.InboundOrderId);

        // SSO Token：唯一索引 + 导航关系
        modelBuilder.Entity<SsoToken>()
            .HasIndex(t => t.Token)
            .IsUnique();

        modelBuilder.Entity<SsoToken>()
            .HasIndex(t => t.AuthCode)
            .IsUnique();

        modelBuilder.Entity<SsoToken>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SsoToken>()
            .HasOne(t => t.CreatedByAdmin)
            .WithMany()
            .HasForeignKey(t => t.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // OAuthClient：ClientId 唯一
        modelBuilder.Entity<OAuthClient>()
            .HasIndex(c => c.ClientId)
            .IsUnique();

        // AuthorizationCode：Code 唯一 + 导航关系
        modelBuilder.Entity<AuthorizationCode>()
            .HasIndex(a => a.Code)
            .IsUnique();

        modelBuilder.Entity<AuthorizationCode>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AuthorizationCode>()
            .HasOne(a => a.Client)
            .WithMany(c => c.AuthorizationCodes)
            .HasForeignKey(a => a.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        // 统一附件：按模块+关联ID查询的索引
        modelBuilder.Entity<Attachment>()
            .HasIndex(a => new { a.ModuleName, a.RelatedId });

        modelBuilder.Entity<Attachment>()
            .HasIndex(a => a.ModuleName);

        modelBuilder.Entity<Attachment>()
            .HasOne(a => a.Uploader)
            .WithMany()
            .HasForeignKey(a => a.UploadedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // RefreshToken：Token 唯一 + 导航关系
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(r => r.Token)
            .IsUnique();

        modelBuilder.Entity<RefreshToken>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefreshToken>()
            .HasOne(r => r.Client)
            .WithMany(c => c.RefreshTokens)
            .HasForeignKey(r => r.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        // WorkflowDefinition：Key 唯一
        modelBuilder.Entity<WorkflowDefinition>()
            .HasIndex(d => d.Key)
            .IsUnique();

        // WorkflowInstance：按 DefinitionId / ApplicantId / Status 索引
        modelBuilder.Entity<WorkflowInstance>()
            .HasIndex(i => i.DefinitionId);

        modelBuilder.Entity<WorkflowInstance>()
            .HasIndex(i => i.ApplicantId);

        modelBuilder.Entity<WorkflowInstance>()
            .HasIndex(i => i.Status);

        modelBuilder.Entity<WorkflowInstance>()
            .HasOne(i => i.Definition)
            .WithMany()
            .HasForeignKey(i => i.DefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WorkflowInstance>()
            .HasOne(i => i.Applicant)
            .WithMany()
            .HasForeignKey(i => i.ApplicantId)
            .OnDelete(DeleteBehavior.Restrict);

        // WorkflowTask：按 InstanceId / AssigneeId / Status 索引
        modelBuilder.Entity<WorkflowTask>()
            .HasIndex(t => t.InstanceId);

        modelBuilder.Entity<WorkflowTask>()
            .HasIndex(t => t.AssigneeId);

        modelBuilder.Entity<WorkflowTask>()
            .HasIndex(t => t.Status);

        modelBuilder.Entity<WorkflowTask>()
            .HasOne(t => t.Instance)
            .WithMany(i => i.Tasks)
            .HasForeignKey(t => t.InstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WorkflowTask>()
            .HasOne(t => t.Assignee)
            .WithMany()
            .HasForeignKey(t => t.AssigneeId)
            .OnDelete(DeleteBehavior.Restrict);

        // WorkflowHistory：按 InstanceId 索引
        modelBuilder.Entity<WorkflowHistory>()
            .HasIndex(h => h.InstanceId);

        modelBuilder.Entity<WorkflowHistory>()
            .HasOne(h => h.Instance)
            .WithMany(i => i.Histories)
            .HasForeignKey(h => h.InstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WorkflowHistory>()
            .HasOne(h => h.Task)
            .WithMany()
            .HasForeignKey(h => h.TaskId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<WorkflowHistory>()
            .HasOne(h => h.Actor)
            .WithMany()
            .HasForeignKey(h => h.ActorId)
            .OnDelete(DeleteBehavior.Restrict);

        // UserPermission 复合主键
        modelBuilder.Entity<UserPermission>()
            .HasKey(up => new { up.UserId, up.PermissionId });

        modelBuilder.Entity<UserPermission>()
            .HasOne(up => up.User)
            .WithMany()
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserPermission>()
            .HasOne(up => up.Permission)
            .WithMany()
            .HasForeignKey(up => up.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        // OperationLog 索引
        modelBuilder.Entity<OperationLog>()
            .HasIndex(l => l.UserId);

        modelBuilder.Entity<OperationLog>()
            .HasIndex(l => l.Action);

        modelBuilder.Entity<OperationLog>()
            .HasIndex(l => l.CreatedAt);

        modelBuilder.Entity<OperationLog>()
            .HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // PermissionChangeLog 索引
        modelBuilder.Entity<PermissionChangeLog>()
            .HasIndex(l => l.TargetUserId);

        modelBuilder.Entity<PermissionChangeLog>()
            .HasIndex(l => l.CreatedAt);

        modelBuilder.Entity<PermissionChangeLog>()
            .HasOne(l => l.TargetUser)
            .WithMany()
            .HasForeignKey(l => l.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PermissionChangeLog>()
            .HasOne(l => l.Operator)
            .WithMany()
            .HasForeignKey(l => l.OperatorId)
            .OnDelete(DeleteBehavior.Restrict);

        // AiModelConfig

        // ChatSession
        modelBuilder.Entity<ChatSession>()
            .HasIndex(s => s.UserId);

        // ChatMessage
        modelBuilder.Entity<ChatMessage>()
            .HasIndex(m => m.SessionId);

        // DailySummary
        modelBuilder.Entity<DailySummary>()
            .HasIndex(s => s.SummaryDate)
            .IsUnique();

        // KnowledgeEntry
        modelBuilder.Entity<KnowledgeEntry>()
            .HasIndex(k => k.Category);


    }
}
