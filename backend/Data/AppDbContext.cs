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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

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

        // WorkflowDefinition: 同 Name+Category 查最新版本
        modelBuilder.Entity<WorkflowDefinition>()
            .HasIndex(w => new { w.Name, w.Category, w.Version });

        // WorkflowInstance: 按 Module+RelatedId 查关联
        modelBuilder.Entity<WorkflowInstance>()
            .HasIndex(w => new { w.ModuleName, w.RelatedId });

        modelBuilder.Entity<WorkflowInstance>()
            .HasIndex(w => w.Status);

        // WorkflowTask: 按审批人+状态查待办
        modelBuilder.Entity<WorkflowTask>()
            .HasIndex(t => new { t.AssigneeId, t.Status });

        modelBuilder.Entity<WorkflowTask>()
            .HasIndex(t => t.InstanceId);
    }
}
