using SA.Application.Abstractions;
using SA.Application.Analysis;
using SA.Application.Common;
using SA.Contracts.Common;
using SA.Contracts.Stock;
using SA.Contracts.Trend;
using SA.Domain.Common;

namespace SA.Application.Stocks;

/// <summary>
/// 个股用例入口：总览、行情条、数据新鲜度，以及「缺数据时自动入队采集」。
/// </summary>
/// <remarks>
/// <b>按需采集的闭环</b>：接口发现本地没有日线时返回 <c>1003</c> 并调用
/// <see cref="IOnDemandQueue.TryEnqueue"/>，采集调度在下一轮优先处理该标的，
/// 前端按 3 秒 × 5 次重试。这样「第一次打开某只股票」不会永远停在空态。
/// </remarks>
public sealed class StockService(
    OverviewComposer composer,
    TrendAnalyzer trend,
    IDailyHistoryStore daily,
    IIndicatorStore indicators,
    IQuoteSnapshotStore quotes,
    IOnDemandQueue onDemand)
{
    /// <summary>总览（8 张摘要卡）。</summary>
    public Task<ServiceResult<StockOverviewDto>> GetOverviewAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        ComposeWithOnDemandAsync(code, cancellationToken);

    /// <summary>行情条。</summary>
    public Task<ServiceResult<StockProfileDto>> GetProfileAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        composer.GetProfileAsync(code, cancellationToken);

    /// <summary>趋势与价格结构。缺日线时自动入队并返回 1003。</summary>
    public async Task<ServiceResult<TrendDto>> GetTrendAsync(
        string code,
        int limit = 120,
        CancellationToken cancellationToken = default)
    {
        var result = await trend.GetTrendAsync(code, limit, cancellationToken).ConfigureAwait(false);
        if (!result.Ok && result.Error == ErrorCode.DataNotReady)
        {
            onDemand.TryEnqueue(code);
        }

        return result;
    }

    /// <summary>
    /// 数据新鲜度。界面据此显示「日线到哪一天、指标到哪一天、是否正在采集」。
    /// </summary>
    public async Task<ServiceResult<StockFreshnessDto>> GetFreshnessAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var dailyLast = await daily.GetLastDateAsync(code, cancellationToken).ConfigureAwait(false);
        var indicatorLast = await indicators.GetLastDateAsync(code, cancellationToken).ConfigureAwait(false);
        var quote = (await quotes.GetByCodesAsync([code], cancellationToken).ConfigureAwait(false))
            .TryGetValue(code, out var snapshot)
                ? snapshot
                : null;

        // 无日线即视为「采集中」：入队后由调度补，前端据此重试
        var collecting = dailyLast is null;
        if (collecting)
        {
            onDemand.TryEnqueue(code);
        }

        return ServiceResult<StockFreshnessDto>.Success(new StockFreshnessDto(
            Code: code,
            AsOf: quote is null ? null : SaTime.Format(quote.AsOf),
            DailyLastDate: dailyLast is null ? null : SaTime.Format(dailyLast.Value),
            IndicatorLastDate: indicatorLast is null ? null : SaTime.Format(indicatorLast.Value),
            Collecting: collecting));
    }

    private async Task<ServiceResult<StockOverviewDto>> ComposeWithOnDemandAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var result = await composer.ComposeAsync(code, cancellationToken).ConfigureAwait(false);

        // 趋势卡处于采集中说明日线还没到位：入队，让总览页刷新几次后自然变成有数据
        var trendCard = result.Value?.Modules.FirstOrDefault(m => m.Key == "trend");
        if (trendCard is { Status: "collecting" })
        {
            onDemand.TryEnqueue(code);
        }

        return result;
    }
}
