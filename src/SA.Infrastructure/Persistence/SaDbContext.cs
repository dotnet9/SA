using Microsoft.EntityFrameworkCore;
using SA.Domain.Entities.Identity;
using SA.Domain.Entities.System;
using SA.Infrastructure.Persistence.Converters;

namespace SA.Infrastructure.Persistence;

/// <summary>
/// 元数据上下文。表名与列名逐列对应 docs/详细设计.md §3 的 DDL
/// （表名显式指定，避免 EF 复数化默认行为与文档不一致）。
/// </summary>
public sealed class SaDbContext(DbContextOptions<SaDbContext> options) : DbContext(options)
{
    /// <summary>用户。</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>角色。</summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>角色功能点授权。</summary>
    public DbSet<RoleFunctionPoint> RoleFunctionPoints => Set<RoleFunctionPoint>();

    /// <summary>角色操作级参数。</summary>
    public DbSet<RoleQuota> RoleQuotas => Set<RoleQuota>();

    /// <summary>功能点字典。</summary>
    public DbSet<FunctionPointRow> FunctionPoints => Set<FunctionPointRow>();

    /// <summary>刷新令牌。</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>登录日志。</summary>
    public DbSet<LoginLog> LoginLogs => Set<LoginLog>();

    /// <summary>密码历史。</summary>
    public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();

    /// <summary>系统设置。</summary>
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    /// <summary>个人设置。</summary>
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();

    /// <summary>配额用量。</summary>
    public DbSet<QuotaUsage> QuotaUsages => Set<QuotaUsage>();

    /// <summary>导出记录。</summary>
    public DbSet<ExportLog> ExportLogs => Set<ExportLog>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var timeConverter = new SaDateTimeOffsetConverter();
        var nullableTimeConverter = new SaNullableDateTimeOffsetConverter();
        var dateConverter = new SaDateOnlyConverter();

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Role");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Name).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(256);
            entity.Property(e => e.CreatedAt).HasConversion(timeConverter);
        });

        modelBuilder.Entity<FunctionPointRow>(entity =>
        {
            entity.ToTable("FunctionPoint");
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasMaxLength(64);
            entity.Property(e => e.GroupName).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(256);
        });

        modelBuilder.Entity<RoleFunctionPoint>(entity =>
        {
            entity.ToTable("RoleFunctionPoint");
            entity.HasKey(e => new { e.RoleId, e.FunctionPointCode });
            entity.Property(e => e.RoleId).HasMaxLength(64);
            entity.Property(e => e.FunctionPointCode).HasMaxLength(64);
            entity.HasOne<Role>().WithMany(r => r.FunctionPoints).HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoleQuota>(entity =>
        {
            entity.ToTable("RoleQuota");
            entity.HasKey(e => new { e.RoleId, e.QuotaKey });
            entity.Property(e => e.RoleId).HasMaxLength(64);
            entity.Property(e => e.QuotaKey).HasMaxLength(64);
            entity.HasOne<Role>().WithMany(r => r.Quotas).HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("User");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Username).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Nickname).HasMaxLength(64).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(e => e.PasswordSalt).HasMaxLength(128).IsRequired();
            entity.Property(e => e.TotpSecret).HasMaxLength(512);
            entity.Property(e => e.Status).HasMaxLength(16).IsRequired();
            entity.Property(e => e.RoleId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.LastLoginIp).HasMaxLength(64);
            entity.Property(e => e.LastLoginAt).HasConversion(nullableTimeConverter);
            entity.Property(e => e.LockedUntil).HasConversion(nullableTimeConverter);
            entity.Property(e => e.PasswordChangedAt).HasConversion(nullableTimeConverter);
            entity.Property(e => e.CreatedAt).HasConversion(timeConverter);
            entity.HasOne(e => e.Role).WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshToken");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.UserId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.TokenHash);
            entity.Property(e => e.Device).HasMaxLength(256);
            entity.Property(e => e.Ip).HasMaxLength(64);
            entity.Property(e => e.ExpiresAt).HasConversion(timeConverter);
            entity.Property(e => e.RevokedAt).HasConversion(nullableTimeConverter);
            entity.Property(e => e.CreatedAt).HasConversion(timeConverter);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LoginLog>(entity =>
        {
            entity.ToTable("LoginLog");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserName).HasMaxLength(64);
            entity.Property(e => e.Ip).HasMaxLength(64);
            entity.Property(e => e.Device).HasMaxLength(256);
            entity.Property(e => e.Result).HasMaxLength(16).IsRequired();
            entity.Property(e => e.Note).HasMaxLength(256);
            entity.Property(e => e.CreatedAt).HasConversion(timeConverter);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<PasswordHistory>(entity =>
        {
            entity.ToTable("PasswordHistory");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(e => e.PasswordSalt).HasMaxLength(128).IsRequired();
            entity.Property(e => e.CreatedAt).HasConversion(timeConverter);
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.ToTable("AppSetting");
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(128);
            entity.Property(e => e.Value).IsRequired();
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);
        });

        modelBuilder.Entity<UserSetting>(entity =>
        {
            entity.ToTable("UserSetting");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasMaxLength(64);
            entity.Property(e => e.Json).IsRequired();
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuotaUsage>(entity =>
        {
            entity.ToTable("QuotaUsage");
            entity.HasKey(e => new { e.UserId, e.Day, e.Kind });
            entity.Property(e => e.UserId).HasMaxLength(64);
            entity.Property(e => e.Day).HasMaxLength(10);
            entity.Property(e => e.Kind).HasMaxLength(32);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExportLog>(entity =>
        {
            entity.ToTable("ExportLog");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Dataset).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Format).HasMaxLength(16).IsRequired();
            entity.Property(e => e.CreatedAt).HasConversion(timeConverter);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
