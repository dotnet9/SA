using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Domain.Analysis;
using SA.Domain.Common;
using SA.Domain.Entities.Collect;
using SA.Domain.History;
using SA.Infrastructure.Collect.Adapters;
using SA.Infrastructure.Collect.Registry;

namespace SA.Infrastructure.Collect.Jobs;

/// <summary>
/// 日线入库任务：按标的增量拉取前复权日线，并检测除权除息。
/// </summary>
/// <remarks>
/// <para>
/// <b>复权口径</b>：入库序列固定为<b>前复权</b>（<c>fqt=1</c>），指标与图形都按同一口径计算，
/// 避免「图形前复权、指标不复权」这类看起来对不上的问题（实施计划 §5.3）。
/// </para>
/// <para>
/// <b>除权检测</b>：同时拉 <c>fqt=0</c> 与 <c>fqt=1</c>，逐日计算比值
/// <c>r = 前复权收盘 / 不复权收盘</c>。前复权序列在除权日会整体重算，因此 <c>r</c> 在除权前后
/// 会发生变化；一旦发现变化，说明该标的的历史前复权价全部失效，必须整段重拉并重算指标。
/// </para>
/// </remarks>
public sealed class DailyKlineJob(
    SourceRegistry registry,
    CollectExecutor executor,
    IDailyHistoryStore daily,
    ISyncCursorStore cursors,
    CollectOptions options,
    ILogger<DailyKlineJob> logger)
{
    /// <summary>任务名。</summary>
    public const string TaskName = "daily-kline";

    /// <summary>除权检测时比对的最近行数：再多也只影响检测灵敏度，不影响成本。</summary>
    private const int AdjustmentProbeRows = 10;

    /// <summary>
    /// 增量更新一个标的：从「已入库的最后一天 + 1」开始拉，直到今天。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>写入行数；无新数据返回 0。</returns>
    public async Task<int> RunIncrementalAsync(string code, CancellationToken cancellationToken = default)
    {
        var today = SaTime.Today;
        var last = await daily.GetLastDateAsync(code, cancellationToken).ConfigureAwait(false);

        // 多回看几天：保证除权检测有足够的重叠区间，也让「昨日漏跑」自动补上
        var from = last is null
            ? today.AddDays(-options.BackfillLookbackDays)
            : last.Value.AddDays(-AdjustmentProbeRows);

        if (from > today)
        {
            return 0;
        }

        return await FetchAndStoreAsync(code, from, today, last is null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 全量重建一个标的（除权触发或按需拉取时使用）。
    /// </summary>
    public async Task<int> RunFullAsync(string code, CancellationToken cancellationToken = default)
    {
        var today = SaTime.Today;
        await daily.DeleteAsync(code, cancellationToken).ConfigureAwait(false);
        return await FetchAndStoreAsync(code, today.AddDays(-options.BackfillLookbackDays), today, true, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<int> FetchAndStoreAsync(
        string code,
        DateOnly from,
        DateOnly to,
        bool isFull,
        CancellationToken cancellationToken)
    {
        var front = new List<DailyBar>();
        var raw = new List<DailyBar>();

        var result = await executor.ExecuteAsync(
            $"{TaskName}:{code}",
            registry.Kline,
            "主源",
            async ct =>
            {
                front = (await registry.Kline
                    .GetDailyAsync(code, from, to, EastMoneyKlineSource.AdjustFront, EastMoneyKlineSource.PeriodDaily, ct)
                    .ConfigureAwait(false)).ToList();

                raw = (await registry.Kline
                    .GetDailyAsync(code, from, to, EastMoneyKlineSource.AdjustNone, EastMoneyKlineSource.PeriodDaily, ct)
                    .ConfigureAwait(false)).ToList();

                return (front.Count, front.Count);
            },
            cancellationToken).ConfigureAwait(false);

        if (result == 0 || front.Count == 0)
        {
            if (result == 0)
            {
                // 失败：记一条待重试游标，下一轮继续（断点续跑）
                await cursors.UpsertAsync(
                    new SyncCursor
                    {
                        Dataset = SyncDatasets.Daily,
                        Code = code,
                        Status = SyncStatuses.Error,
                        Note = "增量拉取失败，待重试",
                        UpdatedAt = SaTime.Now
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            return 0;
        }

        // 除权检测：增量更新时才有意义（全量重建本身就是最新口径）
        if (!isFull && DetectAdjustment(raw, front))
        {
            logger.LogInformation("{Code} 检测到除权除息，触发全量重拉与指标重算", code);
            return await RunFullAsync(code, cancellationToken).ConfigureAwait(false);
        }

        var written = await daily.MergeAsync(code, front, cancellationToken).ConfigureAwait(false);

        await cursors.UpsertAsync(
            new SyncCursor
            {
                Dataset = SyncDatasets.Daily,
                Code = code,
                LastDate = front[^1].Date,
                Status = SyncStatuses.Ok,
                Note = null,
                UpdatedAt = SaTime.Now
            },
            cancellationToken).ConfigureAwait(false);

        return front.Count;
    }

    /// <summary>
    /// 判断是否发生除权除息。
    /// </summary>
    /// <remarks>
    /// 前复权价与不复权价之比在无除权时应当恒定（同一日两者只差一个常数因子）；
    /// 一旦该比值变化，说明前复权序列被整体重算，历史数据必须重建。
    /// </remarks>
    internal static bool DetectAdjustment(IReadOnlyList<DailyBar> raw, IReadOnlyList<DailyBar> front)
    {
        var rawByDate = raw.ToDictionary(bar => bar.Date, bar => bar.Close);
        var ratios = new List<decimal>();

        foreach (var bar in front)
        {
            if (!rawByDate.TryGetValue(bar.Date, out var rawClose) || rawClose == 0)
            {
                continue;
            }

            ratios.Add(bar.Close / rawClose);
        }

        if (ratios.Count < 2)
        {
            return false;
        }

        // 允许 1e-6 的相对误差：两次请求之间上游自身的舍入会带来微小抖动
        var first = ratios[0];
        return ratios.Any(ratio => Math.Abs(ratio - first) > 1e-6m);
    }
}

/// <summary>
/// 指标计算任务：为已有日线的标的增量计算 MA / MACD / KDJ / RSI / BOLL。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么重算一段而不是只算新增行</b>：EMA 族指标（MACD、RSI）是路径相关的，
/// 只算新增行需要把状态持久化下来，代价与出错风险都高于「读一段窗口重算」。
/// 这里每次读最近 <see cref="WarmupBars"/> 根重算并覆盖写入，覆盖是按日期合并，
/// 因此天然幂等。
/// </para>
/// <para>
/// 窗口取 400 根（≈1.6 年）：EMA 的实际记忆长度远短于此，因此窗口起点对最新值的
/// 影响在小数点后 6 位以外，可以忽略；窗口不足时按可用样本计算，不足周期的位置一律为 null。
/// </para>
/// </remarks>
public sealed class IndicatorJob(
    CollectExecutor executor,
    SourceRegistry registry,
    IDailyHistoryStore daily,
    IIndicatorStore indicators,
    ISyncCursorStore cursors)
{
    /// <summary>任务名。</summary>
    public const string TaskName = "indicator";

    /// <summary>重算窗口长度（含预热）。</summary>
    public const int WarmupBars = 400;

    /// <summary>
    /// 计算并写入一个标的的指标。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>写入行数。</returns>
    public async Task<int> RunAsync(string code, CancellationToken cancellationToken = default)
    {
        var bars = await daily.GetLatestAsync(code, WarmupBars, cancellationToken).ConfigureAwait(false);
        if (bars.Count == 0)
        {
            return 0;
        }

        var result = await executor.ExecuteAsync(
            $"{TaskName}:{code}",
            registry.Kline,
            "本地计算",
            async ct =>
            {
                var rows = Compute(bars);
                var written = await indicators.MergeAsync(code, rows, ct).ConfigureAwait(false);
                return (written, written);
            },
            cancellationToken).ConfigureAwait(false);

        if (result > 0)
        {
            await cursors.UpsertAsync(
                new SyncCursor
                {
                    Dataset = SyncDatasets.Indicator,
                    Code = code,
                    LastDate = bars[^1].Date,
                    Status = SyncStatuses.Ok,
                    UpdatedAt = SaTime.Now
                },
                cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// 由日线序列计算指标行。纯函数，便于与独立计算逐点比对。
    /// </summary>
    /// <param name="bars">日线序列（按日期升序）。</param>
    public static List<IndicatorRow> Compute(IReadOnlyList<DailyBar> bars)
    {
        var closes = bars.Select(bar => bar.Close).ToArray();
        var highs = bars.Select(bar => bar.High).ToArray();
        var lows = bars.Select(bar => bar.Low).ToArray();

        var ma5 = Indicators.Sma(closes, 5);
        var ma10 = Indicators.Sma(closes, 10);
        var ma20 = Indicators.Sma(closes, 20);
        var ma60 = Indicators.Sma(closes, 60);
        var macd = Indicators.Macd(closes);
        var kdj = Indicators.Kdj(highs, lows, closes);
        var rsi6 = Indicators.Rsi(closes, 6);
        var rsi12 = Indicators.Rsi(closes, 12);
        var rsi24 = Indicators.Rsi(closes, 24);
        var boll = Indicators.Boll(closes, 20, 2m);

        var rows = new List<IndicatorRow>(bars.Count);
        for (var i = 0; i < bars.Count; i++)
        {
            rows.Add(new IndicatorRow(
                Date: bars[i].Date,
                Ma5: Round(ma5[i]),
                Ma10: Round(ma10[i]),
                Ma20: Round(ma20[i]),
                Ma60: Round(ma60[i]),
                Dif: Round(macd.Dif[i], 6),
                Dea: Round(macd.Dea[i], 6),
                Macd: Round(macd.Macd[i], 6),
                K: Round(kdj.K[i]),
                D: Round(kdj.D[i]),
                J: Round(kdj.J[i]),
                Rsi6: Round(rsi6[i]),
                Rsi12: Round(rsi12[i]),
                Rsi24: Round(rsi24[i]),
                BollUp: Round(boll.Upper[i]),
                BollMid: Round(boll.Middle[i]),
                BollLow: Round(boll.Lower[i])));
        }

        return rows;
    }

    private static decimal? Round(decimal? value, int digits = 4) =>
        value is null ? null : Math.Round(value.Value, digits, MidpointRounding.AwayFromZero);
}
