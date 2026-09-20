using System.Text.Json;
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
/// <b>节拍与冷却</b>：评估节拍 30 秒；每条规则另有 <see cref="AlertEvaluator.Cooldown"/>，
/// 避免阈值附近震荡时刷屏。
/// </para>
/// <para>
/// <b>合并窗口</b>：一轮评估内同一用户触发的多条提醒合并为一条站内通知
/// （正文列出各条），避免「一次行情跳动触发 5 条规则 → 收到 5 条通知」。
/// 窗口长度即评估节拍，实现上不需要额外的时间轮。
/// </para>
/// <para>
/// <b>免打扰</b>：处于免打扰时段时只写站内通知、不发起 Web Push；
/// 这样「不打扰」不等于「丢消息」，用户回到应用仍能看到。
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

    /// <summary>同一用户单轮最多合并多少条提醒（超过部分只计数，避免通知正文过长）。</summary>
    private const int MaxMergedItems = 8;

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
    /// <returns>本轮写出的通知条数（合并后）。</returns>
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
        var capital = provider.GetRequiredService<ICapitalStore>();

        var now = SaTime.Now;
        var codes = rules.Select(rule => rule.Code).Distinct(StringComparer.Ordinal).ToList();
        var quoteMap = await quotes.GetByCodesAsync(codes, cancellationToken).ConfigureAwait(false);
        var instrumentMap = await instruments.GetByCodesAsync(codes, cancellationToken).ConfigureAwait(false);

        // 命中明细按用户分组：一轮内的多条提醒会合并成一条通知（合并窗口 = 本拍）
        var hitsByUser = new Dictionary<string, List<MergedHit>>(StringComparer.Ordinal);

        foreach (var rule in rules.Take(MaxRulesPerRound))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            quoteMap.TryGetValue(rule.Code, out var quote);
            if (quote is null)
            {
                continue;
            }

            var needsBars = rule.RuleType is Domain.Entities.Alerts.AlertRuleTypes.BreakMa20
                or Domain.Entities.Alerts.AlertRuleTypes.NewHigh
                or Domain.Entities.Alerts.AlertRuleTypes.NewLow;

            var bars = needsBars
                ? await daily.GetLatestAsync(rule.Code, 250, cancellationToken).ConfigureAwait(false)
                : [];

            // 资金类规则才读资金流：避免为每条价格规则多打一次库
            var fundFlow = AlertService.NeedsFundFlow(rule.RuleType)
                ? (await capital.GetFundFlowAsync(rule.Code, 20, cancellationToken).ConfigureAwait(false))
                    .Select(row => row.MainNet).ToList()
                : null;

            var evaluation = AlertService.Evaluate(
                rule,
                quote,
                bars.Select(bar => bar.Close).ToList(),
                bars.Select(bar => bar.High).ToList(),
                bars.Select(bar => bar.Low).ToList(),
                fundFlow,
                now);

            if (!evaluation.Triggered)
            {
                continue;
            }

            var name = instrumentMap.TryGetValue(rule.Code, out var instrument) ? instrument.Name : rule.Code;

            if (!hitsByUser.TryGetValue(rule.UserId, out var hits))
            {
                hits = [];
                hitsByUser[rule.UserId] = hits;
            }

            hits.Add(new MergedHit(
                RuleId: rule.Id,
                Code: rule.Code,
                Name: name,
                Title: evaluation.Title ?? "提醒触发",
                Body: evaluation.Body ?? string.Empty,
                Level: evaluation.Level ?? "info"));

            await alerts.MarkTriggeredAsync(rule.Id, now, cancellationToken).ConfigureAwait(false);
        }

        if (hitsByUser.Count == 0)
        {
            return 0;
        }

        var settings = provider.GetRequiredService<ISettingsStore>();
        var notifications = new List<Domain.Entities.Alerts.Notification>();
        var written = 0;

        foreach (var (userId, hits) in hitsByUser)
        {
            var preference = await UserNotifySettingsStore.GetAsync(settings, userId, cancellationToken)
                .ConfigureAwait(false);

            var notification = AlertNotificationFactory.CreateMerged(userId, hits, now, MaxMergedItems);
            notifications.Add(notification);
            written++;

            // 1) 站内通知经 SignalR 推给该用户的实时连接（按用户分组，避免把 A 的提醒推给 B）
            await hub.Clients
                .Group(UserGroups.Of(userId))
                .SendAsync(
                    "AlertTriggered",
                    new AlertTriggeredDto(
                        RuleId: hits[0].RuleId,
                        Code: hits[0].Code,
                        Name: hits[0].Name,
                        Title: notification.Title,
                        Body: notification.Body,
                        Value: null,
                        RuleType: null,
                        TriggeredAt: SaTime.Format(now)),
                    cancellationToken).ConfigureAwait(false);

            // 2) 浏览器推送：免打扰期间跳过（站内记录已写，不丢消息）
            if (!preference.PushEnabled)
            {
                continue;
            }

            if (preference.InDndWindow(now))
            {
                logger.LogDebug("用户 {UserId} 处于免打扰时段，跳过浏览器推送（站内通知已写入）", userId);
                continue;
            }

            await SendBrowserPushAsync(provider, userId, notification, cancellationToken).ConfigureAwait(false);
        }

        await alerts.AddNotificationsAsync(notifications, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "提醒评估：{Hits} 条命中合并为 {Merged} 条通知（本轮评估 {Rules} 条规则）",
            hitsByUser.Sum(pair => pair.Value.Count), written, rules.Count);

        return written;
    }

    /// <summary>
    /// 向该用户的全部有效订阅发送 Web Push。
    /// </summary>
    /// <remarks>
    /// 订阅失效（404/410）时删除该行；其他失败累加计数，连续失败到阈值后停用订阅。
    /// 这样死端点不会在每轮评估里持续消耗请求。
    /// </remarks>
    private async Task SendBrowserPushAsync(
        IServiceProvider provider,
        string userId,
        Domain.Entities.Alerts.Notification notification,
        CancellationToken cancellationToken)
    {
        var pushStore = provider.GetRequiredService<IPushSubscriptionStore>();
        var sender = provider.GetRequiredService<IPushSender>();

        var subscriptions = await pushStore.GetByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        var active = subscriptions.Where(row => row.Enabled).ToList();
        if (active.Count == 0)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            title = notification.Title,
            body = notification.Body,
            level = notification.Level,
            code = notification.Code,
            notificationId = notification.Id,
            url = notification.Code is null ? "/notifications" : $"/stock/{notification.Code}"
        });

        foreach (var subscription in active)
        {
            var result = await sender.SendAsync(subscription, payload, cancellationToken).ConfigureAwait(false);

            if (result.Ok)
            {
                await pushStore.RecordResultAsync(subscription.Id, true, null, options.DegradeAfterFailures, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (result.Permanent)
            {
                // 订阅已失效：留着只会让每轮都失败一次
                await pushStore.RemoveByEndpointAsync(subscription.Endpoint, cancellationToken).ConfigureAwait(false);
                logger.LogInformation("已清理失效的推送订阅（用户 {UserId}）", userId);
                continue;
            }

            await pushStore.RecordResultAsync(
                subscription.Id, false, result.Error, options.DegradeAfterFailures, cancellationToken).ConfigureAwait(false);
        }
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
