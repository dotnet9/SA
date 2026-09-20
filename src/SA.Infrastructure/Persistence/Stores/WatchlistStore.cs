using Microsoft.EntityFrameworkCore;
using SA.Application.Abstractions;
using SA.Domain.Authorization;
using SA.Domain.Common;
using SA.Domain.Entities.Watchlist;

namespace SA.Infrastructure.Persistence.Stores;

/// <summary>
/// 自选股存储。所有查询都以 <c>UserId</c> 为第一条件，越权访问在此层不可表达。
/// </summary>
public sealed class WatchlistStore(SaDbContext db) : IWatchlistStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<WatchItem>> GetItemsAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        await _db.WatchItems
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.AddedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetCodesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var codes = await _db.WatchItems
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.Code)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return codes.ToHashSet(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public Task<WatchItem?> FindItemAsync(string userId, string code, CancellationToken cancellationToken = default) =>
        _db.WatchItems.FirstOrDefaultAsync(
            item => item.UserId == userId && item.Code == code,
            cancellationToken);

    /// <inheritdoc />
    public async Task AddItemsAsync(
        IReadOnlyList<WatchItem> items,
        CancellationToken cancellationToken = default)
    {
        // 幂等：已存在的代码跳过，不覆盖用户已有的分组与备注（实施计划 §1.3「自选增删幂等」）
        if (items.Count == 0)
        {
            return;
        }

        var userId = items[0].UserId;
        var codes = items.Select(item => item.Code).Distinct(StringComparer.Ordinal).ToList();

        var existing = await _db.WatchItems
            .Where(item => item.UserId == userId && codes.Contains(item.Code))
            .Select(item => item.Code)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var known = existing.ToHashSet(StringComparer.Ordinal);
        var toAdd = items.Where(item => !known.Contains(item.Code)).ToList();

        if (toAdd.Count > 0)
        {
            await _db.WatchItems.AddRangeAsync(toAdd, cancellationToken).ConfigureAwait(false);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<int> RemoveItemsAsync(
        string userId,
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken = default)
    {
        if (codes.Count == 0)
        {
            return 0;
        }

        var targets = await _db.WatchItems
            .Where(item => item.UserId == userId && codes.Contains(item.Code))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (targets.Count == 0)
        {
            return 0;
        }

        _db.WatchItems.RemoveRange(targets);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return targets.Count;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WatchGroup>> GetGroupsAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        await _db.WatchGroups
            .AsNoTracking()
            .Where(group => group.UserId == userId)
            .OrderBy(group => group.SortOrder)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<WatchGroup?> FindGroupAsync(
        string userId,
        string groupId,
        CancellationToken cancellationToken = default) =>
        _db.WatchGroups.FirstOrDefaultAsync(
            group => group.UserId == userId && group.Id == groupId,
            cancellationToken);

    /// <inheritdoc />
    public async Task AddGroupAsync(WatchGroup group, CancellationToken cancellationToken = default)
    {
        await _db.WatchGroups.AddAsync(group, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> RemoveGroupAsync(
        string userId,
        string groupId,
        CancellationToken cancellationToken = default)
    {
        var group = await FindGroupAsync(userId, groupId, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return 0;
        }

        // 组内股票移回未分组，而不是连带删除：删分组不该悄悄丢掉自选
        var members = await _db.WatchItems
            .Where(item => item.UserId == userId && item.GroupId == groupId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var member in members)
        {
            member.GroupId = null;
        }

        _db.WatchGroups.Remove(group);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return members.Count;
    }

    /// <inheritdoc />
    public async Task ReorderAsync(
        string userId,
        IReadOnlyList<WatchOrderEntry> ordered,
        CancellationToken cancellationToken = default)
    {
        if (ordered.Count == 0)
        {
            return;
        }

        var codes = ordered.Select(entry => entry.Code).ToList();
        var items = await _db.WatchItems
            .Where(item => item.UserId == userId && codes.Contains(item.Code))
            .ToDictionaryAsync(item => item.Code, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);

        for (var index = 0; index < ordered.Count; index++)
        {
            var entry = ordered[index];
            if (!items.TryGetValue(entry.Code, out var item))
            {
                continue;
            }

            item.SortOrder = index;
            item.GroupId = entry.GroupId;

            if (entry.Note is not null)
            {
                item.Note = entry.Note;
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateItemAsync(
        string userId,
        string code,
        string? groupId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var item = await FindItemAsync(userId, code, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return false;
        }

        item.GroupId = groupId;
        if (note is not null)
        {
            item.Note = note;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}

/// <summary>
/// 数据范围判定。
/// </summary>
/// <remarks>
/// 范围由角色功能点推导（见 <see cref="DataScopes"/>），不硬编码角色名：
/// 自定义角色改权限后下一个请求即生效。自选范围的角色只能看到自选内的股票，
/// 市场排行、搜索、选股器共用这一处过滤，避免逐个入口各写一遍而漏掉其中一个。
/// </remarks>
public sealed class DataScopeService(
    IWatchlistStore watchlist,
    SA.Application.Authorization.PermissionService permissions) : IDataScopeService
{
    private readonly IWatchlistStore _watchlist = watchlist;
    private readonly SA.Application.Authorization.PermissionService _permissions = permissions;

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>?> AllowedCodesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var resolved = await _permissions.ResolveAsync(userId, cancellationToken).ConfigureAwait(false);

        // 取不到权限（用户被删或禁用）按最严处理：不给任何数据
        if (resolved is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        if (resolved.DataScope == DataScope.All)
        {
            // null 表示不受限，调用方据此跳过过滤
            return null;
        }

        return await _watchlist.GetCodesAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> IsAllowedAsync(
        string userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var allowed = await AllowedCodesAsync(userId, cancellationToken).ConfigureAwait(false);
        return allowed is null || allowed.Contains(code);
    }
}
