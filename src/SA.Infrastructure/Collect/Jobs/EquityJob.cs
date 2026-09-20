using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Infrastructure.Collect.Registry;

namespace SA.Infrastructure.Collect.Jobs;

/// <summary>
/// 股权结构采集任务：十大股东 / 十大流通股东 / 股东户数 / 股权质押。
/// </summary>
/// <remarks>
/// 与财务任务同属「季频、体量小、随用户访问补齐」的一类：
/// 按需优先，回补时在日线补完的标的上顺带跑一次。
/// 三个数据集各自容错（见 <see cref="CollectExecutor"/> 的独立执行），
/// 其中一个报表拿不到不会让整页无数据。
/// </remarks>
public sealed class EquityJob(
    SourceRegistry registry,
    CollectExecutor executor,
    IEquityStore store,
    ILogger<EquityJob> logger)
{
    /// <summary>任务名。</summary>
    public const string TaskName = "equity-structure";

    /// <summary>取多少期十大股东（一期含全量与流通两组）。</summary>
    public const int HolderPeriods = 2;

    /// <summary>取多少期股东户数。</summary>
    public const int HolderCountPeriods = 16;

    /// <summary>
    /// 采集一个标的的股权结构。
    /// </summary>
    /// <returns>写入的数据项数；失败或无数据返回 0。</returns>
    public async Task<int> RunAsync(string code, CancellationToken cancellationToken = default)
    {
        var written = 0;

        var holders = await executor.ExecuteAsync(
            $"{TaskName}-holders:{code}",
            registry.Equity,
            "主源",
            async ct =>
            {
                var rows = await registry.Equity.GetTopHoldersAsync(code, HolderPeriods, ct).ConfigureAwait(false);
                if (rows.Count == 0)
                {
                    return (0, 0);
                }

                var count = await store.UpsertTopHoldersAsync(rows, ct).ConfigureAwait(false);
                return (count, count);
            },
            cancellationToken).ConfigureAwait(false);
        written += holders;

        var counts = await executor.ExecuteAsync(
            $"{TaskName}-holdernum:{code}",
            registry.Equity,
            "主源",
            async ct =>
            {
                var rows = await registry.Equity.GetHolderCountsAsync(code, HolderCountPeriods, ct).ConfigureAwait(false);
                if (rows.Count == 0)
                {
                    return (0, 0);
                }

                var count = await store.UpsertHolderCountsAsync(rows, ct).ConfigureAwait(false);
                return (count, count);
            },
            cancellationToken).ConfigureAwait(false);
        written += counts;

        var pledge = await executor.ExecuteAsync(
            $"{TaskName}-pledge:{code}",
            registry.Equity,
            "主源",
            async ct =>
            {
                var row = await registry.Equity.GetPledgeAsync(code, ct).ConfigureAwait(false);
                if (row is null)
                {
                    return (0, 0);
                }

                var count = await store.UpsertPledgeAsync(row, ct).ConfigureAwait(false);
                return (count, count);
            },
            cancellationToken).ConfigureAwait(false);
        written += pledge;

        if (written > 0)
        {
            logger.LogInformation(
                "{Code} 股权结构已更新：股东 {Holders} 行、户数 {Counts} 期、质押 {Pledge} 行",
                code, holders, counts, pledge);
        }
        else
        {
            logger.LogDebug("{Code} 暂无股权结构数据", code);
        }

        return written;
    }
}
