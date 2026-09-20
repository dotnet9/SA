using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Infrastructure.Collect.Registry;

namespace SA.Infrastructure.Collect.Jobs;

/// <summary>
/// 资金面采集任务：个股资金流、龙虎榜、大宗交易、两融明细、陆股通持股。
/// </summary>
/// <remarks>
/// 五个数据集频率差异很大（资金流逐日、两融逐日、陆股通季频、龙虎榜按事件、大宗交易按事件），
/// 因此各自独立执行：某一路不可用不影响其余区块，页面按区块降级。
/// 整体属于「体量小、按需优先」的一类，与财务、股权任务同样挂在按需采集与回补流程上。
/// </remarks>
public sealed class CapitalJob(
    SourceRegistry registry,
    CollectExecutor executor,
    ICapitalStore store,
    ILogger<CapitalJob> logger)
{
    /// <summary>任务名。</summary>
    public const string TaskName = "capital";

    /// <summary>资金流回看交易日数。</summary>
    public const int FundFlowDays = 60;

    /// <summary>两融回看交易日数。</summary>
    public const int MarginDays = 30;

    /// <summary>
    /// 采集一个标的的资金面数据。
    /// </summary>
    /// <returns>写入的数据项数；失败或无数据返回 0。</returns>
    public async Task<int> RunAsync(string code, CancellationToken cancellationToken = default)
    {
        // 资金流走的是行情侧主机，与报表不是同一个源，因此单独执行：
        // 行情侧不可用时不会把报表侧的冷却窗口一起触发
        var flows = await executor.ExecuteAsync(
            $"{TaskName}-flow:{code}",
            registry.FundFlow,
            "主源",
            async ct =>
            {
                var rows = await registry.FundFlow.GetFundFlowAsync(code, FundFlowDays, ct).ConfigureAwait(false);
                return rows.Count == 0 ? (0, 0) : (await store.UpsertFundFlowAsync(rows, ct).ConfigureAwait(false), rows.Count);
            },
            cancellationToken).ConfigureAwait(false);

        var margins = await executor.ExecuteAsync(
            $"{TaskName}-margin:{code}",
            registry.Capital,
            "主源",
            async ct =>
            {
                var rows = await registry.Capital.GetMarginDetailsAsync(code, MarginDays, ct).ConfigureAwait(false);
                return rows.Count == 0 ? (0, 0) : (await store.UpsertMarginDetailsAsync(rows, ct).ConfigureAwait(false), rows.Count);
            },
            cancellationToken).ConfigureAwait(false);

        var billboards = await executor.ExecuteAsync(
            $"{TaskName}-billboard:{code}",
            registry.Capital,
            "主源",
            async ct =>
            {
                var rows = await registry.Capital.GetBillboardsAsync(code, 20, ct).ConfigureAwait(false);
                return rows.Count == 0 ? (0, 0) : (await store.UpsertBillboardsAsync(code, rows, ct).ConfigureAwait(false), rows.Count);
            },
            cancellationToken).ConfigureAwait(false);

        var blocks = await executor.ExecuteAsync(
            $"{TaskName}-blocktrade:{code}",
            registry.Capital,
            "主源",
            async ct =>
            {
                var rows = await registry.Capital.GetBlockTradesAsync(code, 20, ct).ConfigureAwait(false);
                return (await store.ReplaceBlockTradesAsync(code, rows, ct).ConfigureAwait(false), rows.Count);
            },
            cancellationToken).ConfigureAwait(false);

        var northbound = await executor.ExecuteAsync(
            $"{TaskName}-northbound:{code}",
            registry.Capital,
            "主源",
            async ct =>
            {
                var rows = await registry.Capital.GetNorthboundAsync(code, 8, ct).ConfigureAwait(false);
                return rows.Count == 0 ? (0, 0) : (await store.UpsertNorthboundAsync(rows, ct).ConfigureAwait(false), rows.Count);
            },
            cancellationToken).ConfigureAwait(false);

        var written = flows + margins + billboards + blocks + northbound;
        if (written > 0)
        {
            logger.LogInformation(
                "{Code} 资金面已更新：资金流 {Flow}、两融 {Margin}、龙虎榜 {Billboard}、大宗 {Block}、陆股通 {North}",
                code, flows, margins, billboards, blocks, northbound);
        }
        else
        {
            logger.LogDebug("{Code} 暂无资金面数据", code);
        }

        return written;
    }
}
