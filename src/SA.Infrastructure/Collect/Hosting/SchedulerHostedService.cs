using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Infrastructure.Collect.Jobs;
using SA.Domain.Common;

namespace SA.Infrastructure.Collect.Hosting;

/// <summary>
/// 采集调度：按交易日历与市场时段驱动各采集任务。
/// </summary>
/// <remarks>
/// <para>
/// 调度策略（实施计划 §5.4，扫描间隔受上游分页上限约束，见 <see cref="IMarketListSource"/>）：
/// </para>
/// <list type="bullet">
/// <item>启动时先跑一次「日历 → 指数 → 股票池 → 板块 → 全市场快照 → 市场统计」，
/// 保证首屏可用；顺序不可颠倒，因为快照的业务日期取自指数口径日。</item>
/// <item>盘中（09:15–15:05）每 <c>FullScanIntervalSeconds</c> 秒跑一轮轻任务（指数 / 板块 / 快照）。</item>
/// <item>市场统计（涨跌停 / 资金流 / 两融）单独按 5 分钟节奏跑，其数据源较慢且盘中变化不敏感。</item>
/// <item>非交易日只跑一次收盘快照与校验任务，不再高频轮询。</item>
/// </list>
/// <para>
/// 任何一次任务失败都不会让循环退出：失败明细由 <c>CollectExecutor</c> 记入
/// <c>CollectTaskLog</c> / <c>DataSourceStatus</c>，界面在数据源监控与市场页展示。
/// </para>
/// </remarks>
public sealed class SchedulerHostedService(
    IServiceScopeFactory scopeFactory,
    CollectOptions options,
    ILogger<SchedulerHostedService> logger) : BackgroundService
{
    /// <summary>市场统计的重跑间隔。</summary>
    private static readonly TimeSpan MarketStatInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 全市场基本面的重跑间隔。
    /// </summary>
    /// <remarks>
    /// 6 小时而不是每天一次：披露季里同一报告期会持续有新的标的补披露，
    /// 而一次扫描是 27 个请求（约 20 秒），成本远低于错过新披露的代价。
    /// 主键是 upsert，重扫幂等。
    /// </remarks>
    private static readonly TimeSpan FundamentalInterval = TimeSpan.FromHours(6);

    /// <summary>非交易日的轮询间隔（只需偶尔确认是否进入新的交易日）。</summary>
    private static readonly TimeSpan IdleInterval = TimeSpan.FromMinutes(30);


    private DateTimeOffset _lastMarketStatAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastUniverseAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastCalendarAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastSectorKlineAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastFundamentalAt = DateTimeOffset.MinValue;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("采集已按配置关闭（Sa:Collector:Enabled=false），调度不启动");
            return;
        }

        // 启动自检：让宿主先完成 HTTP 就绪，再开始打上游
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);

        logger.LogInformation(
            "采集调度启动：全市场扫描 {Scan}s / 按域名限速 {Interval}ms / 并发 {Concurrency} / 熔断 {Threshold} 次 {Cooldown}s",
            options.FullScanIntervalSeconds, options.PerHostMinIntervalMs, options.MaxConcurrency,
            options.BreakerThreshold, options.BreakerCooldownSeconds);

        await RunStartupAsync(stoppingToken).ConfigureAwait(false);

        var interval = TimeSpan.FromSeconds(Math.Max(10, options.FullScanIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var today = SaTime.Today;
                var isTradingDay = await IsTradingDayAsync(today, stoppingToken).ConfigureAwait(false);
                var inSession = MarketSession.IsTradingTime();

                if (isTradingDay || options.CollectOnNonTradingDay)
                {
                    await RunCycleAsync(isTradingDay && inSession, stoppingToken).ConfigureAwait(false);
                }
                else
                {
                    logger.LogDebug("非交易日且未开启非交易日采集，本轮跳过");
                }

                var delay = isTradingDay && inSession ? interval : IdleInterval;
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // 调度循环绝不能因为单轮异常退出，否则采集会静默停摆
                logger.LogError(ex, "采集调度单轮异常，{Delay}s 后继续", 30);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
            }
        }

        logger.LogInformation("采集调度已停止");
    }

    /// <summary>
    /// 启动时的一次性采集。顺序有意义：日历 → 指数（提供业务日期）→ 池子 → 板块 → 快照 → 统计。
    /// </summary>
    private async Task RunStartupAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        await RunAsync("交易日历", () => provider.GetRequiredService<TradingCalendarJob>().RunAsync(cancellationToken)).ConfigureAwait(false);
        _lastCalendarAt = SaTime.Now;

        await RunAsync("指数快照", () => provider.GetRequiredService<IndexJob>().RunAsync(cancellationToken)).ConfigureAwait(false);
        await RunAsync("股票池", () => provider.GetRequiredService<UniverseJob>().RunAsync(cancellationToken)).ConfigureAwait(false);
        _lastUniverseAt = SaTime.Now;

        await RunAsync("行业板块", () => provider.GetRequiredService<SectorJob>().RunAsync(cancellationToken)).ConfigureAwait(false);
        await RunAsync("全市场快照", () => provider.GetRequiredService<QuoteSnapshotJob>().RunAsync(cancellationToken)).ConfigureAwait(false);

        // 行业指数日线依赖行业板块快照（按资金显著度挑选行业），因此必须排在它之后
        await RunAsync("行业指数日线", () => provider.GetRequiredService<SectorKlineJob>().RunPriorityAsync(cancellationToken)).ConfigureAwait(false);
        _lastSectorKlineAt = SaTime.Now;

        await RunAsync("市场统计", () => provider.GetRequiredService<MarketStatJob>().RunAsync(cancellationToken)).ConfigureAwait(false);
        _lastMarketStatAt = SaTime.Now;

        // 全市场基本面：选股器的数据基础。放在最后，因为它最慢（27 次请求），
        // 且首屏（市场概览）不依赖它。
        await RunAsync("全市场基本面", () => provider.GetRequiredService<FundamentalJob>().RunMarketAsync(cancellationToken)).ConfigureAwait(false);
        _lastFundamentalAt = SaTime.Now;
    }

    /// <summary>
    /// 一轮常规采集。轻任务每轮都跑；重任务按各自间隔。
    /// </summary>
    /// <param name="inSession">当前是否处于盘中时段。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task RunCycleAsync(bool inSession, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;
        var now = SaTime.Now;

        // 指数必须每轮更新：它同时是全市场快照业务日期的来源
        await RunAsync("指数快照", () => provider.GetRequiredService<IndexJob>().RunAsync(cancellationToken)).ConfigureAwait(false);
        await RunAsync("行业板块", () => provider.GetRequiredService<SectorJob>().RunAsync(cancellationToken)).ConfigureAwait(false);

        // 盘中刷新快照；非盘中只在快照过期时补一次（例如收盘后首次进入）
        if (inSession)
        {
            await RunAsync("全市场快照", () => provider.GetRequiredService<QuoteSnapshotJob>().RunAsync(cancellationToken)).ConfigureAwait(false);
        }
        else
        {
            var quotes = provider.GetRequiredService<IQuoteSnapshotStore>();
            var lastUpdated = await quotes.GetLastUpdatedAtAsync(cancellationToken).ConfigureAwait(false);
            var today = SaTime.Today;
            var stale = lastUpdated is null
                || DateOnly.FromDateTime(SaTime.ToLocal(lastUpdated.Value)) != today;

            if (stale)
            {
                await RunAsync("全市场快照", () => provider.GetRequiredService<QuoteSnapshotJob>().RunAsync(cancellationToken)).ConfigureAwait(false);
            }
        }

        if (now - _lastMarketStatAt >= MarketStatInterval)
        {
            await RunAsync("市场统计", () => provider.GetRequiredService<MarketStatJob>().RunAsync(cancellationToken)).ConfigureAwait(false);
            _lastMarketStatAt = now;
        }

        // 股票池与日历每天刷新一次即可（新上市、改名、节假日表）
        if (now - _lastUniverseAt >= TimeSpan.FromHours(12))
        {
            await RunAsync("股票池", () => provider.GetRequiredService<UniverseJob>().RunAsync(cancellationToken)).ConfigureAwait(false);
            _lastUniverseAt = now;
        }

        if (now - _lastCalendarAt >= TimeSpan.FromHours(12))
        {
            await RunAsync("交易日历", () => provider.GetRequiredService<TradingCalendarJob>().RunAsync(cancellationToken)).ConfigureAwait(false);
            _lastCalendarAt = now;
        }

        // 行业指数日线每天滚动补齐一次（收盘后一次即可；景气度依赖它算相对强弱与带宽）
        if (now - _lastSectorKlineAt >= TimeSpan.FromHours(12))
        {
            await RunAsync("行业指数日线", () => provider.GetRequiredService<SectorKlineJob>().RunPriorityAsync(cancellationToken)).ConfigureAwait(false);
            _lastSectorKlineAt = now;
        }

        if (now - _lastFundamentalAt >= FundamentalInterval)
        {
            await RunAsync("全市场基本面", () => provider.GetRequiredService<FundamentalJob>().RunMarketAsync(cancellationToken)).ConfigureAwait(false);
            _lastFundamentalAt = now;
        }

        // 按需采集不在这里：它由 OnDemandHostedService 以 3 秒节拍独立处理，
        // 否则「首次打开个股页」最坏要等一个完整的行情轮次（见该服务的说明）
    }

    /// <summary>
    /// 交易日判定：以日历为准；日历尚未生成时退化为「周一至周五」。
    /// </summary>
    private async Task<bool> IsTradingDayAsync(DateOnly today, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var calendar = scope.ServiceProvider.GetRequiredService<ITradingCalendarStore>();
        var rows = await calendar.GetRangeAsync(today, today, cancellationToken).ConfigureAwait(false);

        if (rows.Count > 0)
        {
            return rows[0].IsOpen;
        }

        return today.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    /// <summary>
    /// 执行单个任务并吞掉异常（异常已由执行外壳记录，这里只负责不让循环中断）。
    /// </summary>
    private async Task RunAsync(string label, Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "任务「{Label}」执行失败，本轮跳过", label);
        }
    }
}
