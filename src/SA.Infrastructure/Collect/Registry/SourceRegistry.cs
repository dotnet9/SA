using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Domain.History;

namespace SA.Infrastructure.Collect.Registry;

/// <summary>
/// 数据源注册表：集中声明每个域的主源与备源顺序，供采集任务与探活使用。
/// </summary>
/// <remarks>
/// <para>
/// 降级链在原型与详细设计 §6.2 中写作「腾讯 → 新浪 → 东方财富」，但实施计划 §3.1 的实测结论是
/// <b>东财为主源</b>（字段最全、覆盖最广），腾讯为快照类备源，新浪需 <c>Referer</c>。
/// </para>
/// <para>
/// <b>2026-09-21 复核后的实际顺序</b>（每一处偏离都有实测依据，见下）：
/// </para>
/// <list type="bullet">
/// <item><b>K 线：腾讯 → 东财</b>。东财 <c>push2his</c> 的 kline 端点在本机被连接重置，
/// 而同一主机的 fflow 端点正常。腾讯优先可避免「每次请求先失败重试 3 次再降级」。</item>
/// <item><b>全市场列表：东财 push2delay → 新浪</b>。push2 的 clist 被拒，push2delay 可用
/// <b>且带 <c>f100</c> 行业字段</b>；新浪列表实时但<b>完全没有行业字段</b>。</item>
/// </list>
/// <para>
/// 因此顺序不是「谁更权威」，而是「谁当前真的可用，且信息损失最小」。
/// 上游恢复后改 <c>DependencyInjection</c> 的注册顺序即可，无需改本类。
/// </para>
/// </remarks>
public sealed class SourceRegistry
{
    private readonly ILogger<SourceRegistry> _logger;

    /// <summary>
    /// 构造注册表。
    /// </summary>
    public SourceRegistry(
        IEnumerable<IMarketListSource> marketLists,
        IIndexSource indices,
        ISectorSource sectors,
        ILimitPoolSource limitPools,
        IMarginMarketSource margin,
        IMarketFundFlowSource marketFundFlow,
        IEnumerable<ITradingCalendarSource> calendars,
        IEnumerable<IKlineSource> klines,
        IFinanceSource finance,
        IFundamentalSource fundamental,
        IEquitySource equity,
        ICapitalSource capital,
        IFundFlowSource fundFlow,
        IRatingSource rating,
        IEnumerable<IQuoteSnapshotSource> quoteSnapshots,
        IEnumerable<IProbeable> allSources,
        ILogger<SourceRegistry> logger)
    {
        _logger = logger;
        Indices = indices;
        Sectors = sectors;
        LimitPools = limitPools;
        Margin = margin;
        MarketFundFlow = marketFundFlow;
        Finance = finance;
        Fundamental = fundamental;
        Equity = equity;
        Capital = capital;
        FundFlow = fundFlow;
        Rating = rating;

        // 注册顺序即降级顺序：主源在前
        MarketLists = marketLists.ToList();
        Klines = klines.ToList();
        CalendarSources = calendars.ToList();
        QuoteSnapshots = quoteSnapshots.ToList();
        All = allSources.Distinct().ToList();
    }

    /// <summary>全市场列表源，按降级顺序排列（主源在前）。</summary>
    public IReadOnlyList<IMarketListSource> MarketLists { get; }

    /// <summary>指数源。</summary>
    public IIndexSource Indices { get; }

    /// <summary>行业板块源。</summary>
    public ISectorSource Sectors { get; }

    /// <summary>涨跌停统计源。</summary>
    public ILimitPoolSource LimitPools { get; }

    /// <summary>两融余额源。</summary>
    public IMarginMarketSource Margin { get; }

    /// <summary>大盘资金流源。</summary>
    public IMarketFundFlowSource MarketFundFlow { get; }

    /// <summary>交易日历源，按降级顺序排列（主源在前）。</summary>
    public IReadOnlyList<ITradingCalendarSource> CalendarSources { get; }

    /// <summary>K 线源，按降级顺序排列（主源在前）。</summary>
    public IReadOnlyList<IKlineSource> Klines { get; }

    /// <summary>财务报表源。</summary>
    public IFinanceSource Finance { get; }

    /// <summary>基本面指标源（按报告期扫全市场的横截面因子）。</summary>
    public IFundamentalSource Fundamental { get; }

    /// <summary>股权结构源。</summary>
    public IEquitySource Equity { get; }

    /// <summary>资金面报表源（数据中心主机）。</summary>
    public ICapitalSource Capital { get; }

    /// <summary>个股资金流源（行情侧主机）。</summary>
    public IFundFlowSource FundFlow { get; }

    /// <summary>机构评级源。</summary>
    public IRatingSource Rating { get; }

    /// <summary>多标的快照源，按降级顺序排列（主源在前）。</summary>
    public IReadOnlyList<IQuoteSnapshotSource> QuoteSnapshots { get; }

    /// <summary>全部可探活的数据源，用于 <c>--probe</c> 与后台监控。</summary>
    public IReadOnlyList<IProbeable> All { get; }

