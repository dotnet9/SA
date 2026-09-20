using SA.Application.Abstractions;
using SA.Application.Events;
using SA.Application.Tests.Market;
using SA.Domain.Entities.Capital;
using SA.Domain.Entities.Equity;
using SA.Domain.Entities.Events;
using SA.Domain.Entities.Finance;
using SA.Domain.Entities.Market;

namespace SA.Application.Tests.Events;

/// <summary>
/// 事件派生与四张拓扑图。事件全部由本地数据派生，因此每条派生规则与人工覆盖都必须钉住。
/// </summary>
public class EventTimelineServiceTests
{
    private const string AsOf = "2026-09-18";

    [Fact]
    public async Task 业绩预告派生为事件并按同比幅度定强度()
    {
        var service = Build(
            reports: [],
            forecasts:
            [
                Forecast(new DateOnly(2026, 6, 30), changeMax: 60m, type: "预增", notice: new DateOnly(2026, 7, 10)),
                Forecast(new DateOnly(2026, 3, 31), changeMax: -25m, type: "预减", notice: new DateOnly(2026, 4, 8))
            ]);

        var dto = (await service.GetAsync("300750", canAnnotate: false)).Value!;

        var increase = dto.Events.Single(item => item.Key == "forecast:2026-06-30");
        Assert.Equal("up", increase.Tone);
        Assert.Equal(5, increase.Impact);
        Assert.Equal("2026-07-10", increase.Date);
        Assert.Equal("业绩预告", increase.TypeName);

        var decrease = dto.Events.Single(item => item.Key == "forecast:2026-03-31");
        Assert.Equal("down", decrease.Tone);
        Assert.Equal(4, decrease.Impact);
    }

    [Fact]
    public async Task 定期报告与分红方案各自成为一条事件()
    {
        var service = Build(reports: [Report(new DateOnly(2026, 6, 30), netProfitYoy: 41.98m, dividend: "10派14.11元")]);

        var dto = (await service.GetAsync("300750", canAnnotate: false)).Value!;

        var report = dto.Events.Single(item => item.Type == "report");
        Assert.Equal("up", report.Tone);
        Assert.Contains("营收", report.Detail!, StringComparison.Ordinal);

        var dividend = dto.Events.Single(item => item.Type == "dividend");
        // 分红是中性事件：高分红偏积极，但也可能意味着缺乏再投资机会
        Assert.Equal("neutral", dividend.Tone);
        Assert.Equal("10派14.11元", dividend.Title);
    }

    [Fact]
    public async Task 缺公告日的报告不进时间线()
    {
        // 需要另一条事件让页面正常返回，这样才验证得了「报告没进线」而不是「整页失败」
        var service = Build(
            reports: [Report(new DateOnly(2026, 6, 30), netProfitYoy: 10m, withNotice: false)],
            blockTrades: [Block(new DateOnly(2026, 9, 15), premium: 0m, amount: 1_000_000m)]);

        var dto = (await service.GetAsync("300750", canAnnotate: false)).Value!;

        // 没有公告日就无法定位「什么时候发生」，因此不派生事件
        Assert.DoesNotContain(dto.Events, item => item.Type == "report");
        Assert.Contains(dto.Events, item => item.Type == "blocktrade");
    }

