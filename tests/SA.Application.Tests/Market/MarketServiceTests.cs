using SA.Application.Abstractions;
using SA.Application.Common;
using SA.Application.Market;
using SA.Application.Search;
using SA.Contracts.Common;
using SA.Domain.Entities.Collect;
using SA.Domain.Entities.Market;

namespace SA.Application.Tests.Market;

/// <summary>
/// 市场概览聚合。页面上的涨跌家数、成交额与三张榜单全部由个股快照在内存里聚合，
/// 口径算错会直接体现为「数字与东财对不上」，因此逐个口径固定下来。
/// </summary>
public class MarketServiceTests
{
    private const string AsOfDate = "2026-09-18";

    [Fact]
    public async Task 涨跌家数按快照统计而成交额换算为亿元()
    {
        var service = Build(
            quotes:
            [
                Quote("600519", price: 1257.12m, pct: 1.24m, amount: 9_860_000_000m, cap: 1_968_000_000_000m),
                Quote("300750", price: 301.95m, pct: -0.77m, amount: 11_537_664_647.91m, cap: 1_397_193_466_937m),
                Quote("000002", price: 3.32m, pct: 9.93m, amount: 1_419_831_684.34m, cap: 39_609_955_444m),
                Quote("600048", price: 8.62m, pct: 0m, amount: 3_820_000_000m, cap: 103_200_000_000m)
            ]);

        var result = await service.GetBreadthAsync();

        Assert.True(result.Ok);
        var breadth = result.Value!;
        Assert.Equal(2, breadth.Up);
        Assert.Equal(1, breadth.Down);
        Assert.Equal(1, breadth.Flat);

        // 总数只计「有行情」的标的（停牌与退市不进快照）
        Assert.Equal(4, breadth.Total);

        // (98.6 + 115.3766464791 + 14.1983168434 + 38.2) 亿 = 266.3749633225 亿 → 266.37
        Assert.Equal(266.37m, breadth.Turnover);
    }

