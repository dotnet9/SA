using SA.Domain.Entities.Alerts;

namespace SA.Application.Abstractions;

/// <summary>
/// 提醒规则与通知的读写。全部按用户隔离。
/// </summary>
public interface IAlertStore
{
    /// <summary>取用户的全部规则。</summary>
    Task<IReadOnlyList<AlertRule>> GetRulesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>取规则。</summary>
    Task<AlertRule?> FindRuleAsync(string userId, string ruleId, CancellationToken cancellationToken = default);

    /// <summary>取全部启用中的规则（供评估器跨用户扫描）。</summary>
    Task<IReadOnlyList<AlertRule>> GetEnabledRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>当前规则数（配额校验用）。</summary>
    Task<int> CountRulesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>新增规则。</summary>
    Task AddRuleAsync(AlertRule rule, CancellationToken cancellationToken = default);

    /// <summary>更新规则的可变字段。</summary>
    Task<bool> UpdateRuleAsync(
        string userId,
        string ruleId,
        decimal? threshold,
        string? note,
        bool? enabled,
        CancellationToken cancellationToken = default);

    /// <summary>删除规则，返回实际删除条数。</summary>
    Task<int> RemoveRuleAsync(string userId, string ruleId, CancellationToken cancellationToken = default);

    /// <summary>批量写入通知，返回写入条数。</summary>
    Task<int> AddNotificationsAsync(IReadOnlyList<Notification> notifications, CancellationToken cancellationToken = default);

    /// <summary>取用户通知（按时间倒序）。</summary>
    Task<IReadOnlyList<Notification>> GetNotificationsAsync(
        string userId,
        bool unreadOnly,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>未读数量。</summary>
    Task<int> CountUnreadAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>标记已读；<paramref name="ids"/> 为空表示全部标记已读。</summary>
    Task<int> MarkReadAsync(string userId, IReadOnlyCollection<long> ids, CancellationToken cancellationToken = default);

    /// <summary>记录一次触发（更新时间与次数）。</summary>
    Task MarkTriggeredAsync(string ruleId, DateTimeOffset triggeredAt, CancellationToken cancellationToken = default);
}
