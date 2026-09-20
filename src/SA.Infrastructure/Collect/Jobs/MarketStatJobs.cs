using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Collect;
using SA.Infrastructure.Collect;
using SA.Infrastructure.Collect.Registry;

namespace SA.Infrastructure.Collect.Jobs;

/// <summary>
/// 市场级统计任务：涨跌停家数、两市资金分层、两融余额。
/// </summary>
/// <remarks>
/// <para>
/// 三个数据来自三个独立端点，披露时间也各不相同（两融最晚，实测最新交易日往往只有沪市），
/// 因此分三次调用执行外壳：一次失败不影响另外两项，且失败会精确记到对应的数据源上。
/// </para>
/// <para>
/// 三项结果都写到<b>同一个业务日</b>的行上（业务日取指数口径日），
/// 各自的口径日期另行记录，避免把 09-17 的两融数据说成 09-18 的。
/// </para>
/// </remarks>
public sealed class MarketStatJob(
    SourceRegistry registry,
    CollectExecutor executor,
    IIndexStore indexStore,
    IMarketStatStore stats,
    ILogger<MarketStatJob> logger)
{
    /// <summary>任务名。</summary>
    public const string TaskName = "market-stat";

    /// <summary>执行一次。</summary>
    /// <returns>成功写入的数据项数（0–3）。</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var businessDay = await ResolveBusinessDateAsync(cancellationToken).ConfigureAwait(false);
        var written = 0;

        written += await RunLimitPoolsAsync(businessDay, cancellationToken).ConfigureAwait(false);
        written += await RunFundFlowAsync(businessDay, cancellationToken).ConfigureAwait(false);
        written += await RunMarginAsync(businessDay, cancellationToken).ConfigureAwait(false);

        return written;
    }

    /// <summary>涨跌停家数（交易所口径，避免用涨跌幅阈值反推）。</summary>
    private async Task<int> RunLimitPoolsAsync(DateOnly businessDay, CancellationToken cancellationToken)
    {
        var result = await executor.ExecuteAsync(
            $"{TaskName}-limit-pool",
            registry.LimitPools,
            "主源",
            async ct =>
            {
                var pools = await registry.LimitPools.GetLimitPoolsAsync(businessDay, ct).ConfigureAwait(false);
                if (pools.Date == default)
                {
                    throw new InvalidOperationException("涨跌停端点未返回口径日期");
                }

                if (pools.Date != businessDay)
                {
                    logger.LogWarning(
                        "涨跌停口径日 {PoolDate:yyyy-MM-dd} 与业务日 {BusinessDay:yyyy-MM-dd} 不一致，按业务日归档",
                        pools.Date, businessDay);
                }

                await stats.UpsertAsync(
                    new MarketStatPatch(businessDay, LimitUp: pools.LimitUp, LimitDown: pools.LimitDown),
                    ct).ConfigureAwait(false);

                return (1, pools.LimitUp + pools.LimitDown);
            },
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <summary>两市资金分层（沪 + 深按层相加）。</summary>
    private async Task<int> RunFundFlowAsync(DateOnly businessDay, CancellationToken cancellationToken)
    {
        var result = await executor.ExecuteAsync(
            $"{TaskName}-fundflow",
            registry.MarketFundFlow,
            "主源",
            async ct =>
            {
                var flow = await registry.MarketFundFlow.GetLatestAsync(ct).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("大盘资金流未返回数据");

                await stats.UpsertAsync(
                    new MarketStatPatch(businessDay, FundFlow: flow, FundFlowDate: flow.Date),
                    ct).ConfigureAwait(false);

                return (1, 2);
            },
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <summary>两融余额（沪深两市均已披露的最近交易日）。</summary>
    private async Task<int> RunMarginAsync(DateOnly businessDay, CancellationToken cancellationToken)
    {
        var result = await executor.ExecuteAsync(
            $"{TaskName}-margin",
            registry.Margin,
            "主源",
            async ct =>
            {
                var margin = await registry.Margin.GetLatestAsync(ct).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("两融端点未取到沪深齐备的交易日");

                await stats.UpsertAsync(
                    new MarketStatPatch(businessDay, Margin: margin, MarginDate: margin.Date),
                    ct).ConfigureAwait(false);

                return (1, 1);
            },
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <summary>业务日取指数口径日；指数尚未采集时退化为本机业务日。</summary>
    private async Task<DateOnly> ResolveBusinessDateAsync(CancellationToken cancellationToken)
    {
        var indices = await indexStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return indices.Count > 0 && indices.Max(i => i.AsOf) != DateOnly.MinValue
            ? indices.Max(i => i.AsOf)
            : SaTime.Today;
    }
}

/// <summary>
/// 交易日历任务：由上证指数日线的日期序列推导交易日，写入 <c>TradingDay</c>。
/// </summary>
/// <remarks>
/// 回看窗口默认 1 年，足够支撑「上一个交易日 / 近 N 日」这类口径，也避免一次拉取过长历史。
/// </remarks>
public sealed class TradingCalendarJob(
    SourceRegistry registry,
    CollectExecutor executor,
    ITradingCalendarStore store,
    ILogger<TradingCalendarJob> logger)
{
    /// <summary>任务名。</summary>
    public const string TaskName = "trading-calendar";

    /// <summary>回看天数。</summary>
    public const int LookbackDays = 400;

    /// <summary>执行一次。</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var to = SaTime.Today;
        var from = to.AddDays(-LookbackDays);

        var result = await executor.ExecuteAsync(
            TaskName,
            registry.Calendar,
            "主源",
            async ct =>
            {
                var days = await registry.Calendar.GetTradingDaysAsync(from, to, ct).ConfigureAwait(false);

                // 区间内全部日期先标记为休市，再把实际有日线的日期标为交易日：
                // 这样「某天不是交易日」也有明确记录，不必靠「查不到」推断
                var open = days.ToHashSet();
                var calendar = new List<TradingDay>();
                for (var day = from; day <= to; day = day.AddDays(1))
                {
                    calendar.Add(new TradingDay { Date = day, IsOpen = open.Contains(day) });
                }

                await store.UpsertAsync(calendar, ct).ConfigureAwait(false);
                return (calendar.Count, days.Count);
            },
            cancellationToken).ConfigureAwait(false);

        if (result > 0)
        {
            logger.LogInformation("交易日历已更新：区间 {Days} 天", result);
        }

        return result;
    }
}
