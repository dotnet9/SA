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
/// <para>
/// 实施计划 §5.2 要求「<c>watchlist</c> 角色在查询管线注入自选过滤（市场列表、排行、选股器共用同一过滤器）」。
/// 集中在这里的好处是：新增入口只要调用 <see cref="AllowedAsync"/> 就不会漏掉范围限制，
/// 而不是每个入口各自记得加一次判断。
/// </para>
/// <para>
/// <b>匿名请求不受限</b>：公开市场数据对所有人开放，匿名用户没有个人范围，
/// 因此返回 <c>null</c>（不过滤）。这不构成越权——受限范围保护的是「某个角色不该看到全市场」，
/// 而匿名用户本来就没有被分配范围。反向的做法（匿名返回空集）会让公开页变成空白页。
/// </para>
/// <para>
/// <b>令牌无效不会走到这里</b>：带坏令牌的请求在认证阶段就被拒（401），
/// 因此不存在「令牌过期 → 被当作匿名 → 看到全市场」这条绕过路径。
/// </para>
/// </remarks>
public sealed class DataScopeFilter(IUserContext user, IDataScopeService scope)
{
    private readonly IUserContext _user = user;
    private readonly IDataScopeService _scope = scope;

    /// <summary>
    /// 取当前用户可见的代码集合；<c>null</c> 表示不受限（匿名，或角色范围为全市场）。
    /// </summary>
    public async Task<IReadOnlySet<string>?> AllowedAsync(CancellationToken cancellationToken = default)
    {
        var userId = _user.UserId;

        // 匿名：公开数据，不设范围
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        return await _scope.AllowedCodesAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>判断某代码是否在当前用户可见范围内。</summary>
    public static bool IsVisible(IReadOnlySet<string>? allowed, string code) =>
        allowed is null || allowed.Contains(code);
}
