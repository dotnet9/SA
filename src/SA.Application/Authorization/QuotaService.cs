using SA.Application.Abstractions;
using SA.Domain.Entities.Identity;

namespace SA.Application.Authorization;

/// <summary>
/// 接口配额。按账号 + 业务日计数（内存缓存 + <c>QuotaUsage</c> 表落库），
/// 超限由端点返回 <c>3002</c>（需求规格 §7.1 操作权限、详细设计 §1.2）。
/// </summary>
public sealed class QuotaService(ISessionStore sessionStore, PermissionService permissions)
{
    private readonly ISessionStore _sessionStore = sessionStore;
    private readonly PermissionService _permissions = permissions;

    /// <summary>
    /// 一次配额判定的结果。
    /// </summary>
    /// <param name="Allowed">是否允许。</param>
    /// <param name="Used">判定后（或尝试后）的已用量。</param>
    /// <param name="Limit">上限；0 表示未配置上限。</param>
    public readonly record struct QuotaCheck(bool Allowed, int Used, int Limit);

    /// <summary>
    /// 检查并占用一次配额。未配置上限（缺失或 ≤0）时直接放行且不计数。
    /// </summary>
    public async Task<QuotaCheck> ConsumeAsync(string userId, string kind, CancellationToken cancellationToken = default)
    {
        var resolved = await _permissions.ResolveAsync(userId, cancellationToken).ConfigureAwait(false);
        if (resolved is null)
        {
            return new QuotaCheck(false, 0, 0);
        }

        var limit = ResolveLimit(resolved.Quotas, kind);
        if (limit <= 0)
        {
            return new QuotaCheck(true, 0, 0);
        }

        var day = SA.Domain.Common.SaTime.Today.ToString("yyyy-MM-dd");
        var used = await _sessionStore.GetQuotaUsageAsync(userId, day, kind, cancellationToken).ConfigureAwait(false);
        if (used >= limit)
        {
            return new QuotaCheck(false, used, limit);
        }

        var updated = await _sessionStore.IncrementQuotaUsageAsync(userId, day, kind, 1, cancellationToken).ConfigureAwait(false);
        return new QuotaCheck(true, updated, limit);
    }

    /// <summary>
    /// 取当前配额状态，不占用。用于界面展示剩余额度。
    /// </summary>
    public async Task<QuotaCheck> PeekAsync(string userId, string kind, CancellationToken cancellationToken = default)
    {
        var resolved = await _permissions.ResolveAsync(userId, cancellationToken).ConfigureAwait(false);
        if (resolved is null)
        {
            return new QuotaCheck(false, 0, 0);
        }

        var limit = ResolveLimit(resolved.Quotas, kind);
        if (limit <= 0)
        {
            return new QuotaCheck(true, 0, 0);
        }

        var day = SA.Domain.Common.SaTime.Today.ToString("yyyy-MM-dd");
        var used = await _sessionStore.GetQuotaUsageAsync(userId, day, kind, cancellationToken).ConfigureAwait(false);
        return new QuotaCheck(used < limit, used, limit);
    }

    private static int ResolveLimit(IReadOnlyDictionary<string, int> quotas, string kind) =>
        quotas.TryGetValue(kind, out var value) ? value : 0;
}
