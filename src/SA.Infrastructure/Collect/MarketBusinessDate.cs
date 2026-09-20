using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Domain.Common;

namespace SA.Infrastructure.Collect;

/// <summary>
/// 全市场数据的业务日期解析。
/// </summary>
/// <remarks>
/// 行情列表与板块列表端点都<b>不返回日期</b>，只有指数快照带时间戳（上游 <c>f124</c>）。
/// 若用本机日期落库，周末与节假日就会把上一个交易日的收盘数据标成当天数据，
/// 直接违反实施计划 §10 的「快照沿用上一收盘值并标注时间」。
/// 因此凡是要给「某日全市场数据」打时间戳的任务，都必须经过这里取日期。
/// </remarks>
public sealed class MarketBusinessDate(IIndexStore indexStore, ILogger<MarketBusinessDate> logger)
{
    /// <summary>
    /// 取当前业务日期；指数快照尚未采集时返回 null（调用方决定如何降级）。
    /// </summary>
    public async Task<DateOnly?> TryResolveAsync(CancellationToken cancellationToken = default)
    {
        var indices = await indexStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (indices.Count == 0)
        {
            return null;
        }

        var max = indices.Max(i => i.AsOf);
        return max == DateOnly.MinValue ? null : max;
    }

    /// <summary>
    /// 取当前业务日期，取不到时退化为本机业务日并记录警告。
    /// </summary>
    public async Task<DateOnly> ResolveOrTodayAsync(string taskName, CancellationToken cancellationToken = default)
    {
        var resolved = await TryResolveAsync(cancellationToken).ConfigureAwait(false);
        if (resolved is not null)
        {
            return resolved.Value;
        }

        logger.LogWarning(
            "{Task}：指数快照尚未采集，业务日期退化为本机业务日 {Today:yyyy-MM-dd}",
            taskName, SaTime.Today);
        return SaTime.Today;
    }
}
