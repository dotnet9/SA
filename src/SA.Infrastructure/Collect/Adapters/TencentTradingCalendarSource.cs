using System.Diagnostics;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 交易日历的腾讯实现：上证指数日线的日期序列。
/// </summary>
/// <remarks>
/// <para>
/// 交易日历原本直接走东财 <c>push2his …/kline/get</c>——与个股 K 线<b>是同一个被拒的端点组</b>
/// （实测 2026-09-21 连接被重置）。因此日历必须和 K 线一起走降级链，
/// 否则会出现「K 线已经能取到，日历却一直空」的割裂状态。
/// </para>
/// <para>
/// 实现上不重复解析逻辑：直接委托给 <see cref="TencentKlineSource"/> 取上证指数
/// （<c>000001</c> 在 <c>MarketCodes</c> 里登记为 <c>1.000001</c>），只投影日期。
/// 指数只在交易日产生 K 线，因此日期集合就是交易日历。
/// </para>
/// </remarks>
public sealed class TencentTradingCalendarSource(TencentKlineSource klines) : ITradingCalendarSource
{
    private const string IndexCode = "000001";

    /// <inheritdoc />
    public string Name => "腾讯财经 · 指数日线（交易日历）";

    /// <inheritdoc />
    public string Domains => "日历,降级";

    /// <inheritdoc />
    public async Task<IReadOnlyList<DateOnly>> GetTradingDaysAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var bars = await klines
            .GetDailyAsync(IndexCode, from, to, TencentKlineSource.AdjustNone, TencentKlineSource.PeriodDaily, cancellationToken)
            .ConfigureAwait(false);

        return bars.Select(bar => bar.Date).Distinct().OrderBy(date => date).ToList();
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var to = SaTime.Today;
            var days = await GetTradingDaysAsync(to.AddDays(-30), to, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return days.Count > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, days.Count)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "近 30 天无交易日");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }
}
