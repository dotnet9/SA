using Microsoft.EntityFrameworkCore;
using SA.Domain.Entities.Collect;
using SA.Domain.Entities.Identity;
using SA.Domain.Entities.Market;
using SA.Domain.Entities.System;
using SA.Domain.Entities.Watchlist;
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

    /// <summary>证券基础信息（股票池）。</summary>
    public DbSet<Instrument> Instruments => Set<Instrument>();

    /// <summary>个股行情快照（整体替换）。</summary>
    public DbSet<QuoteSnapshot> QuoteSnapshots => Set<QuoteSnapshot>();

    /// <summary>指数快照。</summary>
    public DbSet<IndexQuote> IndexQuotes => Set<IndexQuote>();

    /// <summary>行业板块快照。</summary>
    public DbSet<Sector> Sectors => Set<Sector>();

    /// <summary>数据源健康状态。</summary>
    public DbSet<DataSourceStatus> DataSourceStatuses => Set<DataSourceStatus>();

    /// <summary>采集任务日志。</summary>
    public DbSet<CollectTaskLog> CollectTaskLogs => Set<CollectTaskLog>();

    /// <summary>交易日历缓存。</summary>
    public DbSet<TradingDay> TradingDays => Set<TradingDay>();

    /// <summary>市场级日度统计。</summary>
    public DbSet<MarketStat> MarketStats => Set<MarketStat>();

    /// <summary>回补断点。</summary>
    public DbSet<SyncCursor> SyncCursors => Set<SyncCursor>();

    /// <summary>自选分组。</summary>
    public DbSet<WatchGroup> WatchGroups => Set<WatchGroup>();

    /// <summary>自选项。</summary>
    public DbSet<WatchItem> WatchItems => Set<WatchItem>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var timeConverter = new SaDateTimeOffsetConverter();
        var nullableTimeConverter = new SaNullableDateTimeOffsetConverter();
        var dateConverter = new SaDateOnlyConverter();
        var nullableDateConverter = new SaNullableDateOnlyConverter();

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

        modelBuilder.Entity<Instrument>(entity =>
        {
            entity.ToTable("Instrument");
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.Name).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Pinyin).HasMaxLength(64);
            entity.Property(e => e.Board).HasMaxLength(16).IsRequired();
            entity.Property(e => e.Industry).HasMaxLength(64);
            entity.Property(e => e.UpdatedOn).HasConversion(dateConverter);

            // 搜索的三个入口：代码、名称、拼音首字母，都是等值与前缀匹配
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Pinyin);
            entity.HasIndex(e => e.Industry);
        });

        modelBuilder.Entity<QuoteSnapshot>(entity =>
        {
            entity.ToTable("QuoteSnapshot");
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasMaxLength(16);

            // 金额以元计，需要 decimal 精度；SQLite 存 REAL，换算到亿元时再做四舍五入
            foreach (var property in new[]
            {
                nameof(QuoteSnapshot.Price), nameof(QuoteSnapshot.Change), nameof(QuoteSnapshot.Pct),
                nameof(QuoteSnapshot.Volume), nameof(QuoteSnapshot.Amount), nameof(QuoteSnapshot.Turnover),
                nameof(QuoteSnapshot.VolRatio), nameof(QuoteSnapshot.Open), nameof(QuoteSnapshot.High),
                nameof(QuoteSnapshot.Low), nameof(QuoteSnapshot.PrevClose), nameof(QuoteSnapshot.MarketCap),
                nameof(QuoteSnapshot.FloatCap), nameof(QuoteSnapshot.Pe), nameof(QuoteSnapshot.PeTtm),
                nameof(QuoteSnapshot.Pb)
            })
            {
                entity.Property(property).HasConversion<double>();
            }

            entity.Property(e => e.AsOf).HasConversion(dateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);

            // 排行榜与行业聚合按成交额 / 涨跌幅排序，建立覆盖索引避免全表排序
            entity.HasIndex(e => e.Amount);
            entity.HasIndex(e => e.Pct);
        });

        modelBuilder.Entity<IndexQuote>(entity =>
        {
            entity.ToTable("IndexQuote");
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.Name).HasMaxLength(32).IsRequired();

            foreach (var property in new[]
            {
                nameof(IndexQuote.Price), nameof(IndexQuote.Change), nameof(IndexQuote.Pct),
                nameof(IndexQuote.Volume), nameof(IndexQuote.Amount)
            })
            {
                entity.Property(property).HasConversion<double>();
            }

            entity.Property(e => e.AsOf).HasConversion(dateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);
        });

        modelBuilder.Entity<Sector>(entity =>
        {
            entity.ToTable("Sector");
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.Name).HasMaxLength(64).IsRequired();
            entity.Property(e => e.LeaderName).HasMaxLength(64);
            entity.Property(e => e.LeaderCode).HasMaxLength(16);

            foreach (var property in new[] { nameof(Sector.Pct), nameof(Sector.MainNet), nameof(Sector.Pe) })
            {
                entity.Property(property).HasConversion<double>();
            }

            entity.Property(e => e.AsOf).HasConversion(dateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);
            entity.HasIndex(e => e.Pct);
        });

        modelBuilder.Entity<DataSourceStatus>(entity =>
        {
            entity.ToTable("DataSourceStatus");
            entity.HasKey(e => e.Name);
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.Type).HasMaxLength(16);
            entity.Property(e => e.Domains).HasMaxLength(128);
            entity.Property(e => e.Status).HasMaxLength(8).IsRequired();
            entity.Property(e => e.LastError).HasMaxLength(512);
            entity.Property(e => e.LastOkAt).HasConversion(nullableTimeConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);
        });

        modelBuilder.Entity<CollectTaskLog>(entity =>
        {
            entity.ToTable("CollectTaskLog");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TaskName).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Source).HasMaxLength(128);
            entity.Property(e => e.Status).HasMaxLength(8).IsRequired();
            entity.Property(e => e.Error).HasMaxLength(512);
            entity.Property(e => e.RetryResult).HasMaxLength(256);
            entity.Property(e => e.StartedAt).HasConversion(timeConverter);
            entity.HasIndex(e => e.StartedAt);
        });

        modelBuilder.Entity<TradingDay>(entity =>
        {
            entity.ToTable("TradingDay");
            entity.HasKey(e => e.Date);
            entity.Property(e => e.Date).HasConversion(dateConverter);
        });

        modelBuilder.Entity<MarketStat>(entity =>
        {
            entity.ToTable("MarketStat");
            entity.HasKey(e => e.Date);
            entity.Property(e => e.Date).HasConversion(dateConverter);
            entity.Property(e => e.MarginDate).HasConversion(nullableDateConverter);
            entity.Property(e => e.FundFlowDate).HasConversion(nullableDateConverter);

            foreach (var property in new[]
            {
                nameof(MarketStat.MainNet), nameof(MarketStat.SuperLarge), nameof(MarketStat.Large),
                nameof(MarketStat.Medium), nameof(MarketStat.Small),
                nameof(MarketStat.FinanceBalance), nameof(MarketStat.LoanBalance)
            })
            {
                entity.Property(property).HasConversion<double>();
            }

            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);
        });

        modelBuilder.Entity<SyncCursor>(entity =>
        {
            entity.ToTable("SyncCursor");
            entity.HasKey(e => new { e.Dataset, e.Code });
            entity.Property(e => e.Dataset).HasMaxLength(32);
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.Status).HasMaxLength(8).IsRequired();
            entity.Property(e => e.Note).HasMaxLength(256);
            entity.Property(e => e.LastDate).HasConversion(nullableDateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);
        });

        modelBuilder.Entity<WatchGroup>(entity =>
        {
            entity.ToTable("WatchGroup");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.UserId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(32).IsRequired();
            entity.Property(e => e.CreatedAt).HasConversion(timeConverter);
            entity.HasIndex(e => new { e.UserId, e.SortOrder });
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WatchItem>(entity =>
        {
            entity.ToTable("WatchItem");
            // 同一账号下同一只股票只出现一次，这也是「添加自选」幂等的基础
            entity.HasKey(e => new { e.UserId, e.Code });
            entity.Property(e => e.UserId).HasMaxLength(64);
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.GroupId).HasMaxLength(64);
            entity.Property(e => e.Note).HasMaxLength(128);
            entity.Property(e => e.AddedAt).HasConversion(timeConverter);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<WatchGroup>().WithMany().HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
