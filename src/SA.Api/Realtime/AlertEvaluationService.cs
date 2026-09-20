using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SA.Api.Hubs;
using SA.Application.Abstractions;
using SA.Application.Alerts;
using SA.Contracts.Realtime;
using SA.Domain.Alerts;
using SA.Domain.Common;

namespace SA.Api.Realtime;

/// <summary>
/// 提醒评估服务：按固定节拍评估启用中的规则，触发即写通知并推送。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么在 Api 宿主而不是采集宿主</b>：通知要经 SignalR 推给浏览器，
/// 而推送通道在 Api 进程里。若放在采集进程，还需要跨进程转发，复杂度远高于收益。
/// </para>
/// <para>
/// <b>节拍与冷却</b>：评估节拍 30 秒（比行情推送慢，因为提醒不追求秒级），
/// 每条规则另有 <see cref="AlertEvaluator.Cooldown"/> 冷却窗口，避免阈值附近震荡时刷屏。
/// </para>
/// </remarks>
public sealed class AlertEvaluationService(
    IServiceScopeFactory scopeFactory,
    IHubContext<QuoteHub> hub,
    CollectOptions options,
    ILogger<AlertEvaluationService> logger) : BackgroundService
{
    /// <summary>评估节拍（秒）。</summary>
    public const int IntervalSeconds = 30;

    /// <summary>单轮最多评估的规则数（超过则下轮继续，避免一次吃掉整轮时间）。</summary>
    private const int MaxRulesPerRound = 500;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("采集已关闭，提醒评估不启动");
            return;
        }

        // 让行情与股票池先就位：评估依赖快照与日线
        await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(IntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    break;
                }

                await EvaluateOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "提醒评估单轮异常，下一拍继续");
            }
        }
    }

    /// <summary>
    /// 执行一轮评估。
    /// </summary>
    /// <returns>本轮触发的通知数。</returns>
    public async Task<int> EvaluateOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var alerts = provider.GetRequiredService<IAlertStore>();
        var rules = await alerts.GetEnabledRulesAsync(cancellationToken).ConfigureAwait(false);
        if (rules.Count == 0)
        {
            return 0;
        }

        var quotes = provider.GetRequiredService<IQuoteSnapshotStore>();
        var instruments = provider.GetRequiredService<IInstrumentStore>();
        var daily = provider.GetRequiredService<IDailyHistoryStore>();

        var now = SaTime.Now;
        var codes = rules.Select(rule => rule.Code).Distinct(StringComparer.Ordinal).ToList();
        var quoteMap = await quotes.GetByCodesAsync(codes, cancellationToken).ConfigureAwait(false);
        var instrumentMap = await instruments.GetByCodesAsync(codes, cancellationToken).ConfigureAwait(false);

        var triggered = new List<Domain.Entities.Alerts.Notification>();
        var triggeredRules = new List<(string RuleId, string UserId, string Code, string Level, string Title, string Body)>();

        foreach (var rule in rules.Take(MaxRulesPerRound))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // 该标的当轮只取一次日线：同一标的可能有多条规则
            quoteMap.TryGetValue(rule.Code, out var quote);
            if (quote is null)
            {
                continue;
            }

            var bars = rule.RuleType is Domain.Entities.Alerts.AlertRuleTypes.BreakMa20
                or Domain.Entities.Alerts.AlertRuleTypes.NewHigh
                or Domain.Entities.Alerts.AlertRuleTypes.NewLow
                    ? await daily.GetLatestAsync(rule.Code, 250, cancellationToken).ConfigureAwait(false)
                    : [];

            var closes = bars.Select(bar => bar.Close).ToList();
            var highs = bars.Select(bar => bar.High).ToList();
            var lows = bars.Select(bar => bar.Low).ToList();

            var evaluation = AlertService.Evaluate(rule, quote, closes, highs, lows, now);
            if (!evaluation.Triggered)
            {
                continue;
            }

            var name = instrumentMap.TryGetValue(rule.Code, out var instrument) ? instrument.Name : null;
            var notification = AlertNotificationFactory.Create(rule, name, evaluation, now);
            triggered.Add(notification);

            triggeredRules.Add((
                rule.Id,
                rule.UserId,
                rule.Code,
                notification.Level,
                notification.Title,
                notification.Body));

            await alerts.MarkTriggeredAsync(rule.Id, now, cancellationToken).ConfigureAwait(false);
        }

        if (triggered.Count == 0)
        {
            return 0;
        }

        await alerts.AddNotificationsAsync(triggered, cancellationToken).ConfigureAwait(false);

        // 推送给对应用户的实时连接（按用户分组，避免把 A 的提醒推给 B）
        foreach (var item in triggeredRules)
        {
            await hub.Clients
                .Group(UserGroups.Of(item.UserId))
                .SendAsync(
                    "AlertTriggered",
                    new AlertTriggeredDto(
                        RuleId: item.RuleId,
                        Code: item.Code,
                        Name: null,
                        Title: item.Title,
                        Body: item.Body,
                        Value: null,
                        RuleType: null,
                        TriggeredAt: SaTime.Format(now)),
                    cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("提醒评估：触发 {Count} 条通知（本轮评估 {Rules} 条规则）", triggered.Count, rules.Count);
        return triggered.Count;
    }
}

/// <summary>
/// 实时连接的 SignalR 分组：以用户为组，保证同一用户的多个标签页都能收到自己的提醒。
/// </summary>
public static class UserGroups
{
    /// <summary>用户分组名。</summary>
    public static string Of(string userId) => $"user:{userId}";
}
