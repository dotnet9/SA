using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Collect;
using SA.Infrastructure.Collect.Adapters;
using SA.Infrastructure.Collect.Jobs;
using SA.Infrastructure.Collect.Registry;

namespace SA.Infrastructure.Collect.Hosting;

/// <summary>
/// 历史回补宿主服务：把「全市场 1 年日线」按优先级分批补完，并断点续跑。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么单独一个宿主服务</b>：全市场 1 年日线约 5,900 个标的 × 2 次请求（前复权 + 不复权），
/// 按 4 req/s 需要约 50 分钟。这既不该塞进 60 秒一轮的行情调度，也不该阻塞首屏，
/// 因此独立成后台任务，按 <see cref="CollectOptions.BackfillIntervalMinutes"/> 一轮一轮推进。
/// </para>
/// <para>
/// <b>优先级</b>（实施计划 §9.3）：基准指数 → 成交额靠前的标的 → 其余。
/// 指数日线是相对强弱的基准，成交额靠前的标的最可能被打开，都在前面；
/// 自选股在自选模块落地后插到指数之后（见批次说明）。
/// </para>
/// <para>
/// <b>断点续跑</b>：每完成一个标的写一条 <c>SyncCursor</c>，重启后只补未完成的标的，
/// 因此中断不会让进度归零。
/// </para>
/// </remarks>
public sealed class BackfillHostedService(
    IServiceScopeFactory scopeFactory,
    CollectOptions options,
    ILogger<BackfillHostedService> logger) : BackgroundService
{
    /// <summary>相对强弱的基准指数。</summary>
    public const string BenchmarkCode = "000300";

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("采集已关闭，历史回补不启动");
            return;
        }

        // 启动后先让行情与指数就位（回补依赖代码清单与基准指数）
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken).ConfigureAwait(false);

        var interval = TimeSpan.FromMinutes(Math.Max(1, options.BackfillIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await RunOneBatchAsync(stoppingToken).ConfigureAwait(false);
                if (processed == 0)
                {
                    logger.LogInformation("历史回补已无待补标的，本轮空闲");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "历史回补批次异常，{Minutes} 分钟后重试", options.BackfillIntervalMinutes);
            }

            await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 推进一轮回补。
    /// </summary>
    /// <returns>本轮处理的标的数（0 表示已补完）。</returns>
    public async Task<int> RunOneBatchAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var codeList = await ResolvePriorityAsync(provider, cancellationToken).ConfigureAwait(false);
        if (codeList.Count == 0)
        {
            return 0;
        }

        var dailyJob = provider.GetRequiredService<DailyKlineJob>();
        var indicatorJob = provider.GetRequiredService<IndicatorJob>();
        var financeJob = provider.GetRequiredService<FinanceJob>();
        var equityJob = provider.GetRequiredService<EquityJob>();
        var cursors = provider.GetRequiredService<ISyncCursorStore>();
        var instruments = provider.GetRequiredService<IInstrumentStore>();

        var done = await cursors.GetByDatasetAsync(SyncDatasets.Daily, cancellationToken).ConfigureAwait(false);
        var finished = done
            .Where(c => c.Status == SyncStatuses.Ok && c.LastDate is not null)
            .Select(c => c.Code)
            .ToHashSet(StringComparer.Ordinal);

        var pending = codeList.Where(code => !finished.Contains(code)).ToList();
        if (pending.Count == 0)
        {
            return 0;
        }

        // 已完成到「上一交易日」的标的视为最新，无需重补（由每日增量任务负责滚动更新）
        var lastTradingDay = await ResolveLastTradingDayAsync(provider, cancellationToken).ConfigureAwait(false);
        var upToDate = done
            .Where(c => c.LastDate is not null && lastTradingDay is not null && c.LastDate >= lastTradingDay)
            .Select(c => c.Code)
            .ToHashSet(StringComparer.Ordinal);

        pending = pending.Where(code => !upToDate.Contains(code)).ToList();
        if (pending.Count == 0)
        {
            return 0;
        }

        var byCode = await instruments.GetByCodesAsync(pending, cancellationToken).ConfigureAwait(false);
        var batch = pending.Take(Math.Max(1, options.BackfillCodesPerRun)).ToList();

        logger.LogInformation(
            "历史回补开始：本轮 {Count} 只（待补共 {Pending} 只，整体已完成 {Done}/{Total}）",
            batch.Count, pending.Count, finished.Count, codeList.Count);

        var succeeded = 0;
        foreach (var code in batch)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var codeName = byCode.TryGetValue(code, out var instrument) ? $"{code} {instrument.Name}" : code;

            try
            {
                var bars = await dailyJob.RunIncrementalAsync(code, cancellationToken).ConfigureAwait(false);
                if (bars > 0)
                {
                    await indicatorJob.RunAsync(code, cancellationToken).ConfigureAwait(false);

                    // 财务与股权都是季频且体量小，顺带补齐，省一次独立的回补轮次
                    await financeJob.RunAsync(code, cancellationToken).ConfigureAwait(false);
                    await equityJob.RunAsync(code, cancellationToken).ConfigureAwait(false);
                    succeeded++;
                }
                else
                {
                    logger.LogDebug("{Code} 无新日线（可能为停牌或新代码）", codeName);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 单个标的失败不应中断整轮回补：游标已记录失败状态，下一轮会重试
                logger.LogWarning(ex, "回补 {Code} 失败，跳过并留待下一轮", codeName);
            }
        }

        logger.LogInformation("历史回补本轮结束：成功 {Succeeded}/{Count} 只", succeeded, batch.Count);
        return succeeded;
    }

    /// <summary>
    /// 按优先级组装待补代码清单。
    /// </summary>
    private async Task<List<string>> ResolvePriorityAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        var instruments = provider.GetRequiredService<IInstrumentStore>();
        var all = await instruments.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (all.Count == 0)
        {
            return [];
        }

        var quotes = provider.GetRequiredService<IQuoteSnapshotStore>();
        var snapshot = await quotes.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // 基准指数在最前（相对强弱需要它），随后是成交额靠前的标的，最后是其余
        var result = new List<string> { BenchmarkCode };

        var ranked = all
            .Where(i => MarketCodes.IsStockCode(i.Code))
            .Select(i => (i.Code, Amount: snapshot.FirstOrDefault(s => s.Code == i.Code)?.Amount ?? 0m))
            .OrderByDescending(x => x.Amount)
            .Select(x => x.Code);

        result.AddRange(ranked.Where(code => code != BenchmarkCode));
        return result.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>取最近一个交易日，用于判断标的是否已补到最新。</summary>
    private static async Task<DateOnly?> ResolveLastTradingDayAsync(
        IServiceProvider provider,
        CancellationToken cancellationToken)
    {
        var calendar = provider.GetRequiredService<ITradingCalendarStore>();
        var today = SaTime.Today;
        var rows = await calendar
            .GetRangeAsync(today.AddDays(-15), today, cancellationToken)
            .ConfigureAwait(false);

        var open = rows.Where(r => r.IsOpen).Select(r => r.Date).ToList();
        return open.Count == 0 ? null : open.Max();
    }
}

