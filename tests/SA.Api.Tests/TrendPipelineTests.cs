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
using SA.Infrastructure.Collect.Hosting;
using SA.Infrastructure.Collect.Jobs;

namespace SA.Api.Tests;

/// <summary>
/// 趋势模块端到端：日线入库 → 指标计算 → 趋势接口。
/// </summary>
/// <remarks>
/// 覆盖实施计划里第 3 批的三条验收：指标与独立计算一致、样本不足返回空、
/// 以及「清空某只数据后访问 → 1003 → 自动就绪」的按需采集闭环。
/// </remarks>
public class TrendPipelineTests : IClassFixture<TrendApiFactory>
{
    private readonly TrendApiFactory _factory;

    public TrendPipelineTests(TrendApiFactory factory) => _factory = factory;

    [Fact]
    public async Task 趋势接口返回K线与服务端算好的指标()
    {
        await _factory.CollectAsync(TrendApiFactory.ReadyCode);
        using var client = await _factory.CreateAdminClientAsync();

        var trend = await GetAsync(client, $"/api/stocks/{TrendApiFactory.ReadyCode}/trend?limit=120");

        Assert.Equal(TrendApiFactory.ReadyCode, trend.GetProperty("code").GetString());
        Assert.Equal("front", trend.GetProperty("adjust").GetString());

        var candles = trend.GetProperty("candles");
        Assert.Equal(120, candles.GetArrayLength());

        // 每根 K 线的字段必须自洽，且与指标序列等长
        foreach (var candle in candles.EnumerateArray())
        {
            var high = candle.GetProperty("h").GetDecimal();
            var low = candle.GetProperty("l").GetDecimal();
            var close = candle.GetProperty("c").GetDecimal();
            Assert.True(high >= low);
            Assert.True(close >= low && close <= high, "收盘价必须落在当日高低区间内");
        }

        var ma5 = trend.GetProperty("ma").GetProperty("ma5");
        Assert.Equal(120, ma5.GetArrayLength());

        // 窗口内的首根 K 线仍有有效 MA5：日线库里比展示窗口更长，指标是带着预热算出来的
        Assert.NotEqual(JsonValueKind.Null, ma5[0].ValueKind);
        Assert.NotEqual(JsonValueKind.Null, ma5[119].ValueKind);

        var macd = trend.GetProperty("macd");
        Assert.Equal(120, macd.GetProperty("dif").GetArrayLength());
        Assert.Equal(120, macd.GetProperty("macd").GetArrayLength());

        // 上涨序列：DIF 与 MACD 柱都应为正
        Assert.True(macd.GetProperty("dif")[119].GetDecimal() > 0);

        var kdj = trend.GetProperty("kdj");
        Assert.Equal(120, kdj.GetProperty("j").GetArrayLength());

        var boll = trend.GetProperty("boll");
        var upper = boll.GetProperty("up")[119].GetDecimal();
        var middle = boll.GetProperty("mid")[119].GetDecimal();
        var lower = boll.GetProperty("low")[119].GetDecimal();
        Assert.True(upper > middle && middle > lower);
    }

    [Fact]
    public async Task 均线与指标由服务端计算且与序列一致()
    {
        await _factory.CollectAsync(TrendApiFactory.ReadyCode);
        using var client = await _factory.CreateAdminClientAsync();

        var trend = await GetAsync(client, $"/api/stocks/{TrendApiFactory.ReadyCode}/trend?limit=240");
        var candles = trend.GetProperty("candles").EnumerateArray().Select(c => c.GetProperty("c").GetDecimal()).ToList();

        var ma5 = trend.GetProperty("ma").GetProperty("ma5");
        var ma20 = trend.GetProperty("ma").GetProperty("ma20");

        // 独立复算 MA5 / MA20 的最后一点（抽验两个点：中段与末点）
        foreach (var index in new[] { 120, candles.Count - 1 })
        {
            Assert.Equal(Math.Round(candles.Skip(index - 4).Take(5).Average(), 4), ma5[index].GetDecimal());
            Assert.Equal(Math.Round(candles.Skip(index - 19).Take(20).Average(), 4), ma20[index].GetDecimal());
        }

        // MACD 柱 = (DIF − DEA) × 2。两侧各自四舍五入到 6 位，因此比对要留 1e-6 容差
        var macd = trend.GetProperty("macd");
        var dif = macd.GetProperty("dif")[candles.Count - 1].GetDecimal();
        var dea = macd.GetProperty("dea")[candles.Count - 1].GetDecimal();
        var bar = macd.GetProperty("macd")[candles.Count - 1].GetDecimal();
        Assert.True(Math.Abs(bar - (dif - dea) * 2) <= 0.000002m, $"MACD 柱与 DIF/DEA 不自洽：{bar} vs {(dif - dea) * 2}");
    }

