using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SA.Application.Abstractions;
using SA.Application.Auth;
using SA.Infrastructure.Collect;
using SA.Infrastructure.Collect.Adapters;
using SA.Infrastructure.Collect.Hosting;
using SA.Infrastructure.Collect.Http;
using SA.Infrastructure.Collect.Jobs;
using SA.Infrastructure.Collect.Registry;
using SA.Infrastructure.History;
using SA.Infrastructure.Persistence;
using SA.Infrastructure.Persistence.Seed;
using SA.Infrastructure.Persistence.Stores;
using SA.Infrastructure.Push;
using SA.Infrastructure.Search;
using SA.Infrastructure.Security;
using SA.Infrastructure.Storage;

namespace SA.Infrastructure;

/// <summary>
/// 基础设施层的服务注册入口。Api 与 Collector 共用同一份注册，保证两个宿主行为一致。
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册数据目录布局。
    /// </summary>
    /// <remarks>
    /// 相对路径一律锚定到仓库根（见 <see cref="DataPaths.ResolveRoot"/>），而不是内容根：
    /// 这样 <c>dotnet run</c>、直接执行 dll、以及内嵌进 SA.Api 的采集器都会落到同一份 data/，
    /// 否则会随启动方式在不同目录各建一份数据库。
    /// </remarks>
    public static IServiceCollection AddSaDataPaths(
        this IServiceCollection services,
        IConfiguration configuration,
        string? contentRoot = null)
    {
        var root = DataPaths.ResolveRoot(configuration["Sa:DataDirectory"], contentRoot);
        services.AddSingleton(new DataPaths(root));
        return services;
    }

    /// <summary>
    /// 注册元数据持久化：DbContext、存储实现、播种器与启动初始化器。
    /// </summary>
    public static IServiceCollection AddSaPersistence(this IServiceCollection services)
    {
        services.AddDbContext<SaDbContext>((provider, builder) =>
        {
            var paths = provider.GetRequiredService<DataPaths>();
            paths.EnsureCreated();
            builder.UseSqlite($"Data Source={paths.DatabaseFile};Foreign Keys=True");
        });

        services.AddScoped<IUserStore, UserStore>();
        services.AddScoped<IRoleStore, RoleStore>();
        services.AddScoped<ISessionStore, SessionStore>();
        services.AddScoped<ISettingsStore, SettingsStore>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<PersistenceInitializer>();

        return services;
    }

    /// <summary>
    /// 注册安全组件：令牌哈希、敏感字段保护、签名密钥与访问令牌签发。
    /// </summary>
    public static IServiceCollection AddSaSecurity(this IServiceCollection services)
    {
        services.AddDataProtection()
            .SetApplicationName("SA")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(DataRootOf(services), "keys")));

        services.AddSingleton<ISigningKeyProvider, SigningKeyProvider>();
        services.AddSingleton<ITokenHasher, Sha256TokenHasher>();
        services.AddSingleton<ISecretProtector>(provider =>
            new DataProtectionSecretProtector(provider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>()));
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        return services;
    }

    /// <summary>
    /// 注册采集层：HTTP 出口、数据源适配器、降级注册表、执行外壳、采集任务与调度。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 任务与调度宿主服务放在基础设施层而不是 <c>SA.Collector</c>，是为了让 SA.Api 能直接内嵌
    /// （实施计划 §5.4 的「单进程是默认形态」）。若让 SA.Api 引用 <c>SA.Collector</c>，
    /// 两个可执行工程会各自生成一个全局 <c>Program</c> 类型，集成测试里的
    /// <c>WebApplicationFactory&lt;Program&gt;</c> 立刻产生 CS0433 二义性。
    /// </para>
    /// <para>
    /// 适配器与注册表都是无状态单例；任务与执行外壳依赖 Scoped 的仓储，因此为 Scoped，
    /// 由调度按轮次创建作用域。SA.Api 与 SA.Collector 共用本方法，两个宿主行为一致。
    /// </para>
    /// </remarks>
    public static IServiceCollection AddSaCollect(this IServiceCollection services)
    {
        services.AddSingleton<HostRateLimiter>();
        services.AddSingleton<HostCircuitBreaker>();
        services.AddSingleton<CollectHttpClient>();

        // 主源为 push2delay：实测 push2 的 clist 连接被重置（2026-09-21 复核），
        // 而 push2delay 的同一端点可用且带 f100 行业字段。
        // 代价是行情有延迟，因此界面的数据时间仍以指数快照时间戳为准（见 CollectOptions.Push2BaseUrl）。
        services.AddSingleton<EastMoneyMarketListSource>();
        services.AddSingleton<SinaMarketListSource>();
        // 注册顺序即降级顺序：push2delay 主源 → 新浪备源
        services.AddSingleton<IMarketListSource>(provider => provider.GetRequiredService<EastMoneyMarketListSource>());
        services.AddSingleton<IMarketListSource>(provider => provider.GetRequiredService<SinaMarketListSource>());

        services.AddSingleton<IIndexSource, EastMoneyIndexSource>();
        services.AddSingleton<ISectorSource, EastMoneySectorSource>();
        services.AddSingleton<ILimitPoolSource, EastMoneyLimitPoolSource>();
        services.AddSingleton<IMarginMarketSource, EastMoneyMarginMarketSource>();
        services.AddSingleton<IMarketFundFlowSource, EastMoneyMarketFundFlowSource>();
        services.AddSingleton<EastMoneyTradingCalendarSource>();
        services.AddSingleton<TencentTradingCalendarSource>();
        // 日历与 K 线同源（指数日线），因此顺序保持一致：腾讯主源 → 东财备源
        services.AddSingleton<ITradingCalendarSource>(provider => provider.GetRequiredService<TencentTradingCalendarSource>());
        services.AddSingleton<ITradingCalendarSource>(provider => provider.GetRequiredService<EastMoneyTradingCalendarSource>());
        // K 线：腾讯主源 → 东财备源。实测东财 push2his 的 kline 端点在本机被连接重置
        // （同一主机的 fflow 端点正常，所以熔断必须按域名而非按数据源），
        // 腾讯优先可避免每次请求都要先失败重试 3 次才降级。
        services.AddSingleton<EastMoneyKlineSource>();
        services.AddSingleton<TencentKlineSource>();
        services.AddSingleton<IKlineSource>(provider => provider.GetRequiredService<TencentKlineSource>());
        services.AddSingleton<IKlineSource>(provider => provider.GetRequiredService<EastMoneyKlineSource>());

        services.AddSingleton<IFinanceSource, EastMoneyFinanceSource>();
        services.AddSingleton<IFundamentalSource, EastMoneyFundamentalSource>();
        services.AddSingleton<IBusinessCompositionSource, EastMoneyBusinessSource>();
        services.AddSingleton<ICapitalStructureSource, EastMoneyCapitalStructureSource>();
        services.AddSingleton<IAnnouncementSource, EastMoneyAnnouncementSource>();
        services.AddSingleton<IResearchReportSource, EastMoneyResearchSource>();
        services.AddSingleton<IEquitySource, EastMoneyEquitySource>();
        services.AddSingleton<ICapitalSource, EastMoneyCapitalSource>();
        services.AddSingleton<IFundFlowSource, EastMoneyFundFlowSource>();
        services.AddSingleton<IRatingSource, EastMoneyRatingSource>();

        // 多标的快照源按注册顺序构成降级链：东财主源 → 腾讯备源
        services.AddSingleton<IQuoteSnapshotSource, EastMoneyQuoteSnapshotSource>();
        services.AddSingleton<IQuoteSnapshotSource, TencentQuoteSource>();

        services.AddSingleton<SourceRegistry>();
        services.AddScoped<CollectExecutor>();
        services.AddScoped<SourceCooldown>();
        services.AddScoped<MarketBusinessDate>();

        services.AddScoped<UniverseJob>();
        services.AddScoped<QuoteSnapshotJob>();
        services.AddScoped<IndexJob>();
        services.AddScoped<SectorJob>();
        services.AddScoped<MarketStatJob>();
        services.AddScoped<TradingCalendarJob>();
        services.AddScoped<DailyKlineJob>();
        services.AddScoped<IndicatorJob>();
        services.AddScoped<BenchmarkDailyJob>();
        services.AddScoped<FinanceJob>();
        services.AddScoped<FundamentalJob>();
        services.AddScoped<ResearchJob>();
        services.AddScoped<EquityJob>();
        services.AddScoped<CapitalJob>();
        services.AddScoped<SectorKlineJob>();
        services.AddScoped<RatingJob>();

        services.AddSingleton<IOnDemandQueue, OnDemandQueue>();

        services.AddHostedService<SchedulerHostedService>();
        services.AddHostedService<OnDemandHostedService>();
        services.AddHostedService<BackfillHostedService>();

        return services;
    }

    /// <summary>
    /// 注册采集仓储：股票池、快照、板块、指数、市场统计、数据源状态与交易日历。
    /// </summary>
    public static IServiceCollection AddSaMarketStores(this IServiceCollection services)
    {
        services.AddScoped<IInstrumentStore, InstrumentStore>();
        services.AddScoped<IQuoteSnapshotStore, QuoteSnapshotStore>();
        services.AddScoped<ISectorStore, SectorStore>();
        services.AddScoped<IIndexStore, IndexStore>();
        services.AddScoped<IMarketStatStore, MarketStatStore>();
        services.AddScoped<ICollectStatusStore, CollectStatusStore>();
        services.AddScoped<ITradingCalendarStore, TradingCalendarStore>();
        services.AddSingleton<IPinyinIndexer, ToolGoodPinyinIndexer>();
        services.AddScoped<IWatchlistStore, WatchlistStore>();
        services.AddScoped<IFinanceStore, FinanceStore>();
        services.AddScoped<IFundamentalStore, FundamentalStore>();
        services.AddScoped<IResearchStore, ResearchStore>();
        services.AddScoped<IEquityStore, EquityStore>();
        services.AddScoped<ICapitalStore, CapitalStore>();
        services.AddScoped<IEventAnnotationStore, EventAnnotationStore>();
        services.AddScoped<IRatingStore, RatingStore>();
        services.AddScoped<IAlertStore, AlertStore>();
        services.AddScoped<IUserAdminStore, UserAdminStore>();
        services.AddScoped<IRoleAdminStore, RoleAdminStore>();
        services.AddScoped<ISessionAdminStore, SessionAdminStore>();
        services.AddScoped<IAuditStore, AuditStore>();
        services.AddScoped<IPushSubscriptionStore, PushSubscriptionStore>();
        services.AddScoped<IScreenerRunStore, ScreenerRunStore>();
        services.AddScoped<IExportLogStore, ExportLogStore>();

        // Web Push 发送器是单例：它内部持有 VAPID 密钥与 WebPushClient，
        // 每次请求都重新生成密钥会让已有订阅静默失效
        services.AddSingleton<IPushSender, VapidKeyProvider>();

        services.AddSingleton<SA.Application.Abstractions.IDataPaths>(provider => provider.GetRequiredService<SA.Infrastructure.Storage.DataPaths>());
        services.AddScoped<IDataScopeService, DataScopeService>();

        return services;
    }

    /// <summary>
    /// 注册时序历史：Parquet 分片路径、日线与指标存储、回补游标。
    /// </summary>
    /// <remarks>
    /// 三个存储都是单例：它们不持有 DbContext，只按标的读写 Parquet 文件
    /// （DuckDB 连接在每次操作内创建并释放），因此没有作用域生命周期问题；
    /// 回补游标走 SQLite，依赖 Scoped 的 DbContext，故为 Scoped。
    /// </remarks>
    public static IServiceCollection AddSaHistory(this IServiceCollection services)
    {
        services.AddSingleton<ParquetPaths>();
        services.AddSingleton<IDailyHistoryStore, DuckDbHistoryStore>();
        services.AddSingleton<IIndicatorStore, DuckDbIndicatorStore>();
        services.AddScoped<ISyncCursorStore, SyncCursorStore>();

        return services;
    }

    /// <summary>
    /// 从已注册的 <see cref="DataPaths"/> 取数据根目录。DataProtection 的密钥环需要真实路径，
    /// 而此处无法注入实例（注册阶段），只能读取服务描述。
    /// </summary>
    private static string DataRootOf(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(DataPaths));
        if (descriptor?.ImplementationInstance is DataPaths paths)
        {
            return paths.Root;
        }

        throw new InvalidOperationException("请先调用 AddSaDataPaths 再调用 AddSaSecurity");
    }
}
