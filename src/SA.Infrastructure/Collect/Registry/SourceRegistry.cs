using SA.Application.Abstractions;

namespace SA.Infrastructure.Collect.Registry;

/// <summary>
/// 数据源注册表：集中声明每个域的主源与备源顺序，供采集任务与探活使用。
/// </summary>
/// <remarks>
/// 降级链在原型与详细设计 §6.2 中写作「腾讯 → 新浪 → 东方财富」，但实施计划 §3.1 的实测结论是
/// <b>东财为主源</b>（字段最全、覆盖最广），腾讯为快照类备源，新浪需 <c>Referer</c> 且本轮不作为
/// 默认启用项。因此这里的顺序是「东财优先、腾讯兜底」，并在数据源状态中显式标注类型，
/// 避免界面把备源数据当成主源数据展示。
/// </remarks>
public sealed class SourceRegistry
{
    /// <summary>
    /// 构造注册表。
    /// </summary>
    public SourceRegistry(
        IMarketListSource marketList,
        IIndexSource indices,
        ISectorSource sectors,
        ILimitPoolSource limitPools,
        IMarginMarketSource margin,
        IMarketFundFlowSource marketFundFlow,
        ITradingCalendarSource calendar,
        IKlineSource kline,
        IEnumerable<IQuoteSnapshotSource> quoteSnapshots,
        IEnumerable<IProbeable> allSources)
    {
        MarketList = marketList;
        Indices = indices;
        Sectors = sectors;
        LimitPools = limitPools;
        Margin = margin;
        MarketFundFlow = marketFundFlow;
        Calendar = calendar;
        Kline = kline;

        // 注册顺序即降级顺序：主源在前
        QuoteSnapshots = quoteSnapshots.ToList();
        All = allSources.Distinct().ToList();
    }

    /// <summary>全市场列表源（股票池 + 全市场快照）。</summary>
    public IMarketListSource MarketList { get; }

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

    /// <summary>交易日历源。</summary>
    public ITradingCalendarSource Calendar { get; }

    /// <summary>K 线源（日 / 周 / 月，指定复权口径）。</summary>
    public IKlineSource Kline { get; }

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
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // 降级到下一个源；失败明细由调用方的 CollectExecutor 记录，这里不重复记
            }
        }

        return null;
    }
}
