using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Infrastructure.Collect.Adapters;
using SA.Infrastructure.Collect.Registry;

namespace SA.Infrastructure.Collect.Jobs;

/// <summary>
/// 行业指数日线任务：为景气度与传导带宽提供行业指数序列（<c>90.BKxxxx</c>）。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么只补一部分行业</b>：东财行业板块约 500 个，全量日线一年是 500 次请求；
/// 而景气度排行看的是「资金与广度最强的行业」，带宽看的是「用户正在看的那只股票所属行业」。
/// 因此这里补两类：<b>当前资金流入/流出最显著的 N 个行业</b>（价格与资金都活跃，最可能被查阅）
/// 与<b>按需触发的行业</b>（用户打开个股页时由调度顺带补齐其行业）。
/// </para>
/// <para>
/// 数据落在既有日线仓（<c>parquet/daily/0/BKxxxx.parquet</c>），因为行业指数与个股日线结构完全相同；
/// 另起一套存储只会让「按代码取日线」这件事出现两条路径。
/// </para>
/// </remarks>
public sealed class SectorKlineJob(
    SourceRegistry registry,
    CollectExecutor executor,
    IDailyHistoryStore daily,
    ISectorStore sectors,
    CollectOptions options,
    ILogger<SectorKlineJob> logger)
{
    /// <summary>任务名。</summary>
    public const string TaskName = "sector-kline";

    /// <summary>单次补齐的行业数量上限（默认 30，可用 <c>BackfillPriorityCodes</c> 的一半调节）。</summary>
    public const int DefaultSectorCount = 30;

    /// <summary>回看天数：带宽需要 60 日，取 400 天以便同时支持长周期分位。</summary>
    public const int LookbackDays = 400;

    /// <summary>
    /// 按资金与涨跌幅显著度补齐行业指数日线。
    /// </summary>
    /// <returns>成功补齐的行业数。</returns>
    public async Task<int> RunPriorityAsync(CancellationToken cancellationToken = default)
    {
        var all = await sectors.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (all.Count == 0)
        {
            logger.LogDebug("行业板块快照尚未就绪，跳过行业日线补齐");
            return 0;
        }

        // 资金绝对值最大的行业优先：它们同时是景气度排行的头部与用户最可能点开的对象
        var targets = all
            .OrderByDescending(sector => Math.Abs(sector.MainNet))
            .Take(Math.Max(1, Math.Min(DefaultSectorCount, options.BackfillPriorityCodes)))
            .Select(sector => sector.Code)
            .ToList();

        var succeeded = 0;
        foreach (var code in targets)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (await RunAsync(code, cancellationToken).ConfigureAwait(false) > 0)
            {
                succeeded++;
            }
        }

        logger.LogInformation("行业指数日线补齐完成：{Succeeded}/{Total} 个行业", succeeded, targets.Count);
        return succeeded;
    }

    /// <summary>
    /// 补齐单个行业的指数日线（增量）。
    /// </summary>
    /// <param name="sectorCode">板块码（BK 开头）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>写入行数；无新数据返回 0。</returns>
    public async Task<int> RunAsync(string sectorCode, CancellationToken cancellationToken = default)
    {
        if (!MarketCodes.IsSectorCode(sectorCode))
        {
            return 0;
        }

        var today = SaTime.Today;
        var last = await daily.GetLastDateAsync(sectorCode, cancellationToken).ConfigureAwait(false);
        var from = last is null ? today.AddDays(-LookbackDays) : last.Value.AddDays(-5);

        if (from > today)
        {
            return 0;
        }

        var bars = new List<Domain.History.DailyBar>();

        var result = await executor.ExecuteAsync(
            $"{TaskName}:{sectorCode}",
            registry.Klines[0],
            "主源",
            async ct =>
            {
                // 板块日线只有东财提供（腾讯不支持板块码，会抛异常让链条继续往下走）
                var hit = await registry
                    .GetSectorDailyWithFallbackAsync(sectorCode, from, today, ct)
                    .ConfigureAwait(false);

                if (hit is null)
                {
                    return (0, 0);
                }

                logger.LogInformation("{Sector} 板块日线命中数据源：{Source}", sectorCode, hit.Value.Source);
                bars = hit.Value.Bars.ToList();
                return (bars.Count, bars.Count);
            },
            cancellationToken).ConfigureAwait(false);

        if (result == 0 || bars.Count == 0)
        {
            return 0;
        }

        return await daily.MergeAsync(sectorCode, bars, cancellationToken).ConfigureAwait(false);
    }
}
