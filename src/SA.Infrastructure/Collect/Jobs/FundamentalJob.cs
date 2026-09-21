using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Finance;
using SA.Infrastructure.Collect.Registry;

namespace SA.Infrastructure.Collect.Jobs;

/// <summary>
/// 基本面指标采集任务（实施计划 §5.1）。
/// </summary>
/// <remarks>
/// <para>
/// 两条路径共用同一张表（主键 <c>(Code, ReportDate)</c>，历史序列与全市场快照天然共存）：
/// </para>
/// <list type="number">
/// <item><b>全市场</b>（<see cref="RunMarketAsync"/>）：按「最新已披露报告期」扫一遍，
/// 实测 27 页 / 5,832 只 A 股，是选股器的数据基础；</item>
/// <item><b>单只历史序列</b>（<see cref="RunHistoryAsync"/>）：用户按需关注某只标的时拉近 8–10 年报告期
/// （实测东芯股份 29 个报告期可一次拿到），支撑「周期高点还是起点」的判断。</item>
/// </list>
/// <para>
/// 之所以不需要第二张表：主键含报告期，历史序列只是同一张表里同一代码的更多行。
/// </para>
/// </remarks>
public sealed class FundamentalJob(
    SourceRegistry registry,
    CollectExecutor executor,
    IFundamentalStore store,
    ILogger<FundamentalJob> logger)
{
    /// <summary>全市场扫描的任务名。</summary>
    public const string MarketTaskName = "fundamental-market";

    /// <summary>单只历史序列的任务名。</summary>
    public const string HistoryTaskName = "fundamental-history";

    /// <summary>
    /// 单只标的拉取的报告期数上限（约 10 年，含季报）。
    /// </summary>
    /// <remarks>
    /// 实测东芯股份全部历史只有 29 期（2017 年起），40 期足够覆盖绝大多数标的的十年，
    /// 又不会因为老公司（如 1990 年代上市）把一次请求拉成几百期。
    /// </remarks>
    public const int HistoryPeriods = 40;

    /// <summary>
    /// 全市场扫描：取最新已披露报告期并落库。
    /// </summary>
    /// <remarks>
    /// 已有该报告期数据时<b>仍然重扫</b>：披露季里同一个报告期会持续有新的标的补披露，
    /// 而主键是 upsert，重扫是幂等的。真正的重复扫描成本（27 次请求）由调度间隔控制。
    /// </remarks>
    /// <returns>写入行数；失败或上游无数据返回 0。</returns>
    public async Task<int> RunMarketAsync(CancellationToken cancellationToken = default)
    {
        var written = 0;
        DateOnly? reportDate = null;

        var result = await executor.ExecuteAsync(
            MarketTaskName,
            registry.Fundamental,
            "主源",
            async ct =>
            {
                reportDate = await registry.Fundamental.GetLatestReportDateAsync(ct).ConfigureAwait(false);
                if (reportDate is null)
                {
                    return (0, 0);
                }

                var (rows, total) = await registry.Fundamental.GetByReportDateAsync(
                    reportDate.Value,
                    async page =>
                    {
                        // 边拉边写：一个报告期 27 页 / 约 1.3 万行，不必全部驻留内存
                        written += await store.UpsertAsync(page, ct).ConfigureAwait(false);
                    },
                    ct).ConfigureAwait(false);

                return (rows, total);
            },
            cancellationToken).ConfigureAwait(false);

        if (result == 0 || written == 0)
        {
            logger.LogWarning("全市场基本面未采集到数据（报告期 {ReportDate}）", reportDate);
            return 0;
        }

        logger.LogInformation(
            "全市场基本面已更新：报告期 {ReportDate:yyyy-MM-dd}，写入 {Written} 行",
            reportDate, written);
        return written;
    }

    /// <summary>
    /// 采集单只标的的历史报告期序列。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>写入行数；失败或无数据返回 0。</returns>
    public async Task<int> RunHistoryAsync(string code, CancellationToken cancellationToken = default)
    {
        var metrics = new List<FundamentalMetric>();

        var result = await executor.ExecuteAsync(
            $"{HistoryTaskName}:{code}",
            registry.Fundamental,
            "主源",
            async ct =>
            {
                metrics = (await registry.Fundamental
                    .GetHistoryAsync(code, HistoryPeriods, ct)
                    .ConfigureAwait(false)).ToList();
                return (metrics.Count, metrics.Count);
            },
            cancellationToken).ConfigureAwait(false);

        if (result == 0 || metrics.Count == 0)
        {
            // 新股 / 次新可能暂无财报：这是合法状态，不是故障
            logger.LogDebug("{Code} 暂无基本面历史数据", code);
            return 0;
        }

        var written = await store.UpsertAsync(metrics, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "{Code} 基本面历史已更新：{Periods} 期，最新 {Latest:yyyy-MM-dd}",
            code, written, metrics[^1].ReportDate);
        return written;
    }
}