/// <summary>
/// 基准指数日线任务：相对强弱需要沪深 300 的同区间日线。
/// </summary>
/// <remarks>
/// 指数与个股共用 K 线端点，只是 <c>secid</c> 不同（<c>1.000300</c>），
/// 因此复用同一个适配器，不做第二套实现。
/// </remarks>
public sealed class BenchmarkDailyJob(
    SourceRegistry registry,
    CollectExecutor executor,
    IDailyHistoryStore daily,
    ILogger<BenchmarkDailyJob> logger)
{
    /// <summary>任务名。</summary>
    public const string TaskName = "daily-kline-benchmark";

    /// <summary>基准指数代码。</summary>
    public const string Code = BackfillHostedService.BenchmarkCode;

    /// <summary>基准回看天数：比个股更长，保证长周期分位有足够样本。</summary>
    public const int LookbackDays = 1500;

    /// <summary>
    /// 增量更新基准指数日线。
    /// </summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var today = SaTime.Today;
        var last = await daily.GetLastDateAsync(Code, cancellationToken).ConfigureAwait(false);
        var from = last is null ? today.AddDays(-LookbackDays) : last.Value.AddDays(1);

        if (from > today)
        {
            return 0;
        }

        var bars = new List<Domain.History.DailyBar>();

        var result = await executor.ExecuteAsync(
            $"{TaskName}:{Code}",
            registry.Klines[0],
            "主源",
            async ct =>
            {
                var hit = await registry
                    .GetDailyWithFallbackAsync(Code, from, today, EastMoneyKlineSource.AdjustNone, EastMoneyKlineSource.PeriodDaily, ct)
                    .ConfigureAwait(false);

                if (hit is null)
                {
                    return (0, 0);
                }

                logger.LogInformation("基准指数 {Code} 日线命中数据源：{Source}", Code, hit.Value.Source);
                bars = hit.Value.Bars.ToList();
                return (bars.Count, bars.Count);
            },
            cancellationToken).ConfigureAwait(false);

        if (result == 0 || bars.Count == 0)
        {
            return 0;
        }

        var written = await daily.MergeAsync(Code, bars, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("基准指数 {Code} 日线已更新：{Rows} 行", Code, written);
        return written;
    }
}
