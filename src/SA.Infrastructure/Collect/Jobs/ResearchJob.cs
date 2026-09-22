using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Infrastructure.Collect.Registry;

namespace SA.Infrastructure.Collect.Jobs;

/// <summary>
/// 个股研究数据采集任务：主营构成 / 股本结构 / 限售解禁 / 公告 / 研报。
/// </summary>
/// <remarks>
/// <para>
/// 全部是「按代码、低频、按需」的数据：主营构成与股本结构随定期报告更新，
/// 公告与研报按天更新。因此它挂在<b>按需采集</b>上（用户打开个股页时补齐），
/// 不进全市场轮询——全市场 5,832 只各拉 4 个端点就是 2.3 万次请求，而其中绝大多数不会被查看。
/// </para>
/// <para>
/// 五个步骤各自独立 try（由调用方的 <c>RunStepAsync</c> 保证）：
/// 公告接口抖动不应该让已经拿到的股本结构一起作废。
/// </para>
/// </remarks>
public sealed class ResearchJob(
    SourceRegistry registry,
    CollectExecutor executor,
    IResearchStore store,
    ILogger<ResearchJob> logger)
{
    /// <summary>任务名前缀。</summary>
    public const string TaskName = "research";

    /// <summary>公告拉取条数（实测东芯 892 条，取近期 60 条足够展示）。</summary>
    public const int AnnouncementLimit = 60;

    /// <summary>研报拉取篇数。</summary>
    public const int ReportLimit = 30;

    /// <summary>
    /// 采集一个标的的研究数据。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>写入的总行数；全部失败或无数据返回 0。</returns>
    public async Task<int> RunAsync(string code, CancellationToken cancellationToken = default)
    {
        var written = 0;

        written += await RunBusinessAsync(code, cancellationToken).ConfigureAwait(false);
        written += await RunCapitalAsync(code, cancellationToken).ConfigureAwait(false);
        written += await RunAnnouncementsAsync(code, cancellationToken).ConfigureAwait(false);
        written += await RunReportsAsync(code, cancellationToken).ConfigureAwait(false);

        if (written > 0)
        {
            logger.LogInformation("{Code} 研究数据已更新：{Rows} 行", code, written);
        }

        return written;
    }

    /// <summary>主营构成 + 业务范围 + 经营评述。</summary>
    private async Task<int> RunBusinessAsync(string code, CancellationToken cancellationToken)
    {
        var result = await executor.ExecuteAsync(
            $"{TaskName}-business:{code}",
            registry.BusinessComposition,
            "主源",
            async ct =>
            {
                var data = await registry.BusinessComposition.GetAsync(code, ct).ConfigureAwait(false);

                var rows = await store.UpsertProfileAsync(
                    new Domain.Entities.Research.BusinessProfile
                    {
                        Code = code,
                        BusinessScope = data.BusinessScope,
                        BusinessReview = data.BusinessReview,
                        UpdatedAt = Domain.Common.SaTime.Now
                    },
                    ct).ConfigureAwait(false);

                if (data.Items.Count > 0)
                {
                    rows += await store.UpsertCompositionsAsync(data.Items, ct).ConfigureAwait(false);
                }

                return (rows, data.Items.Count);
            },
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <summary>股本变动历史 + 限售解禁。</summary>
    private async Task<int> RunCapitalAsync(string code, CancellationToken cancellationToken)
    {
        var result = await executor.ExecuteAsync(
            $"{TaskName}-capital:{code}",
            registry.CapitalStructure,
            "主源",
            async ct =>
            {
                var data = await registry.CapitalStructure.GetAsync(code, ct).ConfigureAwait(false);

                var rows = 0;
                if (data.ShareChanges.Count > 0)
                {
                    rows += await store.UpsertShareChangesAsync(data.ShareChanges, ct).ConfigureAwait(false);
                }

                // 整体替换：上游 xsjj 给的是「当前剩余的待解禁」，已过期的会被上游移除。
                // 空列表也要执行替换，否则旧的待解禁会一直留在库里被当成未来解禁。
                rows += await store.ReplaceUpcomingUnlocksAsync(code, data.UpcomingUnlocks, ct).ConfigureAwait(false);

                return (rows, data.ShareChanges.Count);
            },
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <summary>公告列表。</summary>
    private async Task<int> RunAnnouncementsAsync(string code, CancellationToken cancellationToken)
    {
        var result = await executor.ExecuteAsync(
            $"{TaskName}-announcement:{code}",
            registry.Announcements,
            "主源",
            async ct =>
            {
                var rows = await registry.Announcements.GetAsync(code, AnnouncementLimit, ct).ConfigureAwait(false);
                if (rows.Count == 0)
                {
                    return (0, 0);
                }

                var written = await store.UpsertAnnouncementsAsync(rows, ct).ConfigureAwait(false);
                return (written, rows.Count);
            },
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <summary>研报列表。</summary>
    private async Task<int> RunReportsAsync(string code, CancellationToken cancellationToken)
    {
        var result = await executor.ExecuteAsync(
            $"{TaskName}-report:{code}",
            registry.ResearchReports,
            "主源",
            async ct =>
            {
                var rows = await registry.ResearchReports.GetAsync(code, ReportLimit, ct).ConfigureAwait(false);
                if (rows.Count == 0)
                {
                    // 没有研报覆盖是合法状态（小盘股常见），不是故障
                    return (0, 0);
                }

                var written = await store.UpsertReportsAsync(rows, ct).ConfigureAwait(false);
                return (written, rows.Count);
            },
            cancellationToken).ConfigureAwait(false);

        return result;
    }
}