    [Fact]
    public async Task 相对强弱基于基准指数且口径可追溯()
    {
        await _factory.CollectAsync(TrendApiFactory.ReadyCode);
        using var client = await _factory.CreateAdminClientAsync();

        var trend = await GetAsync(client, $"/api/stocks/{TrendApiFactory.ReadyCode}/trend?limit=120");
        var rs = trend.GetProperty("relativeStrength");

        Assert.Equal("000300", rs.GetProperty("benchmarkCode").GetString());
        Assert.NotEqual(JsonValueKind.Null, rs.GetProperty("value").ValueKind);

        // 曲线长度与 K 线对齐
        Assert.Equal(120, rs.GetProperty("line").GetArrayLength());
    }

    [Fact]
    public async Task 样本不足时位置类指标返回空而不是近似值()
    {
        // 只回补 40 个交易日：MA250 与 250 日分位都应当为空
        await _factory.CollectAsync(TrendApiFactory.ShortCode);
        using var client = await _factory.CreateAdminClientAsync();

        var trend = await GetAsync(client, $"/api/stocks/{TrendApiFactory.ShortCode}/trend?limit=40");
        var levels = trend.GetProperty("levels");

        Assert.Equal(JsonValueKind.Null, levels.GetProperty("aboveMa250Pct").ValueKind);
        Assert.Equal(JsonValueKind.Null, levels.GetProperty("quantile250").ValueKind);
        Assert.Equal(JsonValueKind.Null, levels.GetProperty("quantile3y").ValueKind);

        // 只有 40 个样本时，窗口首根的 MA20 也不该有值（不足 20 个前置样本）
        var ma20 = trend.GetProperty("ma").GetProperty("ma20");
        Assert.Equal(JsonValueKind.Null, ma20[0].ValueKind);
        Assert.Equal(JsonValueKind.Null, ma20[18].ValueKind);
        Assert.NotEqual(JsonValueKind.Null, ma20[19].ValueKind);

        // 样本数要如实返回，界面才能解释「为什么是 —」
        Assert.Equal(40, levels.GetProperty("samples").GetInt32());

        // 口径说明里必须写明样本不足的处理方式
        var notes = trend.GetProperty("notes").EnumerateArray().Select(n => n.GetString()!).ToList();
        Assert.Contains(notes, note => note.Contains("样本不足", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 无日线时返回1003并触发按需采集()
    {
        // PendingCode 初始没有数据
        using var client = await _factory.CreateAdminClientAsync();

        using var response = await client.GetAsync($"/api/stocks/{TrendApiFactory.PendingCode}/trend");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1003, document.RootElement.GetProperty("code").GetInt32());

        // 接口调用应当把该标的放进按需队列
        var queue = _factory.Services.GetRequiredService<IOnDemandQueue>();
        Assert.True(queue.PendingCount > 0 || _factory.WasEnqueued(TrendApiFactory.PendingCode));

        // 执行一轮按需采集（模拟调度消费队列）
        await _factory.RunOnDemandAsync();

        // 数据到位后同一次请求应当成功
        var trend = await GetAsync(client, $"/api/stocks/{TrendApiFactory.PendingCode}/trend?limit=60");
        Assert.Equal(TrendApiFactory.PendingCode, trend.GetProperty("code").GetString());
        Assert.Equal(60, trend.GetProperty("candles").GetArrayLength());
    }

    [Fact]
    public async Task 新鲜度接口说明日线与指标的入库进度()
    {
        await _factory.CollectAsync(TrendApiFactory.ReadyCode);
        using var client = await _factory.CreateAdminClientAsync();

        var freshness = await GetAsync(client, $"/api/stocks/{TrendApiFactory.ReadyCode}/freshness");

        Assert.False(freshness.GetProperty("collecting").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(freshness.GetProperty("dailyLastDate").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(freshness.GetProperty("indicatorLastDate").GetString()));
    }

    [Fact]
    public async Task 总览页已接入模块返回真实数据未接入模块标注采集中()
    {
        await _factory.CollectAsync(TrendApiFactory.ReadyCode);
        using var client = await _factory.CreateAdminClientAsync();

        var overview = await GetAsync(client, $"/api/stocks/{TrendApiFactory.ReadyCode}/overview");

        var modules = overview.GetProperty("modules").EnumerateArray().ToList();
        Assert.Equal(8, modules.Count);

        var trend = modules.Single(m => m.GetProperty("key").GetString() == "trend");
        Assert.Equal("ready", trend.GetProperty("status").GetString());
        Assert.True(trend.GetProperty("tags").GetArrayLength() > 0);
        Assert.True(trend.GetProperty("kpis").GetArrayLength() > 0);

        // 「行业与同业对比」不需要新的采集源（全部由全市场快照横截面算出），
        // 因此在任何已采集到快照的环境里都应当就绪
        var industry = modules.Single(m => m.GetProperty("key").GetString() == "industry");
        Assert.Equal("ready", industry.GetProperty("status").GetString());

        // 「事件与影响」同样由本地已落地的数据派生，因此也应就绪
        var events = modules.Single(m => m.GetProperty("key").GetString() == "events");
        Assert.Equal("ready", events.GetProperty("status").GetString());

        // 八个模块已全部接入：其中「风险与舆情」由本地数据计算（无需采集源），
        // 「机构评级」由替身源提供确定性数据，因此这两张卡也应当是就绪状态
        foreach (var key in new[] { "risk", "rating" })
        {
            var module = modules.Single(m => m.GetProperty("key").GetString() == key);
            Assert.Equal("ready", module.GetProperty("status").GetString());
        }

        Assert.All(modules, module => Assert.NotEqual("collecting", module.GetProperty("status").GetString()));

        // 行情条来自全市场快照
        var profile = overview.GetProperty("profile");
        Assert.Equal(TrendApiFactory.ReadyCode, profile.GetProperty("code").GetString());
        Assert.NotEqual(JsonValueKind.Null, profile.GetProperty("price").ValueKind);
    }

    /* ------------------------------------------------------------------ */

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
/// 趋势模块的测试宿主：stub 采集源 + 关闭调度（任务由测试显式驱动）。
/// </summary>
public sealed class TrendApiFactory : WebApplicationFactory<Program>
{
    /// <summary>有充足日线的标的（400 个交易日，足够覆盖 MA250 与 250 日分位）。</summary>
    public const string ReadyCode = "300750";

    /// <summary>只有 40 个交易日的标的，用于验证样本不足时的空态。</summary>
    public const string ShortCode = "600519";

    /// <summary>初始无数据、由按需采集补齐的标的。</summary>
    public const string PendingCode = "000002";

    /// <summary>测试管理员密码。</summary>
    public const string AdminPassword = "SaTest!2026Pass";

    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), "sa-tests-trend", Guid.NewGuid().ToString("N"));

    private readonly FakeKlineSource _kline = new(emptyFor: [PendingCode], tradingDays: 400);
    private readonly HashSet<string> _enqueued = new(StringComparer.Ordinal);
    private readonly HashSet<string> _collected = new(StringComparer.Ordinal);
    private bool _marketDataReady;

    /// <summary>该代码是否曾被按需入队。</summary>
    public bool WasEnqueued(string code) => _enqueued.Contains(code);

    /// <summary>
    /// 采集指定标的的日线与指标（幂等）。
    /// </summary>
    public async Task CollectAsync(string code)
    {
        await EnsureMarketDataAsync();

        if (!_collected.Add(code))
        {
            return;
        }

        using var scope = Services.CreateScope();
        var provider = scope.ServiceProvider;

        await provider.GetRequiredService<BenchmarkDailyJob>().RunAsync();
        await provider.GetRequiredService<DailyKlineJob>().RunIncrementalAsync(code);
        await provider.GetRequiredService<IndicatorJob>().RunAsync(code);

        // 总览页的每张卡都有各自的数据来源，采集齐全才能验证「八个模块全部就绪」
        await provider.GetRequiredService<FinanceJob>().RunAsync(code);
        await provider.GetRequiredService<EquityJob>().RunAsync(code);
        await provider.GetRequiredService<CapitalJob>().RunAsync(code);
        await provider.GetRequiredService<RatingJob>().RunAsync(code);
    }

    /// <summary>
    /// 先把股票池与快照准备好：行情条、行业与板块都来自这两张表。
    /// </summary>
    private async Task EnsureMarketDataAsync()
    {
        if (_marketDataReady)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var provider = scope.ServiceProvider;

        await provider.GetRequiredService<IndexJob>().RunAsync();
        await provider.GetRequiredService<UniverseJob>().RunAsync();
        await provider.GetRequiredService<QuoteSnapshotJob>().RunAsync();

        _marketDataReady = true;
    }

    /// <summary>
    /// 执行一轮按需采集（等价于调度在下一轮消费队列）。
    /// </summary>
    /// <remarks>
    /// 这里刻意模拟「用户等待期间上游已经可用」：把标的从无数据集合里移除，
    /// 再跑一次采集任务，验证 1003 之后确实能自动变成有数据。
    /// </remarks>
    public async Task RunOnDemandAsync()
    {
        using var scope = Services.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IOnDemandQueue>();
        var codes = queue.Drain(3);

        foreach (var code in codes)
        {
            _enqueued.Add(code);
            _kline.MakeAvailable(code);

            await scope.ServiceProvider.GetRequiredService<DailyKlineJob>().RunIncrementalAsync(code);
            await scope.ServiceProvider.GetRequiredService<IndicatorJob>().RunAsync(code);
        }
    }

    /// <summary>
    /// 取一个用于调用接口的客户端。
    /// </summary>
    /// <remarks>
    /// 本应用已去掉登录与权限，所有端点匿名可访问，因此不再需要登录取令牌。
    /// 方法名与签名保持不变，避免改动几十处调用点。
    /// </remarks>
    public Task<HttpClient> CreateAdminClientAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        return Task.FromResult(client);
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 只有 40 个交易日：用来验证 MA250 与 250 日分位在样本不足时返回空
        _kline.SetTradingDays(ShortCode, 40);

        builder.UseEnvironment("Development");
        builder.UseSetting("Sa:DataDirectory", _dataDirectory);
        builder.UseSetting("Sa:Auth:AdminInitialPassword", AdminPassword);
        builder.UseSetting("Sa:Auth:RequireTotp", "false");
        builder.UseSetting("Sa:Collector:Enabled", "false");
        // 只回补 40 天的标的：靠把回看窗口调小来构造「样本不足」场景
        builder.UseSetting("Sa:Collector:BackfillLookbackDays", "365");

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
            services.AddSingleton<IKlineSource>(_kline);

            // 财务/股权/资金面：本测试不关心，但必须换成替身，
            // 否则总览页会去打真实上游（测试就不再是离线可重复的）
            services.AddSingleton<IFinanceSource, FakeFinanceSource>();
            services.AddSingleton<IEquitySource, FakeEquitySource>();
            services.AddSingleton<ICapitalSource, FakeCapitalSource>();
            services.AddSingleton<IFundFlowSource, FakeFundFlowSource>();
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
            // SQLite / DuckDB 原生库释放有延迟，清理失败不影响测试结论
        }
    }
}
