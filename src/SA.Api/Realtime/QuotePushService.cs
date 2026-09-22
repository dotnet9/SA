using Microsoft.AspNetCore.SignalR;
using SA.Api.Hubs;
using SA.Application.Abstractions;
using SA.Application.Common;
using SA.Contracts.Realtime;
using SA.Domain.Common;
using SA.Domain.Entities.Market;

namespace SA.Api.Realtime;

/// <summary>
/// 行情推送服务：按连接节流，把订阅标的的最新行情推给对应连接。
/// </summary>
/// <remarks>
/// <para>
/// <b>一次取数、多连接分发</b>：所有连接订阅的代码取并集后<b>只请求上游一次</b>
/// （多标的快照端点，请求数与标的数无关），再按连接拆分发送。
/// 这是「服务端按连接维护订阅集合、快照来源为采集写入的内存快照」的落地方式，
/// 避免每来一个连接就打一次上游（实施计划 §5.6）。
/// </para>
/// <para>
/// <b>权限隔离</b>：每个连接收到的代码是其订阅集合的子集，而订阅集合在订阅时已被数据范围过滤
/// （见 <see cref="QuoteHub.Subscribe"/>），因此不同角色的推送天然互不串。
/// </para>
/// <para>
/// 非交易时段不轮询：每天只推一次收盘快照，避免无意义的空转与上游压力。
/// </para>
/// </remarks>
public sealed class QuotePushService(
    IServiceScopeFactory scopeFactory,
    SubscriptionRegistry subscriptions,
    IHubContext<QuoteHub> hub,
    CollectOptions options,
    ILogger<QuotePushService> logger) : BackgroundService
{
    /// <summary>单次取数的代码上限：超出部分下一轮再取，避免一个极大自选把单轮拖住。</summary>
    private const int MaxCodesPerRound = 800;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("采集已关闭，行情推送不启动");
            return;
        }

        // 节拍固定为最小值，实际是否推送由每条连接的间隔决定
        var tick = TimeSpan.FromSeconds(PushIntervals.Default);
        using var timer = new PeriodicTimer(tick);

        logger.LogInformation("行情推送已启动：节拍 {Tick}s，可选间隔 {Intervals}", tick.TotalSeconds, string.Join('/', PushIntervals.Allowed));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    break;
                }

                await PushOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // 推送失败绝不能终止后台服务，否则实时功能会静默失效
                logger.LogError(ex, "行情推送单轮异常，下一拍继续");
            }
        }
    }

    /// <summary>
    /// 执行一轮推送。
    /// </summary>
    /// <remarks>
    /// 交易时段外只推一次收盘快照：用「当轮是否有连接到期」判断，避免为了空转去请求上游。
    /// </remarks>
    public async Task<int> PushOnceAsync(CancellationToken cancellationToken = default)
    {
        var targets = subscriptions.DueTargets(SaTime.Now);
        if (targets.Count == 0)
        {
            return 0;
        }

        var codes = subscriptions.SubscribedCodes();
        if (codes.Count == 0)
        {
            return 0;
        }

        var batch = codes.Take(MaxCodesPerRound).ToList();
        var quotes = await FetchAsync(batch, cancellationToken).ConfigureAwait(false);
        if (quotes.Count == 0)
        {
            return 0;
        }

        var pushed = 0;
        foreach (var (connectionId, connectionCodes) in targets)
        {
            var payload = connectionCodes
                .Select(code => quotes.TryGetValue(code, out var quote) ? quote : null)
                .Where(quote => quote is not null)
                .Select(quote => quote!)
                .ToList();

            if (payload.Count == 0)
            {
                continue;
            }

            await hub.Clients.Client(connectionId)
                .SendAsync("Quote", payload, cancellationToken)
                .ConfigureAwait(false);
            pushed++;
        }

        return pushed;
    }

    /// <summary>
    /// 取行情快照：优先主源，失败自动降级到备源；成功的结果同时写回库与内存快照。
    /// </summary>
    private async Task<Dictionary<string, QuotePushDto>> FetchAsync(
        IReadOnlyList<string> codes,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;
        var registry = provider.GetRequiredService<Infrastructure.Collect.Registry.SourceRegistry>();

        var result = await registry.GetQuotesWithFallbackAsync(codes, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            logger.LogWarning("自选行情取数失败（主源与备源均不可用），本轮跳过推送");
            return [];
        }

        var (source, rows) = result.Value;
        var map = new Dictionary<string, QuotePushDto>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            map[row.Code] = new QuotePushDto(
                Code: row.Code,
                Price: Display.Round(row.Price),
                Chg: Display.Round(row.Change),
                Pct: Display.Round(row.Pct),
                Volume: row.Volume,
                // 与自选列表接口同一口径：金额统一为亿元（详细设计 §1.3）
                Amount: Display.ToYi(row.Amount),
                Turnover: Display.Round(row.Turnover),
                // 量比 0 表示上游未提供；推送里给 null，前端显示「—」而不是「0.00」
                VolRatio: row.VolRatio <= 0 ? null : Display.Round(row.VolRatio),
                AsOf: row.AsOf is null ? null : SaTime.Format(row.AsOf.Value));
        }

        logger.LogDebug("自选行情取数完成：{Rows}/{Requested} 条（来源 {Source}）", map.Count, codes.Count, source);
        return map;
    }
}