    [Fact]
    public async Task 股东户数只在变动超过阈值时成为事件()
    {
        var service = Build(
            holderCounts:
            [
                Count(new DateOnly(2026, 3, 31), holderNum: 120_000, ratio: -1.2m),
                Count(new DateOnly(2026, 6, 30), holderNum: 100_000, ratio: -16.7m)
            ]);

        var dto = (await service.GetAsync("300750", canAnnotate: false)).Value!;

        // 变动 1.2% 的那期是噪音，不入时间线；16.7% 的那期入线且判为偏积极（筹码集中）
        var holderEvents = dto.Events.Where(item => item.Type == "holderCount").ToList();
        Assert.Single(holderEvents);
        Assert.Equal("up", holderEvents[0].Tone);
        Assert.Equal(3, holderEvents[0].Impact);
        Assert.Contains("减少", holderEvents[0].Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 大宗交易按折溢价判定方向()
    {
        var service = Build(
            blockTrades:
            [
                Block(new DateOnly(2026, 9, 15), premium: -6.16m, amount: 3_163_600m),
                Block(new DateOnly(2026, 9, 16), premium: 5.2m, amount: 200_000_000m)
            ]);

        var dto = (await service.GetAsync("300750", canAnnotate: false)).Value!;

        var trades = dto.Events.Where(item => item.Type == "blocktrade").OrderBy(item => item.Date).ToList();
        Assert.Equal(2, trades.Count);

        // 折价成交偏消极、溢价成交偏积极
        Assert.Equal("down", trades[0].Tone);
        Assert.Equal("up", trades[1].Tone);

        // 成交额过亿的强度更高
        Assert.Equal(3, trades[1].Impact);
    }

    [Fact]
    public async Task 人工标注覆盖规则判读并可恢复()
    {
        var annotations = new FakeAnnotationStore();
        var service = Build(
            blockTrades: [Block(new DateOnly(2026, 9, 15), premium: -6m, amount: 3_163_600m)],
            annotations: annotations);

        var before = (await service.GetAsync("300750", canAnnotate: true)).Value!;
        var target = before.Events.Single(item => item.Type == "blocktrade");
        Assert.Equal("down", target.Tone);
        Assert.False(target.Annotated);
        Assert.True(before.CanAnnotate);

        // 人工判读为「中性 1 级」并留备注
        await annotations.UpsertAsync(new EventAnnotation
        {
            Code = "300750",
            EventKey = target.Key,
            Tone = "neutral",
            Impact = 1,
            Note = "同一机构申赎调仓，不代表方向",
            UserId = "tester",
            UpdatedAt = DateTimeOffset.Now
        });

        var after = (await service.GetAsync("300750", canAnnotate: true)).Value!;
        var updated = after.Events.Single(item => item.Key == target.Key);

        Assert.Equal("neutral", updated.Tone);
        Assert.Equal(1, updated.Impact);
        Assert.True(updated.Annotated);
        Assert.Equal("同一机构申赎调仓，不代表方向", updated.AnnotationNote);

        // 删除标注后恢复规则判读
        await annotations.RemoveAsync("300750", target.Key);

        var restored = (await service.GetAsync("300750", canAnnotate: true)).Value!;
        var back = restored.Events.Single(item => item.Key == target.Key);
        Assert.Equal("down", back.Tone);
        Assert.False(back.Annotated);
    }

    [Fact]
    public async Task 未授权时不下发可标注标记()
    {
        var service = Build(blockTrades: [Block(new DateOnly(2026, 9, 15), premium: -6m, amount: 1m)]);

        var dto = (await service.GetAsync("300750", canAnnotate: false)).Value!;

        Assert.False(dto.CanAnnotate);
    }

    [Fact]
    public async Task 股东拓扑以公司为中心并带持股比例()
    {
        var service = Build(holders: Holders());

        var dto = (await service.GetAsync("300750", canAnnotate: false)).Value!;
        var topology = dto.Topologies.Single(item => item.Key == "shareholder");

        Assert.True(topology.Available);

        // 中心节点是本公司且高亮
        var center = topology.Nodes.Single(node => node.Highlight);
        Assert.Equal("stock:300750", center.Id);

        // 10 个股东节点，边上的标注是持股比例
        Assert.Equal(11, topology.Nodes.Count);
        Assert.Equal(10, topology.Edges.Count);
        Assert.All(topology.Edges, edge => Assert.Equal("stock:300750", edge.Target));
        Assert.Contains(topology.Edges, edge => edge.Label is not null && edge.Label.EndsWith('%'));
    }

    [Fact]
    public async Task 缺少股东数据时拓扑显式不可用并给出原因()
    {
        var service = Build();

        var dto = (await service.GetAsync("300750", canAnnotate: false)).Value!;
        var topology = dto.Topologies.Single(item => item.Key == "shareholder");

        Assert.False(topology.Available);
        Assert.Contains("十大股东", topology.UnavailableReason!, StringComparison.Ordinal);
        Assert.Empty(topology.Nodes);
    }

    [Fact]
    public async Task 供应链接拓扑不提供而是说明原因()
    {
        var service = Build(blockTrades: [Block(new DateOnly(2026, 9, 15), premium: -6m, amount: 1m)]);

        var dto = (await service.GetAsync("300750", canAnnotate: false)).Value!;

        // 明确说明「不提供」而不是给一张空图或推测图
        Assert.Contains(dto.Notes, note => note.Contains("供应链", StringComparison.Ordinal));
        Assert.DoesNotContain(dto.Topologies, item => item.Key == "supplychain");
    }

    [Fact]
    public async Task 交易对手拓扑按对手方聚合金额()
    {
        var service = Build(
            blockTrades:
            [
                Block(new DateOnly(2026, 9, 15), premium: 0m, amount: 100_000_000m, buyer: "机构专用", seller: "营业部A"),
                Block(new DateOnly(2026, 9, 16), premium: 0m, amount: 200_000_000m, buyer: "机构专用", seller: "营业部B")
            ]);

        var dto = (await service.GetAsync("300750", canAnnotate: false)).Value!;
        var topology = dto.Topologies.Single(item => item.Key == "counterparty");

        Assert.True(topology.Available);

        // 「机构专用」买了两笔 → 聚合成一个节点（金额合计 3 亿）
        var institution = topology.Nodes.Single(node => node.Name == "机构专用");
        Assert.Equal(3m, institution.Value);
        Assert.Single(topology.Nodes.Where(node => node.Name == "机构专用"));
    }

    [Fact]
    public async Task 行业拓扑包含同业且按涨跌着色()
    {
        var service = Build(
            instruments:
            [
                Instrument("300750", "宁德时代", "电池"),
                Instrument("002594", "比亚迪", "电池"),
                Instrument("601012", "隆基绿能", "光伏设备")
            ],
            quotes:
            [
                Quote("300750", pct: -0.77m),
                Quote("002594", pct: 2.5m),
                Quote("601012", pct: -1.2m)
            ],
            sectors: [Sector("BK1033", "电池", pct: 1.74m)]);

        var dto = (await service.GetAsync("300750", canAnnotate: false)).Value!;
        var topology = dto.Topologies.Single(item => item.Key == "industry");

        Assert.True(topology.Available);

        // 只含同行业：比亚迪在内，隆基绿能不在
        // 节点标签用证券名称（图上的可读性来自名字），代码放在悬浮说明里
        var peer = topology.Nodes.Single(node => node.Id == "peer:002594");
        Assert.Equal("比亚迪", peer.Name);
        Assert.Contains("002594", peer.Note!, StringComparison.Ordinal);

        Assert.DoesNotContain(topology.Nodes, node => node.Id == "peer:601012");

        // 同业涨跌映射到边的颜色
        Assert.Contains(topology.Edges, edge => edge.Tone == "up");
    }

    [Fact]
    public async Task 缺少股东数据时触发按需采集()
    {
        var queue = new FakeOnDemandQueue();
        var service = Build(blockTrades: [Block(new DateOnly(2026, 9, 15), premium: 0m, amount: 1m)], onDemand: queue);

        await service.GetAsync("300750", canAnnotate: false);

        // 股东拓扑不可用（缺股权数据）→ 该标的应被入队补齐
        Assert.Contains("300750", queue.Enqueued);
    }

    [Fact]
    public async Task 既有事件又有股东数据时不再入队()
    {
        var queue = new FakeOnDemandQueue();
        var service = Build(
            holders: Holders(),
            blockTrades: [Block(new DateOnly(2026, 9, 15), premium: 0m, amount: 1m)],
            onDemand: queue);

        await service.GetAsync("300750", canAnnotate: false);

        Assert.Empty(queue.Enqueued);
    }

    /* ------------------------------------------------------------------
       构造工具
       ------------------------------------------------------------------ */

    private static EventTimelineService Build(
        IReadOnlyList<FinancialReport>? reports = null,
        IReadOnlyList<EarningsForecast>? forecasts = null,
        IReadOnlyList<TopHolder>? holders = null,
        IReadOnlyList<HolderCount>? holderCounts = null,
        PledgeStat? pledge = null,
        IReadOnlyList<BillboardRecord>? billboards = null,
        IReadOnlyList<BlockTrade>? blockTrades = null,
        IReadOnlyList<NorthboundHolding>? northbound = null,
        IReadOnlyList<Instrument>? instruments = null,
        IReadOnlyList<QuoteSnapshot>? quotes = null,
        IReadOnlyList<Sector>? sectors = null,
        FakeAnnotationStore? annotations = null,
        FakeOnDemandQueue? onDemand = null)
    {
        var instrumentRows = instruments ?? [Instrument("300750", "宁德时代", "电池")];
        var quoteRows = quotes ?? [Quote("300750", pct: -0.77m)];

        return new EventTimelineService(
            new FakeFinanceStore(reports ?? [], forecasts ?? []),
            new FakeEquityStore(holders ?? [], holderCounts ?? [], pledge),
            new FakeCapitalStore(billboards ?? [], blockTrades ?? [], northbound ?? []),
            new FakeInstrumentStore(instrumentRows),
            new FakeQuoteStore(quoteRows),
            new FakeSectorStore(sectors ?? []),
            annotations ?? new FakeAnnotationStore(),
            onDemand ?? new FakeOnDemandQueue());
    }

    private static FinancialReport Report(
        DateOnly reportDate,
        decimal netProfitYoy,
        string? dividend = null,
        bool withNotice = true) =>
        new()
        {
            Code = "300750",
            ReportDate = reportDate,
            ReportType = "2026年 半年报",
            Revenue = 276_916_580_000m,
            RevenueYoy = 54.8m,
            NetProfit = 43_284_002_000m,
            NetProfitYoy = netProfitYoy,
            Roe = 12.08m,
            DividendPlan = dividend,
            DividendYield = 0.36m,
            // withNotice = false 用于验证「缺公告日的报告不派生事件」
            NoticeDate = withNotice ? reportDate.AddDays(25) : null,
            UpdatedAt = DateTimeOffset.Now
        };

    private static EarningsForecast Forecast(DateOnly reportDate, decimal changeMax, string type, DateOnly notice) =>
        new()
        {
            Code = "300750",
            ReportDate = reportDate,
            Caliber = "归属于母公司股东的净利润",
            ForecastType = type,
            Summary = "预计业绩变动",
            ChangeMin = changeMax - 2m,
            ChangeMax = changeMax,
            NoticeDate = notice,
            UpdatedAt = DateTimeOffset.Now
        };

    private static HolderCount Count(DateOnly endDate, int holderNum, decimal ratio) =>
        new()
        {
            Code = "300750",
            EndDate = endDate,
            HolderNum = holderNum,
            PreviousHolderNum = holderNum + 10_000,
            HolderNumChange = -10_000,
            HolderNumRatio = ratio,
            NoticeDate = endDate.AddDays(25),
            UpdatedAt = DateTimeOffset.Now
        };

    private static BlockTrade Block(
        DateOnly tradeDate,
        decimal premium,
        decimal amount,
        string buyer = "机构专用",
        string seller = "营业部A") =>
        new()
        {
            Code = "300750",
            TradeDate = tradeDate,
            DealPrice = 316.36m,
            PremiumRatio = premium,
            DealVolume = 10_000m,
            DealAmount = amount,
            BuyerName = buyer,
            SellerName = seller,
            Close = 316.36m,
            UpdatedAt = DateTimeOffset.Now
        };

    private static List<TopHolder> Holders() =>
        Enumerable.Range(1, 10)
            .Select(rank => new TopHolder
            {
                Code = "300750",
                EndDate = new DateOnly(2026, 6, 30),
                Rank = rank,
                IsFreeFloat = false,
                HolderName = $"股东{rank}",
                HoldNum = 100_000_000m - rank * 1_000_000m,
                HoldRatio = 6m - rank * 0.4m,
                NoticeDate = new DateOnly(2026, 7, 30),
                UpdatedAt = DateTimeOffset.Now
            })
            .ToList();

    private static Instrument Instrument(string code, string name, string? industry) =>
        new()
        {
            Code = code,
            Name = name,
            Market = SA.Domain.Common.MarketCodes.MarketOf(code),
            Board = SA.Domain.Common.MarketCodes.BoardOf(code),
            Industry = industry,
            UpdatedOn = DateOnly.Parse(AsOf)
        };

    private static QuoteSnapshot Quote(string code, decimal pct) =>
        new()
        {
            Code = code,
            Price = 300m,
            Pct = pct,
            Volume = 1000m,
            Amount = 1_000_000m,
            MarketCap = 1_000_000_000m,
            FloatCap = 1_000_000_000m,
            Pe = 20m,
            PeTtm = 20m,
            Pb = 2m,
            AsOf = DateOnly.Parse(AsOf),
            UpdatedAt = DateTimeOffset.Now
        };

    private static Sector Sector(string code, string name, decimal pct) =>
        new()
        {
            Code = code,
            Name = name,
            Pct = pct,
            AsOf = DateOnly.Parse(AsOf),
            UpdatedAt = DateTimeOffset.Now
        };
}

/// <summary>财务存储替身（事件模块只读）。</summary>
internal sealed class FakeFinanceStore(
    IReadOnlyList<FinancialReport> reports,
    IReadOnlyList<EarningsForecast> forecasts) : IFinanceStore
{
    public Task<IReadOnlyList<FinancialReport>> GetReportsAsync(string code, int limit = 24, CancellationToken cancellationToken = default) =>
        Task.FromResult(reports);

    public Task<IReadOnlyList<EarningsForecast>> GetForecastsAsync(string code, int limit = 8, CancellationToken cancellationToken = default) =>
        Task.FromResult(forecasts);

    public Task<int> UpsertReportsAsync(IReadOnlyList<FinancialReport> rows, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("事件读模型不应写入");

    public Task<int> UpsertForecastsAsync(IReadOnlyList<EarningsForecast> rows, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("事件读模型不应写入");

    public Task<DateTimeOffset?> GetLastUpdatedAtAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult<DateTimeOffset?>(DateTimeOffset.Now);
}

/// <summary>股权存储替身（事件模块只读）。</summary>
internal sealed class FakeEquityStore(
    IReadOnlyList<TopHolder> holders,
    IReadOnlyList<HolderCount> counts,
    PledgeStat? pledge) : IEquityStore
{
    public Task<IReadOnlyList<TopHolder>> GetTopHoldersAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(holders);

    public Task<IReadOnlyList<HolderCount>> GetHolderCountsAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(counts);

    public Task<PledgeStat?> GetPledgeAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(pledge);

    public Task<int> UpsertTopHoldersAsync(IReadOnlyList<TopHolder> rows, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("事件读模型不应写入");

    public Task<int> UpsertHolderCountsAsync(IReadOnlyList<HolderCount> rows, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("事件读模型不应写入");

    public Task<int> UpsertPledgeAsync(PledgeStat row, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("事件读模型不应写入");

    public Task<DateTimeOffset?> GetLastUpdatedAtAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult<DateTimeOffset?>(DateTimeOffset.Now);
}

/// <summary>资金面存储替身（事件模块只读）。</summary>
internal sealed class FakeCapitalStore(
    IReadOnlyList<BillboardRecord> billboards,
    IReadOnlyList<BlockTrade> blockTrades,
    IReadOnlyList<NorthboundHolding> northbound) : ICapitalStore
{
    public Task<IReadOnlyList<FundFlowDaily>> GetFundFlowAsync(string code, int days, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FundFlowDaily>>([]);

    public Task<IReadOnlyList<BillboardRecord>> GetBillboardsAsync(string code, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult(billboards);

    public Task<IReadOnlyList<BlockTrade>> GetBlockTradesAsync(string code, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult(blockTrades);

    public Task<IReadOnlyList<MarginDetail>> GetMarginDetailsAsync(string code, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MarginDetail>>([]);

    public Task<IReadOnlyList<NorthboundHolding>> GetNorthboundAsync(string code, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult(northbound);

    public Task<int> UpsertFundFlowAsync(IReadOnlyList<FundFlowDaily> rows, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("事件读模型不应写入");

    public Task<int> UpsertBillboardsAsync(string code, IReadOnlyList<BillboardRecord> rows, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("事件读模型不应写入");

    public Task<int> ReplaceBlockTradesAsync(string code, IReadOnlyList<BlockTrade> rows, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("事件读模型不应写入");

    public Task<int> UpsertMarginDetailsAsync(IReadOnlyList<MarginDetail> rows, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("事件读模型不应写入");

    public Task<int> UpsertNorthboundAsync(IReadOnlyList<NorthboundHolding> rows, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("事件读模型不应写入");

    public Task<DateTimeOffset?> GetLastUpdatedAtAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult<DateTimeOffset?>(DateTimeOffset.Now);
}

/// <summary>按需采集队列替身：记录入队请求，供断言「缺数据时会触发补采」。</summary>
internal sealed class FakeOnDemandQueue : IOnDemandQueue
{
    private readonly List<string> _pending = [];

    /// <summary>已入队的代码。</summary>
    public IReadOnlyList<string> Enqueued => _pending;

    /// <inheritdoc />
    public int PendingCount => _pending.Count;

    /// <inheritdoc />
    public bool TryEnqueue(string code)
    {
        _pending.Add(code);
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Drain(int max) => [];
}

/// <summary>人工标注存储替身（内存实现，支持写入与删除）。</summary>
internal sealed class FakeAnnotationStore : SA.Application.Abstractions.IEventAnnotationStore
{
    private readonly Dictionary<string, EventAnnotation> _rows = new(StringComparer.Ordinal);

    public Task<IReadOnlyDictionary<string, EventAnnotation>> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, EventAnnotation>>(
            _rows.Values.Where(row => row.Code == code).ToDictionary(row => row.EventKey, StringComparer.Ordinal));

    public Task<int> UpsertAsync(EventAnnotation annotation, CancellationToken cancellationToken = default)
    {
        _rows[annotation.EventKey] = annotation;
        return Task.FromResult(1);
    }

    public Task<int> RemoveAsync(string code, string eventKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(_rows.Remove(eventKey) ? 1 : 0);
}
