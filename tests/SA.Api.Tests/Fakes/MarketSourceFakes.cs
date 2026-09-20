using SA.Application.Abstractions;

namespace SA.Api.Tests.Fakes;

/// <summary>
/// 采集源的测试替身：返回与真实上游同构的样本，使采集 → 落库 → 聚合 → 接口这条链路
/// 可以在完全离线的条件下被端到端验证。
/// </summary>
/// <remarks>
/// 存在的理由不只是「测试快」：实测东财 <c>push2</c> 主机在被连续请求后会临时拒绝服务，
/// 依赖真实上游的集成测试会时通时不通，无法作为回归依据。
/// </remarks>
internal sealed class FakeMarketListSource : IMarketListSource
{
    private readonly IReadOnlyList<MarketListRow> _rows;
    private readonly bool _fail;

    public FakeMarketListSource(IReadOnlyList<MarketListRow> rows, bool fail = false)
    {
        _rows = rows;
        _fail = fail;
    }

    public string Name => "测试源 · 全市场列表";

    public string Domains => "行情,股票池";

    /// <summary>采样集合：含正常标的、ST 标的与停牌标的（价格 0）。</summary>
    public static IReadOnlyList<MarketListRow> DefaultRows { get; } =
    [
        Row("600519", "贵州茅台", industry: "白酒", price: 1257.12m, pct: 1.24m, amount: 9_860_000_000m, cap: 1_968_000_000_000m),
        Row("300750", "宁德时代", industry: "电池", price: 301.95m, pct: -0.77m, amount: 11_537_664_647.91m, cap: 1_397_193_466_937m),
        Row("000002", "万  科Ａ", industry: "房地产开发", price: 3.32m, pct: 9.93m, amount: 1_419_831_684.34m, cap: 39_609_955_444m),
        Row("000005", "ST星源", industry: "综合", price: 3.32m, pct: 10.02m, amount: 120_000_000m, cap: 800_000_000m),
        Row("600048", "保利发展", industry: "房地产开发", price: 8.62m, pct: 0m, amount: 3_820_000_000m, cap: 103_200_000_000m),
        // 停牌：价格 0，解析后不进快照
        Row("000003", "PT金田A", industry: null, price: 0m, pct: 0m, amount: 0m, cap: 0m)
    ];

    public Task<MarketListResult> GetMarketListAsync(
        Func<IReadOnlyList<MarketListRow>, Task>? onPage = null,
        CancellationToken cancellationToken = default)
    {
        if (_fail)
        {
            throw new InvalidOperationException("测试源按配置失败");
        }

        return Complete();

        async Task<MarketListResult> Complete()
        {
            if (onPage is not null)
            {
                await onPage(_rows).ConfigureAwait(false);
            }

            return new MarketListResult(_rows.Count, _rows.Count, new DateOnly(2026, 9, 18));
        }
    }

    public Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SourceProbeResult.Success(1, 200, _rows.Count));

    private static MarketListRow Row(
        string code,
        string name,
        string? industry,
        decimal price,
        decimal pct,
        decimal amount,
        decimal cap) =>
        new(
            Code: code,
            Name: name,
            Market: Domain.Common.MarketCodes.MarketOf(code),
            Industry: industry,
            Price: price,
            Change: pct == 0 ? 0 : pct / 10,
            Pct: pct,
            Volume: 1_000_000m,
            Amount: amount,
            Turnover: 1.5m,
            VolRatio: 1.2m,
            Pe: 20m,
            PeTtm: 21m,
            Pb: 3m,
            MarketCap: cap,
            FloatCap: cap,
            Open: price,
            High: price,
            Low: price,
            PrevClose: price);
}

/// <summary>指数源的测试替身：口径日固定为 2026-09-18，用于验证快照业务日期取自指数。</summary>
internal sealed class FakeIndexSource : IIndexSource
{
    private static readonly DateTimeOffset QuoteTime = new(2026, 9, 18, 16, 11, 32, TimeSpan.FromHours(8));

    public string Name => "测试源 · 指数行情";

    public string Domains => "行情,指数";

