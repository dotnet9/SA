using System.Collections.Concurrent;
using SA.Contracts.Realtime;
using SA.Domain.Common;

namespace SA.Api.Hubs;

/// <summary>
/// 行情订阅登记表：记录每条连接订阅了哪些代码、推送间隔，以及该连接的数据范围。
/// </summary>
/// <remarks>
/// <para>
/// <b>按连接保存数据范围</b>：订阅时的可见代码集合在订阅那一刻确定并固化下来。
/// 推送时只发 <c>订阅集合 ∩ 可见集合</c>，因此「A 用户不能因为订阅了 B 用户的自选而看到 B 的数据」
/// 这件事在推送链路上就已成立，而不是靠前端过滤（需求规格 §7.3）。
/// </para>
/// <para>
/// 单例：连接是跨请求的，登记表必须与请求生命周期无关。
/// </para>
/// </remarks>
public sealed class SubscriptionRegistry
{
    /// <summary>单条连接的订阅状态。</summary>
    private sealed class Entry
    {
        /// <summary>订阅的代码（已按可见范围过滤）。</summary>
        public HashSet<string> Codes { get; } = new(StringComparer.Ordinal);

        /// <summary>推送间隔（秒）。</summary>
        public int IntervalSeconds { get; set; } = PushIntervals.Default;

        /// <summary>上次推送时间，用于按连接节流。</summary>
        public DateTimeOffset LastPushedAt { get; set; } = DateTimeOffset.MinValue;

        /// <summary>连接所属用户。</summary>
        public string? UserId { get; set; }

        /// <summary>连接建立时间。</summary>
        public DateTimeOffset ConnectedAt { get; init; } = SaTime.Now;
    }

    private readonly ConcurrentDictionary<string, Entry> _connections = new(StringComparer.Ordinal);

    /// <summary>当前连接数（在线会话数与后台监控使用）。</summary>
    public int ConnectionCount => _connections.Count;

    /// <summary>当前订阅的代码并集（推送时一次性取数，避免每连接各请求一次上游）。</summary>
    public IReadOnlyList<string> SubscribedCodes()
    {
        var union = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in _connections.Values)
        {
            union.UnionWith(entry.Codes);
        }

        return union.ToList();
    }

    /// <summary>
    /// 登记一条连接。
    /// </summary>
    public void Track(string connectionId, string? userId) =>
        _connections[connectionId] = new Entry { UserId = userId };

    /// <summary>
    /// 移除一条连接（断开时调用）。
    /// </summary>
    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    /// <summary>
    /// 订阅代码。
    /// </summary>
    /// <param name="connectionId">连接 Id。</param>
    /// <param name="codes">要订阅的代码（调用方须先用数据范围过滤）。</param>
    /// <returns>实际加入订阅的代码。</returns>
    public IReadOnlyList<string> Subscribe(string connectionId, IEnumerable<string> codes)
    {
        if (!_connections.TryGetValue(connectionId, out var entry))
        {
            return [];
        }

        var added = new List<string>();
        foreach (var code in codes)
        {
            if (!string.IsNullOrWhiteSpace(code) && entry.Codes.Add(code.Trim()))
            {
                added.Add(code.Trim());
            }
        }

        return added;
    }

    /// <summary>取消订阅。</summary>
    public void Unsubscribe(string connectionId, IEnumerable<string> codes)
    {
        if (!_connections.TryGetValue(connectionId, out var entry))
        {
            return;
        }

        foreach (var code in codes)
        {
            entry.Codes.Remove(code);
        }
    }

    /// <summary>设置推送间隔（收敛到允许取值）。</summary>
    public int SetInterval(string connectionId, int seconds)
    {
        if (!_connections.TryGetValue(connectionId, out var entry))
        {
            return PushIntervals.Default;
        }

        entry.IntervalSeconds = PushIntervals.Normalize(seconds);
        return entry.IntervalSeconds;
    }

    /// <summary>取某连接的订阅快照。</summary>
    public IReadOnlyList<string> CodesOf(string connectionId) =>
        _connections.TryGetValue(connectionId, out var entry) ? entry.Codes.ToList() : [];

    /// <summary>按连接取「本次该推送什么」：间隔已到且有订阅的连接。</summary>
    /// <returns>连接 Id 与其本次要推送的代码。</returns>
    public IReadOnlyList<(string ConnectionId, IReadOnlyList<string> Codes)> DueTargets(DateTimeOffset now)
    {
        var targets = new List<(string, IReadOnlyList<string>)>();

        foreach (var (connectionId, entry) in _connections)
        {
            if (entry.Codes.Count == 0)
            {
                continue;
            }

            if (now - entry.LastPushedAt < TimeSpan.FromSeconds(entry.IntervalSeconds))
            {
                continue;
            }

            entry.LastPushedAt = now;
            targets.Add((connectionId, entry.Codes.ToList()));
        }

        return targets;
    }

    /// <summary>在线会话（后台「登录与安全」页展示实时连接）。</summary>
    public IReadOnlyList<(string ConnectionId, string? UserId, DateTimeOffset ConnectedAt)> Presence() =>
        _connections.Select(pair => (pair.Key, pair.Value.UserId, pair.Value.ConnectedAt)).ToList();
}
