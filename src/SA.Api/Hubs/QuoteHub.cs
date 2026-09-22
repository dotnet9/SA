using Microsoft.AspNetCore.SignalR;
using SA.Application.Abstractions;
using SA.Application.Authorization;
using SA.Contracts.Realtime;

namespace SA.Api.Hubs;

/// <summary>
/// 自选股行情推送中心（<c>/hubs/quotes</c>）。
/// </summary>
/// <remarks>
/// <para>
/// <b>权限边界</b>：客户端提交的代码先经数据范围过滤再入库订阅（见 <see cref="Subscribe"/>）。
/// 因此受限角色即使手工构造订阅请求，也只能收到自己自选范围内的行情。
/// </para>
/// <para>
/// <b>推送节奏</b>：实际取数与发送由 <c>QuotePushService</c> 按连接节流执行，
/// 中心只负责登记订阅意图，不在每连接里请求上游。
/// </para>
/// </remarks>
public sealed class QuoteHub(
    SubscriptionRegistry subscriptions,
    DataScopeFilter scopeFilter,
    ILogger<QuoteHub> logger) : Hub
{
    /// <summary>连接建立：登记连接。</summary>
    public override async Task OnConnectedAsync()
    {
        subscriptions.Track(Context.ConnectionId, Context.UserIdentifier);
        logger.LogDebug("实时连接建立：{ConnectionId}（用户 {UserId}）", Context.ConnectionId, Context.UserIdentifier);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <summary>连接断开：清理订阅。</summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        subscriptions.Remove(Context.ConnectionId);
        logger.LogDebug("实时连接断开：{ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    /// <summary>
    /// 订阅行情。
    /// </summary>
    /// <param name="codes">证券代码集合。</param>
    /// <returns>实际被接受的代码；被数据范围过滤掉的不在其中。</returns>
    public async Task<IReadOnlyList<string>> Subscribe(string[] codes)
    {
        if (codes is null || codes.Length == 0)
        {
            return [];
        }

        var allowed = await scopeFilter.AllowedAsync(Context.ConnectionAborted).ConfigureAwait(false);

        // 不受限（null）时原样接受；受限时取交集，未授权的代码静默丢弃而不是报错
        var accepted = allowed is null
            ? codes
            : codes.Where(code => allowed.Contains(code)).ToArray();

        var added = subscriptions.Subscribe(Context.ConnectionId, accepted);

        if (accepted.Length < codes.Length)
        {
            logger.LogDebug(
                "订阅请求中 {Rejected} 个代码超出该账号的数据范围，已忽略（连接 {ConnectionId}）",
                codes.Length - accepted.Length, Context.ConnectionId);
        }

        return added;
    }

    /// <summary>取消订阅。</summary>
    /// <param name="codes">证券代码集合。</param>
    public Task Unsubscribe(string[] codes)
    {
        if (codes is { Length: > 0 })
        {
            subscriptions.Unsubscribe(Context.ConnectionId, codes);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 设置推送间隔（秒）。取值收敛到 3 / 5 / 10（详细设计 §7.1）。
    /// </summary>
    /// <param name="seconds">期望间隔。</param>
    /// <returns>实际生效的间隔。</returns>
    public Task<int> SetInterval(int seconds) =>
        Task.FromResult(subscriptions.SetInterval(Context.ConnectionId, seconds));

    /// <summary>当前连接的订阅列表（前端重连后核对用）。</summary>
    public Task<IReadOnlyList<string>> Subscriptions() =>
        Task.FromResult(subscriptions.CodesOf(Context.ConnectionId));
}

/// <summary>
/// 在线状态跟踪。后台「登录与安全」与会话管理使用（第 12 批接页面）。
/// </summary>
public sealed class PresenceTracker(SubscriptionRegistry subscriptions)
{
    /// <summary>当前实时连接数。</summary>
    public int Connections => subscriptions.ConnectionCount;

    /// <summary>按用户统计连接数，用于「在线状态」。</summary>
    public IReadOnlyDictionary<string, int> ConnectionsByUser()
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (_, userId, _) in subscriptions.Presence())
        {
            if (userId is null)
            {
                continue;
            }

            result[userId] = result.TryGetValue(userId, out var count) ? count + 1 : 1;
        }

        return result;
    }
}
