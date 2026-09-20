using Microsoft.EntityFrameworkCore;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Alerts;

namespace SA.Infrastructure.Persistence.Stores;

/// <summary>
/// 提醒规则与通知存储。所有查询都以 <c>UserId</c> 为第一条件。
/// </summary>
public sealed class AlertStore(SaDbContext db) : IAlertStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<AlertRule>> GetRulesAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        await _db.AlertRules
            .AsNoTracking()
            .Where(rule => rule.UserId == userId)
            .OrderByDescending(rule => rule.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<AlertRule?> FindRuleAsync(string userId, string ruleId, CancellationToken cancellationToken = default) =>
        _db.AlertRules.FirstOrDefaultAsync(
            rule => rule.UserId == userId && rule.Id == ruleId,
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AlertRule>> GetEnabledRulesAsync(CancellationToken cancellationToken = default) =>
        await _db.AlertRules
            .AsNoTracking()
            .Where(rule => rule.Enabled)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<int> CountRulesAsync(string userId, CancellationToken cancellationToken = default) =>
        _db.AlertRules.CountAsync(rule => rule.UserId == userId, cancellationToken);

    /// <inheritdoc />
    public async Task AddRuleAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        await _db.AlertRules.AddAsync(rule, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateRuleAsync(
        string userId,
        string ruleId,
        decimal? threshold,
        string? note,
        bool? enabled,
        CancellationToken cancellationToken = default)
    {
        var rule = await FindRuleAsync(userId, ruleId, cancellationToken).ConfigureAwait(false);
        if (rule is null)
        {
            return false;
        }

        if (threshold is not null)
        {
            rule.Threshold = threshold;
        }

        rule.Note = note;

        if (enabled is not null)
        {
            rule.Enabled = enabled.Value;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> RemoveRuleAsync(string userId, string ruleId, CancellationToken cancellationToken = default)
    {
        var removed = await _db.AlertRules
            .Where(rule => rule.UserId == userId && rule.Id == ruleId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        // 已产生的通知保留（历史可回溯），只是解除与规则的关联
        if (removed > 0)
        {
            await _db.Notifications
                .Where(notification => notification.UserId == userId && notification.RuleId == ruleId)
                .ExecuteUpdateAsync(setter => setter.SetProperty(notification => notification.RuleId, (string?)null), cancellationToken)
                .ConfigureAwait(false);
        }

        return removed;
    }

    /// <inheritdoc />
    public async Task<int> AddNotificationsAsync(
        IReadOnlyList<Notification> notifications,
        CancellationToken cancellationToken = default)
    {
        if (notifications.Count == 0)
        {
            return 0;
        }

        await _db.Notifications.AddRangeAsync(notifications, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return notifications.Count;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Notification>> GetNotificationsAsync(
        string userId,
        bool unreadOnly,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Notifications.AsNoTracking().Where(notification => notification.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(notification => !notification.IsRead);
        }

        return await query
            .OrderByDescending(notification => notification.Id)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<int> CountUnreadAsync(string userId, CancellationToken cancellationToken = default) =>
        _db.Notifications.CountAsync(notification => notification.UserId == userId && !notification.IsRead, cancellationToken);

    /// <inheritdoc />
    public async Task<int> MarkReadAsync(
        string userId,
        IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Notifications.Where(notification => notification.UserId == userId && !notification.IsRead);
        if (ids.Count > 0)
        {
            var target = ids.ToList();
            query = query.Where(notification => target.Contains(notification.Id));
        }

        return await query
            .ExecuteUpdateAsync(setter => setter.SetProperty(notification => notification.IsRead, true), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task MarkTriggeredAsync(
        string ruleId,
        DateTimeOffset triggeredAt,
        CancellationToken cancellationToken = default)
    {
        var rule = await _db.AlertRules.FirstOrDefaultAsync(row => row.Id == ruleId, cancellationToken).ConfigureAwait(false);
        if (rule is null)
        {
            return;
        }

        rule.LastTriggeredAt = triggeredAt;
        rule.TriggerCount += 1;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
