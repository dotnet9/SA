using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SA.Api.Tests.Fakes;
using SA.Application.Abstractions;
using SA.Domain.Entities.Finance;
using SA.Infrastructure.Collect.Jobs;

namespace SA.Api.Tests;

/// <summary>
/// 财务模块端到端：采集 → 落库 → 服务组装 → 接口。
/// </summary>
/// <remarks>
/// 用替身数据源 + 真实 SQLite，因此能覆盖「报表与预告的落库与读回」这段真实逻辑
/// （只测解析函数是测不到持久化的）。
/// </remarks>
public class FinancePipelineTests : IClassFixture<FinanceApiFactory>
{
    private readonly FinanceApiFactory _factory;

    public FinancePipelineTests(FinanceApiFactory factory) => _factory = factory;

    [Fact]
    public async Task 采集后财务接口返回单位换算后的真实数据()
    {
        await _factory.CollectAsync();
        using var client = await _factory.CreateAdminClientAsync();

        var finance = await GetAsync(client, "/api/stocks/300750/finance");

        var latest = finance.GetProperty("latest");
        Assert.Equal("2026年 半年报", latest.GetProperty("reportType").GetString());

        // 元 → 亿元的换算：276,916,580,000 元 = 2769.17 亿元
        Assert.Equal(2769.17m, latest.GetProperty("revenue").GetDecimal());
        Assert.Equal(432.84m, latest.GetProperty("netProfit").GetDecimal());

        // 比率原样透传
        Assert.Equal(54.8m, latest.GetProperty("revenueYoy").GetDecimal());
        Assert.Equal(41.98m, latest.GetProperty("netProfitYoy").GetDecimal());
        Assert.Equal(12.08m, latest.GetProperty("roe").GetDecimal());
        Assert.Equal(23.93m, latest.GetProperty("grossMargin").GetDecimal());

        // 明细按期倒序，趋势按期升序
        Assert.Equal(3, finance.GetProperty("periods").GetArrayLength());
        Assert.Equal(3, finance.GetProperty("trend").GetArrayLength());

        var trendLabels = finance.GetProperty("trend").EnumerateArray().Select(p => p.GetProperty("label").GetString()).ToList();
        Assert.Equal(["2025Q4", "2026Q1", "2026Q2"], trendLabels);

        // 结论由规则给出
        var insights = finance.GetProperty("insights").EnumerateArray().Select(i => i.GetString()!).ToList();
        Assert.Contains(insights, text => text.Contains("营收同比 +54.80%", StringComparison.Ordinal));

        // 口径说明必须包含「累计口径」与「同比取自上游」
        var notes = finance.GetProperty("notes").EnumerateArray().Select(n => n.GetString()!).ToList();
        Assert.Contains(notes, note => note.Contains("累计口径", StringComparison.Ordinal));
        Assert.Contains(notes, note => note.Contains("上游", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 业绩预告落库并可读回()
    {
        await _factory.CollectAsync();
        using var client = await _factory.CreateAdminClientAsync();

        var finance = await GetAsync(client, "/api/stocks/300750/finance");
        var forecasts = finance.GetProperty("forecasts").EnumerateArray().ToList();

        // 这是回归点：早期实现按 REPORT_DATE 排序并依赖可为空的主键列，预告读回为空
        Assert.NotEmpty(forecasts);

        var first = forecasts[0];
        Assert.Equal("略增", first.GetProperty("forecastType").GetString());

        // 只保留一条，且口径为归母净利润
        Assert.Single(forecasts);
        Assert.Equal("归属于母公司股东的净利润", first.GetProperty("caliber").GetString());

        // 数值取自归母口径那条（7.00 ~ 7.50 亿、同比 40% ~ 45%），而不是同日扣非口径的 6.71 ~ 7.13 亿
        Assert.Equal(7.00m, first.GetProperty("netProfitMin").GetDecimal());
        Assert.Equal(7.50m, first.GetProperty("netProfitMax").GetDecimal());
        Assert.Equal(40m, first.GetProperty("changeMin").GetDecimal());
        Assert.Equal(45m, first.GetProperty("changeMax").GetDecimal());
    }

    [Fact]
    public async Task 增收不增利与连续下滑都能被识别()
    {
        await _factory.CollectAsync();
        using var client = await _factory.CreateAdminClientAsync();

        // 600519 的样本刻意构造成「营收增、净利降」且连续两期净利下滑
        var finance = await GetAsync(client, "/api/stocks/600519/finance");
        var insights = finance.GetProperty("insights").EnumerateArray().Select(i => i.GetString()!).ToList();

        Assert.Contains(insights, text => text.Contains("增收不增利", StringComparison.Ordinal));
        Assert.Contains(insights, text => text.Contains("净利润连续两期同比下滑", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 无财报数据时返回1003并给出采集中的说明()
    {
        using var client = await _factory.CreateAdminClientAsync();

        using var response = await client.GetAsync("/api/stocks/000002/finance");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1003, document.RootElement.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task 总览页财务卡已接入真实数据()
    {
        await _factory.CollectAsync();
        using var client = await _factory.CreateAdminClientAsync();

        var overview = await GetAsync(client, "/api/stocks/300750/overview");
        var finance = overview.GetProperty("modules").EnumerateArray()
            .Single(m => m.GetProperty("key").GetString() == "finance");

        Assert.Equal("ready", finance.GetProperty("status").GetString());
        Assert.True(finance.GetProperty("kpis").GetArrayLength() >= 4);
        Assert.True(finance.GetProperty("thumb").GetArrayLength() > 0);
        Assert.Equal("/stock/300750/finance", finance.GetProperty("link").GetString());
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, document.RootElement.GetProperty("code").GetInt32());
        return document.RootElement.GetProperty("data").Clone();
    }
}

/// <summary>
/// 财务模块测试宿主：替身财务源 + 真实 SQLite。
/// </summary>
public sealed class FinanceApiFactory : WebApplicationFactory<Program>
{
    /// <summary>测试管理员密码。</summary>
    public const string AdminPassword = "SaTest!2026Pass";

    /// <summary>无财务功能点的账号。</summary>
    public const string NoFinanceUser = "nofin";

    /// <summary>该账号的密码。</summary>
    public const string NoFinancePassword = "SaNoFin!2026Pass";

    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), "sa-tests-finance", Guid.NewGuid().ToString("N"));

    private bool _collected;
    private bool _seeded;

    /// <summary>跑一次财务采集（幂等）。</summary>
    public async Task CollectAsync()
    {
        if (_collected)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var provider = scope.ServiceProvider;

        await provider.GetRequiredService<IndexJob>().RunAsync();
        await provider.GetRequiredService<UniverseJob>().RunAsync();
        await provider.GetRequiredService<QuoteSnapshotJob>().RunAsync();
        await provider.GetRequiredService<FinanceJob>().RunAsync("300750");
        await provider.GetRequiredService<FinanceJob>().RunAsync("600519");

        _collected = true;
    }

    /// <summary>取一个用于调用接口的客户端（本应用无登录，匿名即可）。</summary>
    public Task<HttpClient> CreateAdminClientAsync() =>
        Task.FromResult(CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }));

    private async Task SeedNoFinanceUserAsync()
    {
        if (_seeded)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var provider = scope.ServiceProvider;
        var roles = provider.GetRequiredService<IRoleStore>();
        var users = provider.GetRequiredService<IUserStore>();
        var hasher = provider.GetRequiredService<SA.Application.Auth.IPasswordHasher>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
        var now = SA.Domain.Common.SaTime.Now;

        // 该角色有趋势权限但没有财务权限，用于验证功能点隔离
        await roles.AddAsync(new SA.Domain.Entities.Identity.Role
        {
            Id = "test-no-finance",
            Name = "测试·无财务",
            Description = "仅有趋势与搜索权限",
            IsBuiltin = false,
            CreatedAt = now
        });
        await unitOfWork.SaveChangesAsync();

        await roles.ReplaceFunctionPointsAsync("test-no-finance",
        [
            SA.Domain.Authorization.FunctionPointCatalog.MarketView,
            SA.Domain.Authorization.FunctionPointCatalog.StockSearch,
            SA.Domain.Authorization.FunctionPointCatalog.StockTrend,
            SA.Domain.Authorization.FunctionPointCatalog.DataScopeAll
        ]);
        await unitOfWork.SaveChangesAsync();

        var hash = hasher.Hash(NoFinancePassword);
        await users.AddAsync(new SA.Domain.Entities.Identity.User
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = NoFinanceUser,
            Nickname = NoFinanceUser,
            PasswordHash = hash.Hash,
            PasswordSalt = hash.Salt,
            PasswordIterations = hash.Iterations,
            PasswordChangedAt = now,
            MustChangePwd = false,
            Status = SA.Domain.Entities.Identity.UserStatus.Active,
            RoleId = "test-no-finance",
            CreatedAt = now
        });
        await unitOfWork.SaveChangesAsync();

        _seeded = true;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Sa:DataDirectory", _dataDirectory);
        builder.UseSetting("Sa:Auth:AdminInitialPassword", AdminPassword);
        builder.UseSetting("Sa:Auth:RequireTotp", "false");
        // 三个宿主调度都关掉：由测试显式驱动任务，绝不打真实上游
        builder.UseSetting("Sa:Collector:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            // 采集层改为按 IEnumerable<T> 注入降级链，后注册不再能覆盖前面的注册：
            // 必须先把真实源移除，再注册测试替身，否则真实源会排在链首并真的去打上游。
            services.RemoveAll<IMarketListSource>();
            services.RemoveAll<IKlineSource>();
            services.RemoveAll<ITradingCalendarSource>();
            services.AddSingleton<IMarketListSource>(new FakeMarketListSource(FakeMarketListSource.DefaultRows));
            services.AddSingleton<IIndexSource, FakeIndexSource>();
            services.AddSingleton<ISectorSource, FakeSectorSource>();
            services.AddSingleton<ILimitPoolSource, FakeLimitPoolSource>();
            services.AddSingleton<IMarginMarketSource, FakeMarginMarketSource>();
            services.AddSingleton<IMarketFundFlowSource, FakeMarketFundFlowSource>();
            services.AddSingleton<ITradingCalendarSource, FakeTradingCalendarSource>();
            services.AddSingleton<IFinanceSource, FakeFinanceSource>();
            services.AddSingleton<ICapitalSource, FakeCapitalSource>();
            services.AddSingleton<IFundFlowSource, FakeFundFlowSource>();
            services.AddSingleton<IEquitySource, FakeEquitySource>();
            services.AddSingleton<IRatingSource, FakeRatingSource>();
        });
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        try
        {
            if (Directory.Exists(_dataDirectory))
            {
                Directory.Delete(_dataDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // SQLite 连接释放有延迟，清理失败不影响测试结论
        }
    }
}

/// <summary>财务源替身：数据取自真实响应样本（宁德时代 2026 半年报）。</summary>
internal sealed class FakeFinanceSource : IFinanceSource
{
    public string Name => "测试源 · 财务报表";

    public string Domains => "财务";

    public Task<IReadOnlyList<FinancialReport>> GetReportsAsync(
        string code,
        int limit = 24,
        CancellationToken cancellationToken = default)
    {
        if (code == "000002")
        {
            // 模拟「无财报记录」：部分标的就是拿不到数据
            return Task.FromResult<IReadOnlyList<FinancialReport>>([]);
        }

        var rows = code == "600519" ? Maotai() : Catl();
        return Task.FromResult<IReadOnlyList<FinancialReport>>(rows.Take(limit).ToList());
    }

    public Task<IReadOnlyList<EarningsForecast>> GetForecastsAsync(
        string code,
        int limit = 8,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<EarningsForecast>>(code == "600519"
            ? []
            :
            [
                new EarningsForecast
                {
                    Code = code,
                    ReportDate = new DateOnly(2018, 6, 30),
                    Caliber = "扣除非经常性损益后的净利润",
                    ForecastType = "略增",
                    Summary = "预计2018年1-6月扣除非经常性损益后的净利润盈利67,110.16万元-71,261.30万元,同比增长31.43%-39.56%。",
                    NetProfitMin = 671_101_600m,
                    NetProfitMax = 712_613_000m,
                    ChangeMin = 31.43m,
                    ChangeMax = 39.56m,
                    NoticeDate = new DateOnly(2018, 7, 13),
                    UpdatedAt = SA.Domain.Common.SaTime.Now
                },
                // 同报告期、同公告日、不同口径的重复行：真实上游就是这样返回的，
                // 早期实现会因此撞主键并让整批预告都写不进去
                new EarningsForecast
                {
                    Code = code,
                    ReportDate = new DateOnly(2018, 6, 30),
                    Caliber = "归属于母公司股东的净利润",
                    ForecastType = "略增",
                    NetProfitMin = 700_000_000m,
                    NetProfitMax = 750_000_000m,
                    ChangeMin = 40m,
                    ChangeMax = 45m,
                    NoticeDate = new DateOnly(2018, 7, 13),
                    UpdatedAt = SA.Domain.Common.SaTime.Now
                }
            ]);

    public Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SourceProbeResult.Success(1, 200, 3));

    /// <summary>宁德时代三期（营收与净利同向增长）。</summary>
    private static List<FinancialReport> Catl() =>
    [
        Report("300750", new DateOnly(2025, 12, 31), "2025Q4", 2025, revenue: 180_000_000_000m, revenueYoy: 20.5m, netProfit: 30_000_000_000m, netProfitYoy: 18.2m, roe: 18.5m, grossMargin: 24.1m),
        Report("300750", new DateOnly(2026, 3, 31), "2026Q1", 2026, revenue: 120_000_000_000m, revenueYoy: 35.1m, netProfit: 19_000_000_000m, netProfitYoy: 28.4m, roe: 6.2m, grossMargin: 24.0m),
        Report("300750", new DateOnly(2026, 6, 30), "2026Q2", 2026, revenue: 276_916_580_000m, revenueYoy: 54.8m, netProfit: 43_284_002_000m, netProfitYoy: 41.98m, roe: 12.08m, grossMargin: 23.93m)
    ];

    /// <summary>茅台三期，刻意构造成「增收不增利 + 净利连续两期下滑」。</summary>
    private static List<FinancialReport> Maotai() =>
    [
        Report("600519", new DateOnly(2025, 12, 31), "2025Q4", 2025, revenue: 170_000_000_000m, revenueYoy: 15.2m, netProfit: 85_000_000_000m, netProfitYoy: -4.5m, roe: 32.1m, grossMargin: 91.2m),
        Report("600519", new DateOnly(2026, 3, 31), "2026Q1", 2026, revenue: 50_000_000_000m, revenueYoy: 12.8m, netProfit: 24_000_000_000m, netProfitYoy: -6.1m, roe: 9.4m, grossMargin: 91.0m),
        Report("600519", new DateOnly(2026, 6, 30), "2026Q2", 2026, revenue: 95_000_000_000m, revenueYoy: 10.4m, netProfit: 45_000_000_000m, netProfitYoy: -3.2m, roe: 17.6m, grossMargin: 90.8m)
    ];

    private static FinancialReport Report(
        string code,
        DateOnly date,
        string quarter,
        int year,
        decimal revenue,
        decimal revenueYoy,
        decimal netProfit,
        decimal netProfitYoy,
        decimal roe,
        decimal grossMargin) =>
        new()
        {
            Code = code,
            ReportDate = date,
            ReportType = $"{year}年 {(date.Month == 6 ? "半年报" : date.Month == 3 ? "一季报" : "年报")}",
            Quarter = quarter,
            Revenue = revenue,
            RevenueYoy = revenueYoy,
            NetProfit = netProfit,
            NetProfitYoy = netProfitYoy,
            Roe = roe,
            GrossMargin = grossMargin,
            Eps = 9.51m,
            DeductedEps = 8.57m,
            Bps = 81.99m,
            OperatingCashFlowPerShare = 13.02m,
            RevenueQoq = 14.45m,
            NetProfitQoq = 8.72m,
            DividendPlan = "10派14.11元(含税)",
            DividendYield = 0.36m,
            NoticeDate = date.AddDays(25),
            Industry = "电池",
            UpdatedAt = SA.Domain.Common.SaTime.Now
        };
}
