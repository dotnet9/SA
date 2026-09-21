using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Infrastructure.Collect.Jobs;

namespace SA.Infrastructure.Collect.Hosting;

/// <summary>
/// 按需采集服务：用户打开某只股票的页面而本地缺数据时，尽快补齐它。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么独立成一个宿主服务</b>：按需请求的等待者是<b>正在看页面的人</b>，
/// 而行情调度轮次由全市场扫描的节奏决定（60 秒一轮，一轮还要先跑指数与板块）。
/// 早期版本把按需采集挂在调度轮次末尾，结果是「新开一只股票最多要等一分钟」，
/// 与「接口返回 1003、前端 3 秒 × 5 次重试」的设计完全不匹配（15 秒内就该就绪）。
/// </para>
/// <para>
/// 每轮最多处理 <see cref="MaxPerRound"/> 只、间隔 <see cref="IntervalSeconds"/> 秒：
/// 既让单只标的在几秒内就绪，也不会因为「有人一次点开 20 只」而打满上游。
/// 未处理完的留在队列里，下一轮继续。
/// </para>
/// </remarks>
public sealed class OnDemandHostedService(
    IServiceScopeFactory scopeFactory,
    IOnDemandQueue queue,
    CollectOptions options,
    ILogger<OnDemandHostedService> logger) : BackgroundService
{
    /// <summary>轮询间隔（秒）。</summary>
    public const int IntervalSeconds = 3;

    /// <summary>单轮处理的标的数上限。</summary>
    public const int MaxPerRound = 2;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("采集已关闭，按需采集不启动");
            return;
        }

        // 让启动序列先把股票池准备好：按需采集需要代码已存在于股票池
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(IntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    break;
                }

                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // 单轮异常不能让按需采集停摆，否则「首次打开个股页」会永久停在采集中
                logger.LogError(ex, "按需采集单轮异常，下一拍继续");
            }
        }
    }

    /// <summary>
    /// 执行一轮按需采集。
    /// </summary>
    /// <returns>本轮处理的标的数。</returns>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var codes = queue.Drain(MaxPerRound);
        if (codes.Count == 0)
        {
            return 0;
        }

        logger.LogInformation("按需采集：本轮处理 {Count} 只（排队 {Pending} 只）", codes.Count, queue.PendingCount);

        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var dailyJob = provider.GetRequiredService<DailyKlineJob>();
        var indicatorJob = provider.GetRequiredService<IndicatorJob>();
        var financeJob = provider.GetRequiredService<FinanceJob>();
        var fundamentalJob = provider.GetRequiredService<FundamentalJob>();
        var equityJob = provider.GetRequiredService<EquityJob>();
        var capitalJob = provider.GetRequiredService<CapitalJob>();
        var ratingJob = provider.GetRequiredService<RatingJob>();
        var sectorKlineJob = provider.GetRequiredService<SectorKlineJob>();
        var instruments = provider.GetRequiredService<SA.Application.Abstractions.IInstrumentStore>();
        var sectors = provider.GetRequiredService<SA.Application.Abstractions.ISectorStore>();

        var succeeded = 0;
        foreach (var code in codes)
        {
            // 各自独立 try：财务失败不应把已完成的日线一起算作失败，
            // 反之亦然（用户可能只看趋势页，不想因为财报接口抖动而整只标的重来）
            await RunStepAsync(code, "日线", () => dailyJob.RunIncrementalAsync(code, cancellationToken)).ConfigureAwait(false);
            await RunStepAsync(code, "指标", () => indicatorJob.RunAsync(code, cancellationToken)).ConfigureAwait(false);
            await RunStepAsync(code, "财务", () => financeJob.RunAsync(code, cancellationToken)).ConfigureAwait(false);
            // 基本面历史序列：价值研究的「周期位置」与连续性条件都要它，而它只对
            // 被关注的标的按需拉取（全市场扫描只覆盖最新一期）
            await RunStepAsync(code, "基本面历史", () => fundamentalJob.RunHistoryAsync(code, cancellationToken)).ConfigureAwait(false);
            await RunStepAsync(code, "股权", () => equityJob.RunAsync(code, cancellationToken)).ConfigureAwait(false);
            await RunStepAsync(code, "资金", () => capitalJob.RunAsync(code, cancellationToken)).ConfigureAwait(false);
            await RunStepAsync(code, "评级", () => ratingJob.RunAsync(code, cancellationToken)).ConfigureAwait(false);

            // 该股所属行业的指数日线：因果链的传导带宽要用它。
            // 放在这里而不是只在每日补齐里做，是因为用户正看的这只股票所属行业很可能
            // 不在「资金最显著的 30 个行业」里（会一直拿不到带宽）。
            await RunStepAsync(code, "行业指数", () => EnsureSectorKlineAsync(instruments, sectors, sectorKlineJob, code, cancellationToken))
                .ConfigureAwait(false);

            succeeded++;
        }

        return succeeded;
    }

    /// <summary>
    /// 补齐某标的所属行业的指数日线。
    /// </summary>
    /// <remarks>
    /// 取不到行业（新股、行业缺失）时直接跳过，不算失败：
    /// 这是数据缺失而不是采集故障。
    /// </remarks>
    private static async Task EnsureSectorKlineAsync(
        SA.Application.Abstractions.IInstrumentStore instruments,
        SA.Application.Abstractions.ISectorStore sectors,
        SectorKlineJob job,
        string code,
        CancellationToken cancellationToken)
    {
        var instrument = await instruments.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (instrument?.Industry is null)
        {
            return;
        }

        var all = await sectors.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var sector = all.FirstOrDefault(row => string.Equals(row.Name, instrument.Industry, StringComparison.Ordinal));
        if (sector is null)
        {
            return;
        }

        await job.RunAsync(sector.Code, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunStepAsync(string code, string step, Func<Task> action)
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
            logger.LogWarning(ex, "按需采集 {Code} 的{Step}失败，留待下一轮", code, step);
        }
    }
}
