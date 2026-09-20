using SA.Application.Abstractions;

namespace SA.Application.Authorization;

/// <summary>
/// 当前请求的用户标识。由宿主层从认证声明中读取，供应用层做数据范围过滤。
/// </summary>
public interface IUserContext
{
    /// <summary>当前用户 Id；未认证时为 null。</summary>
    string? UserId { get; }
}

/// <summary>
/// 数据范围过滤器：把「当前用户能看哪些代码」收敛成一处，供排行、搜索、选股等入口复用。
/// </summary>
/// <remarks>
/// 实施计划 §5.2 要求「<c>watchlist</c> 角色在查询管线注入自选过滤（市场列表、排行、选股器共用同一过滤器）」。
/// 集中在这里的好处是：新增入口只要调用 <see cref="AllowedAsync"/> 就不会漏掉范围限制，
/// 而不是每个入口各自记得加一次判断。
/// </remarks>
public sealed class DataScopeFilter(IUserContext user, IDataScopeService scope)
{
    private readonly IUserContext _user = user;
    private readonly IDataScopeService _scope = scope;

    /// <summary>
    /// 取当前用户可见的代码集合；<c>null</c> 表示不受限。
    /// </summary>
    public async Task<IReadOnlySet<string>?> AllowedAsync(CancellationToken cancellationToken = default)
    {
        var userId = _user.UserId;

        // 未认证理论上到不了应用层（有全局兜底策略）；真到了就按最严处理
        if (string.IsNullOrEmpty(userId))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return await _scope.AllowedCodesAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>判断某代码是否在当前用户可见范围内。</summary>
    public static bool IsVisible(IReadOnlySet<string>? allowed, string code) =>
        allowed is null || allowed.Contains(code);
}
