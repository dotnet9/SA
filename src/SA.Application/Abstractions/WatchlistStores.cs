using SA.Domain.Entities.Watchlist;

namespace SA.Application.Abstractions;

/// <summary>
/// 自选股读写。全部按用户隔离：任何查询都必须带 <c>UserId</c>，
/// 越权访问在存储层就无路可走（需求规格 §7.3）。
/// </summary>
public interface IWatchlistStore
{
    /// <summary>取用户的全部自选项（含分组信息），按组序与组内序排列。</summary>
    Task<IReadOnlyList<WatchItem>> GetItemsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>取用户的自选代码集合（数据范围过滤与推送授权用）。</summary>
    Task<IReadOnlySet<string>> GetCodesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>取单个自选项。</summary>
    Task<WatchItem?> FindItemAsync(string userId, string code, CancellationToken cancellationToken = default);

    /// <summary>新增自选项。</summary>
    Task AddItemsAsync(IReadOnlyList<WatchItem> items, CancellationToken cancellationToken = default);

    /// <summary>删除自选项，返回实际删除条数。</summary>
    Task<int> RemoveItemsAsync(string userId, IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default);

    /// <summary>取用户的分组。</summary>
    Task<IReadOnlyList<WatchGroup>> GetGroupsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>取单个分组。</summary>
    Task<WatchGroup?> FindGroupAsync(string userId, string groupId, CancellationToken cancellationToken = default);

    /// <summary>新增分组。</summary>
    Task AddGroupAsync(WatchGroup group, CancellationToken cancellationToken = default);

    /// <summary>删除分组，并把组内自选项移到未分组。</summary>
    Task<int> RemoveGroupAsync(string userId, string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重排自选项：按给定顺序写入 <c>SortOrder</c>，并可选地更新所属分组。
    /// </summary>
    /// <param name="userId">用户。</param>
    /// <param name="ordered">按目标顺序排列的代码；也可以携带分组与备注。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task ReorderAsync(
        string userId,
        IReadOnlyList<WatchOrderEntry> ordered,
        CancellationToken cancellationToken = default);

    /// <summary>更新某个自选项的分组与备注。</summary>
    Task<bool> UpdateItemAsync(
        string userId,
        string code,
        string? groupId,
        string? note,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 自选排序/分组的一次更新项。
/// </summary>
/// <param name="Code">证券代码。</param>
/// <param name="GroupId">目标分组；null 表示未分组。</param>
/// <param name="Note">备注；null 表示不改动。</param>
public readonly record struct WatchOrderEntry(string Code, string? GroupId, string? Note);

/// <summary>
/// 数据范围判定。角色带 <c>data.scope.watchlist</c> 时只能看到自选范围内的股票
/// （需求规格 §7.2、实施计划 §5.2）。
/// </summary>
/// <remarks>
/// 市场排行、搜索、选股器共用这一处过滤，避免每个入口各写一遍而漏掉其中一个。
/// </remarks>
public interface IDataScopeService
{
    /// <summary>
    /// 取该用户可见的代码集合。
    /// </summary>
    /// <returns>
    /// <c>null</c> 表示不受限（全市场）；空集合表示受限但自选为空（界面应提示先添加自选）。
    /// </returns>
    Task<IReadOnlySet<string>?> AllowedCodesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>判断某代码是否在用户可见范围内。</summary>
    Task<bool> IsAllowedAsync(string userId, string code, CancellationToken cancellationToken = default);
}
