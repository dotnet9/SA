using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Domain.Entities.Finance;
using SA.Infrastructure.Collect.Registry;

namespace SA.Infrastructure.Collect.Jobs;

/// <summary>
/// 财务报表采集任务：拉取业绩报表与业绩预告并写库。
/// </summary>
/// <remarks>
/// 财务数据是季频的，单个标的的数据量很小（20 期左右），因此：
/// <list type="bullet">
/// <item>按需触发为主（用户打开财务页时若缺数据即入队，由调度优先处理）；</item>
/// <item>全量回补时只需在「日线补完」的标的上顺带跑一次，不需要独立的轮询节奏；</item>
/// <item>同一报告期重复采集即覆盖，因此重跑安全。</item>
/// </list>
/// </remarks>
public sealed class FinanceJob(
    SourceRegistry registry,
    CollectExecutor executor,
    IFinanceStore store,
    ILogger<FinanceJob> logger)
{
    /// <summary>任务名。</summary>
    public const string TaskName = "finance-report";

    /// <summary>单标的拉取的报告期数上限（约 5 年）。</summary>
    public const int ReportLimit = 24;

    /// <summary>
    /// 采集一个标的的财务数据。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>写入的报告期数；失败或无数据返回 0。</returns>
    public async Task<int> RunAsync(string code, CancellationToken cancellationToken = default)
    {
        var reports = new List<FinancialReport>();
        var forecasts = new List<EarningsForecast>();

        var result = await executor.ExecuteAsync(
            $"{TaskName}:{code}",
            registry.Finance,
            "主源",
            async ct =>
            {
                reports = (await registry.Finance.GetReportsAsync(code, ReportLimit, ct).ConfigureAwait(false)).ToList();
                forecasts = (await registry.Finance.GetForecastsAsync(code, 8, ct).ConfigureAwait(false)).ToList();
                return (reports.Count, reports.Count);
            },
            cancellationToken).ConfigureAwait(false);

        if (result == 0 || reports.Count == 0)
        {
            // 新股可能暂无财报：这不是错误，记一条日志即可，不写失败游标
            logger.LogDebug("{Code} 暂无财报数据", code);
            return 0;
        }

        var written = await store.UpsertReportsAsync(reports, cancellationToken).ConfigureAwait(false);
        if (forecasts.Count > 0)
        {
            await store.UpsertForecastsAsync(forecasts, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("{Code} 财务数据已更新：{Periods} 期，最新 {Latest:yyyy-MM-dd}", code, written, reports[^1].ReportDate);
        return written;
    }
}
