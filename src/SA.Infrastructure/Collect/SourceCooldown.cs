using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Collect;

namespace SA.Infrastructure.Collect;

/// <summary>
/// 数据源冷却闸门：连续失败达到阈值后，在一段时间内直接跳过该源的任务。
/// </summary>
/// <remarks>
/// <para>
/// 实施计划 §5.4 要求「连续失败达阈值自动冷却并标记数据源降级」，本条是冷却的执行者。
/// </para>
/// <para>
/// <b>实测背景</b>（2026-09-20）：东财 <c>push2.eastmoney.com</c> 在被连续请求约 60 次（一轮全市场
/// 扫描的规模）后会开始返回截断响应（<c>HttpIOException: The response ended prematurely</c>），
/// 同一时刻 <c>datacenter-web</c> 与 <c>push2his</c> 仍然正常。此时若调度继续每 60 秒重试一轮，
/// 只会延长被限流的时间；冷却窗口让上游有机会恢复，也让失败日志保持可读。
/// </para>
/// </remarks>
public sealed class SourceCooldown(ICollectStatusStore status, CollectOptions options, ILogger<SourceCooldown> logger)
{
    /// <summary>
    /// 判断某个数据源是否处于冷却期。
    /// </summary>
    /// <param name="sourceName">数据源名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<bool> IsCoolingDownAsync(string sourceName, CancellationToken cancellationToken = default)
    {
        var cooldownMinutes = options.CooldownMinutes;
        if (cooldownMinutes <= 0)
        {
            return false;
        }

        var sources = await status.GetSourcesAsync(cancellationToken).ConfigureAwait(false);
        var row = sources.FirstOrDefault(s => string.Equals(s.Name, sourceName, StringComparison.Ordinal));
        if (row is null || row.Status != DataSourceStates.Err || row.FailCount < options.DegradeAfterFailures)
        {
            return false;
        }

        var elapsed = SaTime.Now - row.UpdatedAt;
        if (elapsed >= TimeSpan.FromMinutes(cooldownMinutes))
        {
            return false;
        }

        logger.LogWarning(
            "{Source} 处于冷却期（连续失败 {FailCount} 次，剩余约 {Remaining:F1} 分钟），本轮跳过",
            sourceName, row.FailCount, (TimeSpan.FromMinutes(cooldownMinutes) - elapsed).TotalMinutes);
        return true;
    }
}