    /// <summary>
    /// 按降级顺序取多标的快照：主源失败即切备源，并返回实际命中的源名。
    /// </summary>
    /// <param name="codes">证券代码集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<(string Source, IReadOnlyList<QuoteRow> Rows)?> GetQuotesWithFallbackAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken = default)
    {
        foreach (var source in QuoteSnapshots)
        {
            try
            {
                var rows = await source.GetQuotesAsync(codes, cancellationToken).ConfigureAwait(false);
                if (rows.Count > 0)
                {
                    return (source.Name, rows);
                }

                _logger.LogDebug("{Source} 未返回快照（{Count} 只），尝试下一个源", source.Name, codes.Count);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "{Source} 取快照失败，降级到下一个源", source.Name);
            }
        }

        return null;
    }

    /// <summary>
    /// 按降级顺序扫描全市场列表，返回实际命中的源名。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="GetQuotesWithFallbackAsync"/> 同一写法。命中备源时返回的行数会明显少于
    /// 主源（新浪 5,564 只 vs 东财 5,917 只，且新浪不含 <c>f100</c> 行业），
    /// 因此调用方必须按 <c>Source</c> 决定是否清理/保留行业字段，不能当作等价数据。
    /// </remarks>
    /// <param name="onPage">每页回调。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<(string Source, MarketListResult Result)?> GetMarketListWithFallbackAsync(
        Func<IReadOnlyList<MarketListRow>, Task>? onPage = null,
        CancellationToken cancellationToken = default)
    {
        foreach (var source in MarketLists)
        {
            try
            {
                var result = await source.GetMarketListAsync(onPage, cancellationToken).ConfigureAwait(false);
                if (result.Rows > 0)
                {
                    return (source.Name, result);
                }

                _logger.LogWarning("{Source} 未返回任何列表行，尝试下一个源", source.Name);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "{Source} 扫描全市场列表失败，降级到下一个源", source.Name);
            }
        }

        return null;
    }

    /// <summary>
    /// 按降级顺序取个股日线，返回实际命中的源名。
    /// </summary>
    /// <param name="code">证券代码（含指数与板块码）。</param>
    /// <param name="from">起始日期（含）。</param>
    /// <param name="to">结束日期（含）。</param>
    /// <param name="adjust">复权口径。</param>
    /// <param name="period">周期（101 日 / 102 周 / 103 月）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<(string Source, IReadOnlyList<DailyBar> Bars)?> GetDailyWithFallbackAsync(
        string code,
        DateOnly from,
        DateOnly to,
        int adjust,
        int period = 101,
        CancellationToken cancellationToken = default) =>
        FallbackAsync(
            Klines,
            source => source.GetDailyAsync(code, from, to, adjust, period, cancellationToken),
            $"{code} 日线",
            cancellationToken);

    /// <summary>
    /// 按降级顺序取板块指数日线，返回实际命中的源名。
    /// </summary>
    /// <param name="sectorCode">板块码（BK 开头）。</param>
    /// <param name="from">起始日期（含）。</param>
    /// <param name="to">结束日期（含）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<(string Source, IReadOnlyList<DailyBar> Bars)?> GetSectorDailyWithFallbackAsync(
        string sectorCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default) =>
        FallbackAsync(
            Klines,
            source => source.GetSectorDailyAsync(sectorCode, from, to, cancellationToken),
            $"{sectorCode} 板块日线",
            cancellationToken);

    /// <summary>
    /// 按降级顺序取交易日历（上证指数日线日期序列），返回实际命中的源名。
    /// </summary>
    /// <remarks>
    /// 交易日历原本直接走东财 <c>push2his</c> 的 K 线端点——与个股 K 线是同一个被拒的端点组，
    /// 因此它必须一起走降级链，否则「K 线恢复不了，日历也跟着一直空」。
    /// </remarks>
    /// <param name="from">起始日期（含）。</param>
    /// <param name="to">结束日期（含）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<(string Source, IReadOnlyList<DateOnly> Days)?> GetTradingDaysWithFallbackAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        foreach (var source in CalendarSources)
        {
            try
            {
                var days = await source.GetTradingDaysAsync(from, to, cancellationToken).ConfigureAwait(false);
                if (days.Count > 0)
                {
                    return (source.Name, days);
                }

                _logger.LogWarning("{Source} 未返回交易日，尝试下一个源", source.Name);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "{Source} 取交易日历失败，降级到下一个源", source.Name);
            }
        }

        return null;
    }

    /// <summary>
    /// K 线类降级链的公共实现：空结果也视为失败并继续降级。
    /// </summary>
    /// <remarks>
    /// 「返回空」与「抛异常」在 K 线上都是失败信号：东财在参数或 <c>secid</c> 不对时
    /// 返回的是<b>空 <c>data</c> 而不是错误</b>，若把空当成功就会静默丢数据。
    /// 但空也可能是真实无数据（停牌 / 新股），因此最后仍返回 null 交由调用方按空处理。
    /// </remarks>
    private async Task<(string Source, IReadOnlyList<DailyBar> Bars)?> FallbackAsync(
        IReadOnlyList<IKlineSource> sources,
        Func<IKlineSource, Task<IReadOnlyList<DailyBar>>> fetch,
        string what,
        CancellationToken cancellationToken)
    {
        foreach (var source in sources)
        {
            try
            {
                var bars = await fetch(source).ConfigureAwait(false);
                if (bars.Count > 0)
                {
                    return (source.Name, bars);
                }

                _logger.LogWarning("{Source} 未返回 {What}，尝试下一个源", source.Name, what);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "{Source} 取 {What} 失败，降级到下一个源", source.Name, what);
            }
        }

        return null;
    }
}