    [Fact]
    public async Task 北向资金本轮为空态且带口径说明()
    {
        var service = Build(quotes: [Quote("600519", 1257.12m, 1.24m, 1_000m, 1_000m)]);

        var result = await service.GetBreadthAsync();

        // 不允许用估算值冒充披露值：未取得就返回 null，并必须带上原因说明
        Assert.Null(result.Value!.Northbound);
        Assert.Empty(result.Value.Northbound5);
        Assert.Contains("不再公开披露", result.Value.NorthboundNote, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 涨幅榜剔除ST而成交额榜保留()
    {
        var service = Build(
            quotes:
            [
                Quote("600519", 1257.12m, 1.24m, 9_860_000_000m, 1_968_000_000_000m),
                Quote("000005", 3.32m, 10.02m, 12_000_000_000m, 8_000_000_000m)
            ],
            instruments:
            [
                Instrument("600519", "贵州茅台"),
                Instrument("000005", "ST星源", isSt: true)
            ]);

        var result = await service.GetRankingsAsync(take: 5);

        var rankings = result.Value!;

        // ST 的极端涨幅会霸榜，失去参考价值，因此涨幅榜剔除
        Assert.DoesNotContain(rankings.Gainers, r => r.Code == "000005");
        Assert.Contains(rankings.Gainers, r => r.Code == "600519");

        // 成交额榜不剔除：成交额是客观规模指标
        Assert.Contains(rankings.Amount, r => r.Code == "000005");
        Assert.Equal("000005", rankings.Amount[0].Code);
    }

    [Fact]
    public async Task 涨幅榜剔除新股而成交额榜保留()
    {
        var service = Build(
            quotes:
            [
                Quote("600519", 1257.12m, 1.24m, 9_860_000_000m, 1_968_000_000_000m),
                Quote("601091", 57.77m, 177.74m, 12_000_000_000m, 179_664_849_567m)
            ],
            instruments:
            [
                Instrument("600519", "贵州茅台"),
                // 上市首日：名称前缀 N / C 是东财的新股标注
                Instrument("601091", "C沈鼓")
            ]);

        var rankings = (await service.GetRankingsAsync(take: 5)).Value!;

        // 「C沈鼓 +177.74%」这类首日标的会长期霸榜，必须剔除
        Assert.DoesNotContain(rankings.Gainers, r => r.Code == "601091");
        Assert.Contains(rankings.Gainers, r => r.Code == "600519");

        // 成交额是客观规模指标，不剔除；同时透出 IsNew 便于界面解释
        var newStock = rankings.Amount.Single(r => r.Code == "601091");
        Assert.True(newStock.IsNew);
        Assert.False(rankings.Amount.Single(r => r.Code == "600519").IsNew);
    }

    [Fact]
    public async Task 榜单按成交额与涨跌幅排序并带出板块与行业()
    {
        var service = Build(
            quotes:
            [
                Quote("600519", 1257.12m, 1.24m, 9_860_000_000m, 1_968_000_000_000m),
                Quote("300750", 301.95m, -0.77m, 11_537_664_647.91m, 1_397_193_466_937m)
            ],
            instruments:
            [
                Instrument("600519", "贵州茅台", industry: "白酒"),
                Instrument("300750", "宁德时代", industry: "电池")
            ]);

        var rankings = (await service.GetRankingsAsync(take: 5)).Value!;

        Assert.Equal("300750", rankings.Amount[0].Code);
        Assert.Equal(115.38m, rankings.Amount[0].Amount);
        Assert.Equal("电池", rankings.Amount[0].Industry);
        Assert.Equal("创业板", rankings.Amount[0].Board);

        Assert.Equal("600519", rankings.Gainers[0].Code);
        Assert.Equal("300750", rankings.Losers[0].Code);
    }

    [Fact]
    public async Task 无快照时概览返回1003而不是空数据()
    {
        var service = Build(quotes: []);

        var result = await service.GetOverviewAsync();

        Assert.False(result.Ok);
        Assert.Equal(ErrorCode.DataNotReady, result.Error);
    }

    [Fact]
    public async Task 有快照时概览返回四个区块与新鲜度()
    {
        var service = Build(
            quotes: [Quote("300750", 301.95m, -0.77m, 11_537_664_647.91m, 1_397_193_466_937m)],
            instruments: [Instrument("300750", "宁德时代", industry: "电池")],
            indices:
            [
                new IndexQuote
                {
                    Code = "000001", Name = "上证指数", Market = 1, SortOrder = 1, Displayed = true,
                    Price = 3911.87m, Change = 36.27m, Pct = 0.94m, Amount = 994_169_450_166.2m,
                    AsOf = new DateOnly(2026, 9, 18), UpdatedAt = DateTimeOffset.Now
                }
            ],
            sectors:
            [
                new Sector
                {
                    Code = "BK1201", Name = "电子", Pct = 2.81m, MainNet = 19_853_959_168m,
                    UpCount = 300, DownCount = 120, LeaderName = "某电子", LeaderCode = "300001",
                    AsOf = new DateOnly(2026, 9, 18), UpdatedAt = DateTimeOffset.Now
                }
            ]);

        var result = await service.GetOverviewAsync();

        Assert.True(result.Ok);
        var overview = result.Value!;

        Assert.Single(overview.Indices);
        Assert.Equal(3911.87m, overview.Indices[0].Price);
        Assert.Equal(9941.69m, overview.Indices[0].Amount);

        Assert.Single(overview.Industries);
        Assert.Equal(198.54m, overview.Industries[0].Flow);

        Assert.Equal(AsOfDate, overview.Status.AsOf);
        Assert.True(overview.Status.IsReady);
    }

    /* ------------------------------------------------------------------
       构造工具
       ------------------------------------------------------------------ */

    private static MarketService Build(
        IReadOnlyList<QuoteSnapshot> quotes,
        IReadOnlyList<Instrument>? instruments = null,
        IReadOnlyList<IndexQuote>? indices = null,
        IReadOnlyList<Sector>? sectors = null,
        MarketStat? stat = null)
    {
        var instrumentsValue = instruments ?? quotes.Select(q => Instrument(q.Code, q.Code)).ToList();
        var instrumentsByCode = instrumentsValue.ToDictionary(i => i.Code, StringComparer.Ordinal);

        var cache = new MarketSnapshotCache();
        cache.Replace(quotes, instrumentsByCode);

        return new MarketService(
            cache,
            new FakeQuoteStore(quotes),
            new FakeInstrumentStore(instrumentsValue),
            new FakeIndexStore(indices ?? []),
            new FakeSectorStore(sectors ?? []),
            new FakeMarketStatStore(stat),
            new FakeCollectStatusStore(),
            new FakeCalendarStore());
    }

    private static QuoteSnapshot Quote(string code, decimal price, decimal pct, decimal amount, decimal cap) =>
        new()
        {
            Code = code,
            Price = price,
            Change = 0m,
            Pct = pct,
            Volume = 1_000_000m,
            Amount = amount,
            Turnover = 1m,
            VolRatio = 1m,
            Open = price,
            High = price,
            Low = price,
            PrevClose = price,
            MarketCap = cap,
            FloatCap = cap,
            Pe = 20m,
            PeTtm = 20m,
            Pb = 3m,
            AsOf = DateOnly.Parse(AsOfDate),
            UpdatedAt = DateTimeOffset.Now
        };

    private static Instrument Instrument(string code, string name, string? industry = null, bool isSt = false) =>
        new()
        {
            Code = code,
            Name = name,
            Pinyin = null,
            Market = SA.Domain.Common.MarketCodes.MarketOf(code),
            Board = SA.Domain.Common.MarketCodes.BoardOf(code),
            Industry = industry,
            IsSt = isSt,
            UpdatedOn = DateOnly.Parse(AsOfDate)
        };
}

/// <summary>个股快照仓储的测试替身。</summary>
internal sealed class FakeQuoteStore(IReadOnlyList<QuoteSnapshot> rows) : IQuoteSnapshotStore
{
    public Task<IReadOnlyList<QuoteSnapshot>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(rows);

    public Task<IReadOnlyDictionary<string, QuoteSnapshot>> GetByCodesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, QuoteSnapshot>>(
            rows.Where(r => codes.Contains(r.Code)).ToDictionary(r => r.Code, StringComparer.Ordinal));

    public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(rows.Count);

    public Task<DateTimeOffset?> GetLastUpdatedAtAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(rows.Count == 0 ? null : (DateTimeOffset?)rows.Max(r => r.UpdatedAt));

    public Task<int> ReplaceAllAsync(IReadOnlyList<QuoteSnapshot> snapshots, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("市场读模型不应写入");
}

/// <summary>股票池仓储的测试替身。</summary>
internal sealed class FakeInstrumentStore(IReadOnlyList<Instrument> rows) : IInstrumentStore
{
    public Task<IReadOnlyList<Instrument>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(rows);

    public Task<IReadOnlyDictionary<string, Instrument>> GetByCodesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, Instrument>>(
            rows.Where(r => codes.Contains(r.Code)).ToDictionary(r => r.Code, StringComparer.Ordinal));

    public Task<Instrument?> FindAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(rows.FirstOrDefault(r => r.Code == code));

    public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(rows.Count);

    public Task<int> UpsertAsync(IReadOnlyList<Instrument> instruments, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("市场读模型不应写入");

    public Task<int> UpdatePinyinAsync(
        IReadOnlyDictionary<string, string> pinyinByCode,
        CancellationToken cancellationToken = default) => throw new NotSupportedException("市场读模型不应写入");
}

/// <summary>指数仓储的测试替身。</summary>
internal sealed class FakeIndexStore(IReadOnlyList<IndexQuote> rows) : IIndexStore
{
    public Task<IReadOnlyList<IndexQuote>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(rows);

    public Task<int> ReplaceAllAsync(IReadOnlyList<IndexQuote> indices, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("市场读模型不应写入");

    public Task<DateTimeOffset?> GetLastUpdatedAtAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(rows.Count == 0 ? null : (DateTimeOffset?)rows.Max(r => r.UpdatedAt));
}

/// <summary>行业板块仓储的测试替身。</summary>
internal sealed class FakeSectorStore(IReadOnlyList<Sector> rows) : ISectorStore
{
    public Task<IReadOnlyList<Sector>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(rows);

    public Task<int> ReplaceAllAsync(IReadOnlyList<Sector> sectors, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("市场读模型不应写入");

    public Task<DateTimeOffset?> GetLastUpdatedAtAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(rows.Count == 0 ? null : (DateTimeOffset?)rows.Max(r => r.UpdatedAt));
}

/// <summary>市场统计仓储的测试替身。</summary>
internal sealed class FakeMarketStatStore(MarketStat? stat) : IMarketStatStore
{
    public Task<MarketStat?> FindAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        Task.FromResult(stat);

    public Task<MarketStat?> GetLatestAsync(CancellationToken cancellationToken = default) => Task.FromResult(stat);

    public Task UpsertAsync(MarketStatPatch patch, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("市场读模型不应写入");
}

/// <summary>采集状态仓储的测试替身。</summary>
internal sealed class FakeCollectStatusStore : ICollectStatusStore
{
    public Task<IReadOnlyList<DataSourceStatus>> GetSourcesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DataSourceStatus>>([]);

    public Task RecordSourceAsync(
        string source,
        string domains,
        string type,
        bool ok,
        long latencyMs,
        string? error,
        int degradeAfterFailures,
        CancellationToken cancellationToken = default) => throw new NotSupportedException("市场读模型不应写入");

    public Task<long> AddTaskLogAsync(CollectTaskLog log, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("市场读模型不应写入");

    public Task<IReadOnlyList<CollectTaskLog>> GetRecentTasksAsync(int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CollectTaskLog>>([]);
}

/// <summary>交易日历仓储的测试替身：默认「日历尚未生成」，走退化判定分支。</summary>
internal sealed class FakeCalendarStore : ITradingCalendarStore
{
    public Task<IReadOnlyList<TradingDay>> GetRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TradingDay>>([]);

    public Task<int> UpsertAsync(IReadOnlyList<TradingDay> days, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("市场读模型不应写入");

    public Task<DateOnly?> GetLastDateAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<DateOnly?>(null);
}