    public Task<IReadOnlyList<IndexQuoteRow>> GetIndicesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IndexQuoteRow>>(
        [
            new("000001", "上证指数", 1, 3911.87m, 36.27m, 0.94m, 485_712_507m, 994_169_450_166.2m, QuoteTime),
            new("399001", "深证成指", 0, 13640.87m, 230.96m, 1.72m, 597_152_064m, 1_082_930_845_525.16m, QuoteTime),
            new("399006", "创业板指", 0, 3372.68m, 74.37m, 2.25m, 167_653_749m, 525_400_868_852.42m, QuoteTime),
            new("000688", "科创50", 1, 1652.63m, 46.25m, 2.88m, 1m, 117_653_000_000m, QuoteTime),
            new("899050", "北证50", 0, 1044.34m, 18.34m, 1.79m, 6_970_704m, 16_053_052_572m, QuoteTime),
            // 沪深300 只作基准，不在概览卡片区展示
            new("000300", "沪深300", 1, 4507.39m, 47.23m, 1.06m, 189_924_166m, 537_699_359_226.9m, QuoteTime)
        ]);

    public Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SourceProbeResult.Success(1, 200, 6));
}

/// <summary>行业板块源的测试替身。</summary>
internal sealed class FakeSectorSource : ISectorSource
{
    public string Name => "测试源 · 行业板块";

    public string Domains => "行业,行情";

    public Task<IReadOnlyList<SectorRow>> GetSectorsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SectorRow>>(
        [
            new("BK1201", "电子", 2.81m, 19_853_959_168m, 300, 120, "某电子", "300001", 45.6m),
            new("BK1033", "电池", 3.86m, 2_860_000_000m, 60, 20, "宁德时代", "300750", 22.4m),
            new("BK0475", "房地产开发", -1.24m, -920_000_000m, 30, 80, "保利发展", "600048", null)
        ]);

    public Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SourceProbeResult.Success(1, 200, 3));
}

/// <summary>涨跌停源的测试替身。</summary>
internal sealed class FakeLimitPoolSource : ILimitPoolSource
{
    public string Name => "测试源 · 涨跌停池";

    public string Domains => "行情,市场情绪";

    public Task<LimitPoolResult> GetLimitPoolsAsync(DateOnly? date = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new LimitPoolResult(new DateOnly(2026, 9, 18), LimitUp: 47, LimitDown: 1));

    public Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SourceProbeResult.Success(1, 200, 48));
}

/// <summary>两融余额源的测试替身。</summary>
internal sealed class FakeMarginMarketSource : IMarginMarketSource
{
    public string Name => "测试源 · 两融余额";

    public string Domains => "资金,两融";

    public Task<MarginMarketResult?> GetLatestAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<MarginMarketResult?>(
            new MarginMarketResult(new DateOnly(2026, 9, 17), 2_601_526_431_491m, 29_234_322_392m));

    public Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SourceProbeResult.Success(1, 200, 1));
}

/// <summary>大盘资金流源的测试替身。</summary>
internal sealed class FakeMarketFundFlowSource : IMarketFundFlowSource
{
    public string Name => "测试源 · 大盘资金流";

    public string Domains => "资金";

    public Task<MarketFundFlowResult?> GetLatestAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<MarketFundFlowResult?>(
            new MarketFundFlowResult(new DateOnly(2026, 9, 18), 145_140_858_88m, 13_733_306_368m, 780_779_520m, -12_080_132_096m, -2_433_945_600m));

    public Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SourceProbeResult.Success(1, 200, 2));
}

/// <summary>交易日历源的测试替身：只把 09-18 与 09-17 视为交易日。</summary>
internal sealed class FakeTradingCalendarSource : ITradingCalendarSource
{
    public string Name => "测试源 · 交易日历";

    public string Domains => "日历";

    public Task<IReadOnlyList<DateOnly>> GetTradingDaysAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DateOnly>>(
            new[] { new DateOnly(2026, 9, 17), new DateOnly(2026, 9, 18) }
                .Where(d => d >= from && d <= to)
                .ToList());

    public Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SourceProbeResult.Success(1, 200, 2));
}
