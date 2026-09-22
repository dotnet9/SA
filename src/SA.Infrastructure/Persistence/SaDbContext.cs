using Microsoft.EntityFrameworkCore;
using SA.Domain.Entities.Collect;
using SA.Domain.Entities.Alerts;
using SA.Domain.Entities.Capital;
using SA.Domain.Entities.Equity;
using SA.Domain.Entities.Events;
using SA.Domain.Entities.Finance;
using SA.Domain.Entities.Rating;
using SA.Domain.Entities.Research;
using SA.Domain.Entities.Screener;
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

    /// <summary>业绩报表。</summary>
    public DbSet<FinancialReport> FinancialReports => Set<FinancialReport>();

    /// <summary>业绩预告。</summary>
    public DbSet<EarningsForecast> EarningsForecasts => Set<EarningsForecast>();

    /// <summary>基本面指标（按报告期的全市场横截面因子 + 单只历史序列）。</summary>
    public DbSet<FundamentalMetric> FundamentalMetrics => Set<FundamentalMetric>();

    /// <summary>主营概况（业务范围 + 经营评述原文）。</summary>
    public DbSet<BusinessProfile> BusinessProfiles => Set<BusinessProfile>();

    /// <summary>主营构成（三套并列口径）。</summary>
    public DbSet<BusinessComposition> BusinessCompositions => Set<BusinessComposition>();

    /// <summary>股本变动历史。</summary>
    public DbSet<ShareChange> ShareChanges => Set<ShareChange>();

    /// <summary>限售解禁（整体替换语义）。</summary>
    public DbSet<UpcomingUnlock> UpcomingUnlocks => Set<UpcomingUnlock>();

    /// <summary>公告列表。</summary>
    public DbSet<Announcement> Announcements => Set<Announcement>();

    /// <summary>研报列表。</summary>
    public DbSet<ResearchReport> ResearchReports => Set<ResearchReport>();

    /// <summary>十大股东（含流通口径）。</summary>
    public DbSet<TopHolder> TopHolders => Set<TopHolder>();

    /// <summary>股东户数。</summary>
    public DbSet<HolderCount> HolderCounts => Set<HolderCount>();

    /// <summary>股权质押。</summary>
    public DbSet<PledgeStat> PledgeStats => Set<PledgeStat>();

    /// <summary>个股逐日资金流。</summary>
    public DbSet<FundFlowDaily> FundFlows => Set<FundFlowDaily>();

    /// <summary>龙虎榜记录。</summary>
    public DbSet<BillboardRecord> Billboards => Set<BillboardRecord>();

    /// <summary>大宗交易。</summary>
    public DbSet<BlockTrade> BlockTrades => Set<BlockTrade>();

    /// <summary>个股两融明细。</summary>
    public DbSet<MarginDetail> MarginDetails => Set<MarginDetail>();

    /// <summary>陆股通持股。</summary>
    public DbSet<NorthboundHolding> NorthboundHoldings => Set<NorthboundHolding>();

    /// <summary>人工事件标注。</summary>
    public DbSet<EventAnnotation> EventAnnotations => Set<EventAnnotation>();

    /// <summary>机构评级共识。</summary>
    public DbSet<RatingConsensus> RatingConsensuses => Set<RatingConsensus>();

    /// <summary>提醒规则。</summary>
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();

    /// <summary>站内通知。</summary>
    public DbSet<Notification> Notifications => Set<Notification>();

    /// <summary>后台操作审计日志。</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>浏览器的 Web Push 订阅。</summary>
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    /// <summary>选股器执行记录（筛选日志 + 我的策略）。</summary>
    public DbSet<ScreenerRun> ScreenerRuns => Set<ScreenerRun>();

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

        modelBuilder.Entity<FinancialReport>(entity =>
        {
            entity.ToTable("FinancialReport");
            entity.HasKey(e => new { e.Code, e.ReportDate });
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.ReportType).HasMaxLength(32);
            entity.Property(e => e.Quarter).HasMaxLength(16);
            entity.Property(e => e.DividendPlan).HasMaxLength(128);
            entity.Property(e => e.Industry).HasMaxLength(64);
            entity.Property(e => e.ReportDate).HasConversion(dateConverter);
            entity.Property(e => e.NoticeDate).HasConversion(nullableDateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);

            foreach (var property in new[]
            {
                nameof(FinancialReport.Revenue), nameof(FinancialReport.NetProfit),
                nameof(FinancialReport.Eps), nameof(FinancialReport.DeductedEps),
                nameof(FinancialReport.Bps), nameof(FinancialReport.OperatingCashFlowPerShare)
            })
            {
                // 营收与净利是「元」级别的大数，用 double 承载足够（有效位数远超金额需要）
                entity.Property(property).HasConversion<double?>();
            }

            foreach (var property in new[]
            {
                nameof(FinancialReport.RevenueYoy), nameof(FinancialReport.NetProfitYoy),
                nameof(FinancialReport.Roe), nameof(FinancialReport.GrossMargin),
                nameof(FinancialReport.RevenueQoq), nameof(FinancialReport.NetProfitQoq),
                nameof(FinancialReport.DividendYield)
            })
            {
                entity.Property(property).HasConversion<double?>();
            }
        });

        modelBuilder.Entity<EarningsForecast>(entity =>
        {
            entity.ToTable("EarningsForecast");
            // 同一报告期只保留最新一条披露（上游同一期会返回多条，详见实体的说明）
            entity.HasKey(e => new { e.Code, e.ReportDate });
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.Caliber).HasMaxLength(64);
            entity.Property(e => e.ForecastType).HasMaxLength(32);
            entity.Property(e => e.Summary).HasMaxLength(1024);
            entity.Property(e => e.ReportDate).HasConversion(dateConverter);
            entity.Property(e => e.NoticeDate).HasConversion(nullableDateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);

            foreach (var property in new[]
            {
                nameof(EarningsForecast.NetProfitMin), nameof(EarningsForecast.NetProfitMax),
                nameof(EarningsForecast.ChangeMin), nameof(EarningsForecast.ChangeMax)
            })
            {
                entity.Property(property).HasConversion<double?>();
            }
        });

        modelBuilder.Entity<FundamentalMetric>(entity =>
        {
            entity.ToTable("FundamentalMetric");
            entity.HasKey(e => new { e.Code, e.ReportDate });
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.ReportType).HasMaxLength(16);
            entity.Property(e => e.OrgType).HasMaxLength(16);
            entity.Property(e => e.ReportDate).HasConversion(dateConverter);
            entity.Property(e => e.NoticeDate).HasConversion(nullableDateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);

            // 选股器与价值研究都要「按代码取最新报告期」，没有这个索引会走全表扫描
            entity.HasIndex(e => new { e.Code, e.ReportDate }).IsUnique();
            entity.HasIndex(e => e.ReportDate);
            // 连续性条件只取年报，单独给一个索引
            entity.HasIndex(e => new { e.ReportType, e.ReportDate });

            // 金额类：元级别的大数，double 承载足够（与 FinancialReport 的处理一致）
            foreach (var property in new[]
            {
                nameof(FundamentalMetric.OperatingCashFlow), nameof(FundamentalMetric.FreeCashFlow),
                nameof(FundamentalMetric.RndExpense), nameof(FundamentalMetric.Revenue),
                nameof(FundamentalMetric.NetProfit), nameof(FundamentalMetric.TotalAssets),
                nameof(FundamentalMetric.TotalEquity), nameof(FundamentalMetric.Liability),
                nameof(FundamentalMetric.TotalShare), nameof(FundamentalMetric.FreeShare)
            })
            {
                entity.Property(property).HasConversion<double?>();
            }

            // 比率 / 百分数类
            foreach (var property in new[]
            {
                nameof(FundamentalMetric.RoeWeighted), nameof(FundamentalMetric.RoeDeducted),
                nameof(FundamentalMetric.GrossMargin), nameof(FundamentalMetric.NetMargin),
                nameof(FundamentalMetric.Roic),
                nameof(FundamentalMetric.OperatingCashFlowToRevenue),
                nameof(FundamentalMetric.OperatingCashFlowToNetProfit),
                nameof(FundamentalMetric.OperatingCashFlowToOperatingProfit),
                nameof(FundamentalMetric.DebtRatio), nameof(FundamentalMetric.CurrentRatio),
                nameof(FundamentalMetric.QuickRatio), nameof(FundamentalMetric.InterestDebtRatio),
                nameof(FundamentalMetric.InterestCoverageRatio), nameof(FundamentalMetric.LiquidationRatio),
                nameof(FundamentalMetric.InventoryTurnoverDays),
                nameof(FundamentalMetric.ReceivableTurnoverDays),
                nameof(FundamentalMetric.AssetTurnoverDays),
                nameof(FundamentalMetric.RevenueYoy), nameof(FundamentalMetric.NetProfitYoy),
                nameof(FundamentalMetric.DeductedNetProfitYoy),
                nameof(FundamentalMetric.Eps), nameof(FundamentalMetric.EpsDeducted),
                nameof(FundamentalMetric.Bps), nameof(FundamentalMetric.OperatingCashFlowPerShare),
                nameof(FundamentalMetric.RndExpenseRatio), nameof(FundamentalMetric.RndPersonnel),
                nameof(FundamentalMetric.StaffNumber)
            })
            {
                entity.Property(property).HasConversion<double?>();
            }
        });

        modelBuilder.Entity<BusinessProfile>(entity =>
        {
            entity.ToTable("BusinessProfile");
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasMaxLength(16);
            // 业务范围与经营评述都是长文本（实测经营评述约 4,500 字），SQLite 的 TEXT 无长度上限，
            // 因此不设 MaxLength——设了反而会在超长时被截断
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);
        });

        modelBuilder.Entity<BusinessComposition>(entity =>
        {
            entity.ToTable("BusinessComposition");
            // 主键含口径与项目名：三套口径并列且项目名在口径内唯一
            entity.HasKey(e => new { e.Code, e.ReportDate, e.MainOpType, e.ItemName });
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.ItemName).HasMaxLength(128);
            entity.Property(e => e.ReportDate).HasConversion(dateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);

            // 展示按「代码 + 报告期 + 口径」取，建索引避免全表扫描
            entity.HasIndex(e => new { e.Code, e.ReportDate, e.MainOpType });

            // 比率字段是小数（0.652002 = 65.20%），金额是元
            foreach (var property in new[]
            {
                nameof(BusinessComposition.Income), nameof(BusinessComposition.Cost),
                nameof(BusinessComposition.Profit),
                nameof(BusinessComposition.IncomeRatio), nameof(BusinessComposition.CostRatio),
                nameof(BusinessComposition.ProfitRatio), nameof(BusinessComposition.GrossProfitRatio)
            })
            {
                entity.Property(property).HasConversion<double?>();
            }
        });

        modelBuilder.Entity<ShareChange>(entity =>
        {
            entity.ToTable("ShareChange");
            entity.HasKey(e => new { e.Code, e.EndDate });
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.ChangeReason).HasMaxLength(64);
            entity.Property(e => e.EndDate).HasConversion(dateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);

            foreach (var property in new[]
            {
                nameof(ShareChange.TotalShares), nameof(ShareChange.LimitedShares),
                nameof(ShareChange.UnlimitedShares), nameof(ShareChange.ListedAShares)
            })
            {
                entity.Property(property).HasConversion<double?>();
            }
        });

        modelBuilder.Entity<UpcomingUnlock>(entity =>
        {
            entity.ToTable("UpcomingUnlock");
            entity.HasKey(e => new { e.Code, e.LiftDate, e.LiftType });
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.LiftType).HasMaxLength(64);
            entity.Property(e => e.LiftDate).HasConversion(dateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);

            foreach (var property in new[]
            {
                nameof(UpcomingUnlock.LiftShares), nameof(UpcomingUnlock.TotalSharesRatio),
                nameof(UpcomingUnlock.UnlimitedASharesRatio)
            })
            {
                entity.Property(property).HasConversion<double?>();
            }
        });

        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.ToTable("Announcement");
            entity.HasKey(e => e.ArtCode);
            entity.Property(e => e.ArtCode).HasMaxLength(64);
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.Title).HasMaxLength(512);
            entity.Property(e => e.ColumnName).HasMaxLength(64);
            entity.Property(e => e.ColumnNames).HasMaxLength(256);
            entity.Property(e => e.AnnType).HasMaxLength(64);
            entity.Property(e => e.SourceType).HasMaxLength(32);
            entity.Property(e => e.Url).HasMaxLength(256);
            entity.Property(e => e.NoticeDate).HasConversion(dateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);

            // 展示按「代码 + 日期倒序」，类型分组按「代码 + 类型」
            entity.HasIndex(e => new { e.Code, e.NoticeDate });
            entity.HasIndex(e => new { e.Code, e.ColumnName });
        });

        modelBuilder.Entity<ResearchReport>(entity =>
        {
            entity.ToTable("ResearchReport");
            entity.HasKey(e => e.InfoCode);
            entity.Property(e => e.InfoCode).HasMaxLength(64);
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.Title).HasMaxLength(512);
            entity.Property(e => e.OrgName).HasMaxLength(128);
            entity.Property(e => e.OrgShortName).HasMaxLength(64);
            entity.Property(e => e.Researcher).HasMaxLength(128);
            entity.Property(e => e.RatingName).HasMaxLength(32);
            entity.Property(e => e.IndustryName).HasMaxLength(64);
            entity.Property(e => e.EncodeUrl).HasMaxLength(256);
            entity.Property(e => e.Url).HasMaxLength(512);
            entity.Property(e => e.PublishDate).HasConversion(dateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);

            entity.HasIndex(e => new { e.Code, e.PublishDate });

            foreach (var property in new[]
            {
                nameof(ResearchReport.PredictThisYearEps), nameof(ResearchReport.PredictThisYearPe),
                nameof(ResearchReport.PredictNextYearEps), nameof(ResearchReport.PredictNextYearPe),
                nameof(ResearchReport.PredictNextTwoYearEps), nameof(ResearchReport.PredictNextTwoYearPe)
            })
            {
                entity.Property(property).HasConversion<double?>();
            }
        });

        modelBuilder.Entity<TopHolder>(entity =>
        {
            entity.ToTable("TopHolder");
            // 同一报告期下，全量口径与流通口径各自有排名
            entity.HasKey(e => new { e.Code, e.EndDate, e.IsFreeFloat, e.Rank });
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.HolderName).HasMaxLength(128).IsRequired();
            entity.Property(e => e.HoldChange).HasMaxLength(32);
            entity.Property(e => e.HolderType).HasMaxLength(32);
            entity.Property(e => e.SharesType).HasMaxLength(32);
            entity.Property(e => e.EndDate).HasConversion(dateConverter);
            entity.Property(e => e.NoticeDate).HasConversion(nullableDateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);

            foreach (var property in new[]
            {
                nameof(TopHolder.HoldNum), nameof(TopHolder.HoldRatio),
                nameof(TopHolder.FreeHoldRatio), nameof(TopHolder.MarketCap)
            })
            {
                entity.Property(property).HasConversion<double?>();
            }
        });

        modelBuilder.Entity<HolderCount>(entity =>
        {
            entity.ToTable("HolderCount");
            entity.HasKey(e => new { e.Code, e.EndDate });
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.ChangeReason).HasMaxLength(128);
            entity.Property(e => e.ReportName).HasMaxLength(32);
            entity.Property(e => e.EndDate).HasConversion(dateConverter);
            entity.Property(e => e.NoticeDate).HasConversion(nullableDateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);

            foreach (var property in new[]
            {
                nameof(HolderCount.HolderNumRatio), nameof(HolderCount.AvgHoldNum),
                nameof(HolderCount.AvgMarketCap), nameof(HolderCount.TotalMarketCap),
                nameof(HolderCount.TotalShares)
            })
            {
                entity.Property(property).HasConversion<double?>();
            }
        });

        modelBuilder.Entity<PledgeStat>(entity =>
        {
            entity.ToTable("PledgeStat");
            entity.HasKey(e => new { e.Code, e.TradeDate });
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.Industry).HasMaxLength(64);
            entity.Property(e => e.TradeDate).HasConversion(dateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);

            foreach (var property in new[]
            {
                nameof(PledgeStat.PledgeRatio), nameof(PledgeStat.PledgeSharesWan),
                nameof(PledgeStat.PledgeMarketCapWan), nameof(PledgeStat.Year1ChangePercent)
            })
            {
                entity.Property(property).HasConversion<double?>();
            }
        });

        modelBuilder.Entity<FundFlowDaily>(entity =>
        {
            entity.ToTable("FundFlowDaily");
            entity.HasKey(e => new { e.Code, e.Date });
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.Date).HasConversion(dateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);

            foreach (var property in new[]
            {
                nameof(FundFlowDaily.MainNet), nameof(FundFlowDaily.SuperLargeNet), nameof(FundFlowDaily.LargeNet),
                nameof(FundFlowDaily.MediumNet), nameof(FundFlowDaily.SmallNet)
            })
            {
                entity.Property(property).HasConversion<double>();
            }

            foreach (var property in new[]
            {
                nameof(FundFlowDaily.MainRatio), nameof(FundFlowDaily.Close), nameof(FundFlowDaily.ChangePercent)
            })
            {
                entity.Property(property).HasConversion<double?>();
            }
        });

        modelBuilder.Entity<BillboardRecord>(entity =>
        {
            entity.ToTable("BillboardRecord");
            // 同一天可能因不同原因多次上榜，因此主键带上原因
            entity.HasKey(e => new { e.Code, e.TradeDate, e.Reason });
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.Reason).HasMaxLength(128);
            entity.Property(e => e.Explain).HasMaxLength(128);
            entity.Property(e => e.TradeDate).HasConversion(dateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);

            foreach (var property in new[]
            {
                nameof(BillboardRecord.Close), nameof(BillboardRecord.ChangePercent),
                nameof(BillboardRecord.TurnoverRate), nameof(BillboardRecord.NetAmount),
                nameof(BillboardRecord.BuyAmount), nameof(BillboardRecord.SellAmount),
                nameof(BillboardRecord.DealAmount), nameof(BillboardRecord.Next1Change),
                nameof(BillboardRecord.Next5Change), nameof(BillboardRecord.Next10Change)
            })
            {
                entity.Property(property).HasConversion<double?>();
            }
        });

        modelBuilder.Entity<BlockTrade>(entity =>
        {
            entity.ToTable("BlockTrade");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.BuyerName).HasMaxLength(128);
            entity.Property(e => e.SellerName).HasMaxLength(128);
            entity.Property(e => e.TradeDate).HasConversion(dateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);
            entity.HasIndex(e => new { e.Code, e.TradeDate });

            foreach (var property in new[]
            {
                nameof(BlockTrade.DealPrice), nameof(BlockTrade.PremiumRatio),
                nameof(BlockTrade.DealVolume), nameof(BlockTrade.DealAmount), nameof(BlockTrade.Close)
            })
            {
                entity.Property(property).HasConversion<double?>();
            }
        });

        modelBuilder.Entity<MarginDetail>(entity =>
        {
            entity.ToTable("MarginDetail");
            entity.HasKey(e => new { e.Code, e.Date });
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.Date).HasConversion(dateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);

            foreach (var property in new[]
            {
                nameof(MarginDetail.FinanceBalance), nameof(MarginDetail.FinanceBuy),
                nameof(MarginDetail.FinanceNetBuy), nameof(MarginDetail.LoanBalance),
                nameof(MarginDetail.LoanVolume), nameof(MarginDetail.TotalBalance),
                nameof(MarginDetail.FinanceBalanceRatio), nameof(MarginDetail.Close),
                nameof(MarginDetail.ChangePercent)
            })
            {
                entity.Property(property).HasConversion<double?>();
            }
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLog");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Username).HasMaxLength(64);
            entity.Property(e => e.Action).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Target).HasMaxLength(128);
            entity.Property(e => e.Detail).HasMaxLength(1024);
            entity.Property(e => e.Ip).HasMaxLength(64);
            entity.Property(e => e.CreatedAt).HasConversion(timeConverter);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<PushSubscription>(entity =>
        {
            entity.ToTable("PushSubscription");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(64).IsRequired();
            // 推送端点可长达数百字符，且必须全局唯一：这是订阅的唯一自然键
            entity.Property(e => e.Endpoint).HasMaxLength(512).IsRequired();
            entity.Property(e => e.P256dh).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Auth).HasMaxLength(64).IsRequired();
            entity.Property(e => e.UserAgent).HasMaxLength(256);
            entity.Property(e => e.LastError).HasMaxLength(512);
            entity.Property(e => e.LastOkAt).HasConversion(nullableTimeConverter);
            entity.Property(e => e.CreatedAt).HasConversion(timeConverter);
            entity.HasIndex(e => e.Endpoint).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.Enabled });
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScreenerRun>(entity =>
        {
            entity.ToTable("ScreenerRun");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(64).IsRequired();
            // 条件以请求体 JSON 原文保存：策略回放要求完全复现，逐字段建模会在新增字段时静默丢条件
            entity.Property(e => e.RequestJson).HasMaxLength(4096).IsRequired();
            entity.Property(e => e.Summary).HasMaxLength(512);
            entity.Property(e => e.Name).HasMaxLength(64);
            entity.Property(e => e.PresetKey).HasMaxLength(32);
            entity.Property(e => e.CreatedAt).HasConversion(timeConverter);
            entity.HasIndex(e => new { e.UserId, e.Id });
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.ToTable("AlertRule");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.UserId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Code).HasMaxLength(16).IsRequired();
            entity.Property(e => e.RuleType).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Note).HasMaxLength(128);
            entity.Property(e => e.Threshold).HasConversion<double?>();
            entity.Property(e => e.CreatedAt).HasConversion(timeConverter);
            entity.Property(e => e.LastTriggeredAt).HasConversion(nullableTimeConverter);
            entity.HasIndex(e => new { e.UserId, e.Code });
            entity.HasIndex(e => e.Enabled);
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notification");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(160).IsRequired();
            entity.Property(e => e.Body).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.Level).HasMaxLength(8).IsRequired();
            entity.Property(e => e.Code).HasMaxLength(16);
            // 规则删除后通知保留（历史可回溯），因此这里不建外键约束
            entity.Property(e => e.RuleId).HasMaxLength(64);
            entity.Property(e => e.CreatedAt).HasConversion(timeConverter);
            entity.HasIndex(e => new { e.UserId, e.IsRead });
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RatingConsensus>(entity =>
        {
            entity.ToTable("RatingConsensus");
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.YearMark1).HasMaxLength(4);
            entity.Property(e => e.YearMark2).HasMaxLength(4);
            entity.Property(e => e.YearMark3).HasMaxLength(4);
            entity.Property(e => e.YearMark4).HasMaxLength(4);
            entity.Property(e => e.IndustryBoard).HasMaxLength(64);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);

            foreach (var property in new[]
            {
                nameof(RatingConsensus.AimPriceMax), nameof(RatingConsensus.AimPriceMin),
                nameof(RatingConsensus.Eps1), nameof(RatingConsensus.Eps2),
                nameof(RatingConsensus.Eps3), nameof(RatingConsensus.Eps4)
            })
            {
                entity.Property(property).HasConversion<double?>();
            }
        });

        modelBuilder.Entity<EventAnnotation>(entity =>
        {
            entity.ToTable("EventAnnotation");
            entity.HasKey(e => new { e.Code, e.EventKey });
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.EventKey).HasMaxLength(160);
            entity.Property(e => e.Tone).HasMaxLength(8).IsRequired();
            entity.Property(e => e.Note).HasMaxLength(256);
            entity.Property(e => e.UserId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);
            entity.HasIndex(e => e.UpdatedAt);
        });

        modelBuilder.Entity<NorthboundHolding>(entity =>
        {
            entity.ToTable("NorthboundHolding");
            entity.HasKey(e => new { e.Code, e.HoldDate });
            entity.Property(e => e.Code).HasMaxLength(16);
            entity.Property(e => e.DateType).HasMaxLength(32);
            entity.Property(e => e.Industry).HasMaxLength(64);
            entity.Property(e => e.HoldDate).HasConversion(dateConverter);
            entity.Property(e => e.UpdatedAt).HasConversion(timeConverter);

            foreach (var property in new[]
            {
                nameof(NorthboundHolding.HoldShares), nameof(NorthboundHolding.PreviousHoldShares),
                nameof(NorthboundHolding.AddShares), nameof(NorthboundHolding.AddSharesAmp),
                nameof(NorthboundHolding.HoldMarketCap), nameof(NorthboundHolding.FreeSharesRatio),
                nameof(NorthboundHolding.TotalSharesRatio)
            })
            {
                entity.Property(property).HasConversion<double?>();
            }
        });
    }
}
