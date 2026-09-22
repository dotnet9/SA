using SA.Application.Abstractions;
using SA.Application.Common;
using SA.Application.Market;
using SA.Contracts.Common;
using SA.Contracts.Watchlist;
using SA.Domain.Authorization;
using SA.Domain.Common;
using SA.Domain.Entities.Watchlist;

namespace SA.Application.Watchlist;

/// <summary>
/// 自选用例：列表、增删、分组、排序。
/// </summary>
/// <remarks>
/// 三条边界（需求规格 §7.2、实施计划 §5.2）：
/// <list type="number">
/// <item>一切按用户隔离，存储层强制带 <c>UserId</c>；</item>
/// <item>数量受角色配额 <c>watchlist.max</c> 限制，超限返回 <c>3002</c>；</item>
/// <item>增删幂等：重复添加不报错也不重复插入，删除不存在的代码返回成功。</item>
/// </list>
/// </remarks>
public sealed class WatchlistService(
    IWatchlistStore store,
    IInstrumentStore instruments,
    IQuoteSnapshotStore quotes)
{
    /// <summary>
    /// 取自选列表。
    /// </summary>
    /// <param name="userId">当前用户。</param>
    /// <param name="canEdit">是否具备编辑权限（由端点按功能点判定后传入）。</param>
    /// <param name="quota">自选数量上限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<ServiceResult<WatchlistDto>> GetAsync(
        string userId,
        bool canEdit,
        int quota,
        CancellationToken cancellationToken = default)
    {
        var items = await store.GetItemsAsync(userId, cancellationToken).ConfigureAwait(false);
        var groups = await store.GetGroupsAsync(userId, cancellationToken).ConfigureAwait(false);

        var codes = items.Select(item => item.Code).ToList();
        var instrumentsByCode = await instruments.GetByCodesAsync(codes, cancellationToken).ConfigureAwait(false);
        var quotesByCode = await quotes.GetByCodesAsync(codes, cancellationToken).ConfigureAwait(false);

        DateOnly? lastUpdated = quotesByCode.Count == 0 ? null : quotesByCode.Values.Max(q => q.AsOf);

        var rows = items.Select(item =>
        {
            instrumentsByCode.TryGetValue(item.Code, out var instrument);
            quotesByCode.TryGetValue(item.Code, out var quote);

            return new WatchItemDto(
                Code: item.Code,
                Name: instrument?.Name ?? item.Code,
                GroupId: item.GroupId,
                SortOrder: item.SortOrder,
                Note: item.Note,
                AddedAt: SaTime.Format(item.AddedAt),
                Price: quote is null ? null : MarketService.Trim(quote.Price),
                Chg: quote is null ? null : MarketService.Trim(quote.Change),
                Pct: quote is null ? null : MarketService.Trim(quote.Pct),
                Volume: quote?.Volume,
                Amount: quote is null ? null : MarketService.ToYi(quote.Amount),
                Turnover: quote is null ? null : MarketService.Trim(quote.Turnover),
                // 量比 0 表示上游未提供，按 null 处理（与 MarketService 同一约定）
            VolRatio: quote is null || quote.VolRatio <= 0 ? null : MarketService.Trim(quote.VolRatio),
                Industry: instrument?.Industry,
                IsSt: instrument?.IsSt ?? false,
                AsOf: quote is null ? null : SaTime.Format(quote.AsOf));
        }).ToList();

        var groupDtos = groups
            .Select(group => new WatchGroupDto(
                group.Id,
                group.Name,
                group.SortOrder,
                items.Count(item => item.GroupId == group.Id)))
            .ToList();

        return ServiceResult<WatchlistDto>.Success(new WatchlistDto(
            groupDtos,
            rows,
            lastUpdated is null ? null : SaTime.Format(lastUpdated.Value),
            canEdit,
            quota));
    }

    /// <summary>
    /// 批量加入自选。
    /// </summary>
    public async Task<ServiceResult<int>> AddAsync(
        string userId,
        WatchAddRequest request,
        int quota,
        CancellationToken cancellationToken = default)
    {
        var codes = Normalize(request.Codes);
        if (codes.Count == 0)
        {
            return ServiceResult<int>.Fail(ErrorCode.InvalidParameter, "请提供至少一个证券代码");
        }

        // 只接受股票池里的代码：避免把拼错的代码写进自选后一直显示「无行情」
        var instrumentsByCode = await instruments.GetByCodesAsync(codes, cancellationToken).ConfigureAwait(false);
        var unknown = codes.Where(code => !instrumentsByCode.ContainsKey(code)).ToList();
        if (unknown.Count > 0)
        {
            return ServiceResult<int>.Fail(
                ErrorCode.NotFound,
                $"以下代码不在股票池中：{string.Join('、', unknown.Take(10))}");
        }

        var existing = await store.GetCodesAsync(userId, cancellationToken).ConfigureAwait(false);
        var incoming = codes.Where(code => !existing.Contains(code)).ToList();

        // 配额只对「新增」计数：重复添加不应因为已达上限而失败
        if (existing.Count + incoming.Count > quota)
        {
            return ServiceResult<int>.Fail(
                ErrorCode.QuotaExceeded,
                $"自选数量上限为 {quota} 只，当前 {existing.Count} 只，本次新增 {incoming.Count} 只");
        }

        if (incoming.Count == 0)
        {
            return ServiceResult<int>.Success(0);
        }

        var maxOrder = (await store.GetItemsAsync(userId, cancellationToken).ConfigureAwait(false))
            .Select(item => item.SortOrder)
            .DefaultIfEmpty(-1)
            .Max();

        var now = SaTime.Now;
        var items = incoming
            .Select((code, index) => new WatchItem
            {
                UserId = userId,
                Code = code,
                GroupId = string.IsNullOrWhiteSpace(request.GroupId) ? null : request.GroupId,
                SortOrder = maxOrder + 1 + index,
                AddedAt = now,
                Note = request.Note
            })
            .ToList();

        await store.AddItemsAsync(items, cancellationToken).ConfigureAwait(false);
        return ServiceResult<int>.Success(items.Count);
    }

    /// <summary>
    /// 批量移除。幂等：删除不存在的代码同样返回成功。
    /// </summary>
    public async Task<ServiceResult<int>> RemoveAsync(
        string userId,
        WatchRemoveRequest request,
        CancellationToken cancellationToken = default)
    {
        var codes = Normalize(request.Codes);
        var removed = await store.RemoveItemsAsync(userId, codes, cancellationToken).ConfigureAwait(false);
        return ServiceResult<int>.Success(removed);
    }

    /// <summary>新建分组。</summary>
    public async Task<ServiceResult<WatchGroupDto>> CreateGroupAsync(
        string userId,
        WatchGroupCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return ServiceResult<WatchGroupDto>.Fail(ErrorCode.InvalidParameter, "分组名不能为空");
        }

        if (name.Length > 32)
        {
            return ServiceResult<WatchGroupDto>.Fail(ErrorCode.InvalidParameter, "分组名不能超过 32 个字符");
        }

        var groups = await store.GetGroupsAsync(userId, cancellationToken).ConfigureAwait(false);
        if (groups.Any(group => string.Equals(group.Name, name, StringComparison.Ordinal)))
        {
            return ServiceResult<WatchGroupDto>.Fail(ErrorCode.InvalidParameter, $"分组「{name}」已存在");
        }

        var group = new WatchGroup
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = userId,
            Name = name,
            SortOrder = groups.Count == 0 ? 0 : groups.Max(g => g.SortOrder) + 1,
            CreatedAt = SaTime.Now
        };

        await store.AddGroupAsync(group, cancellationToken).ConfigureAwait(false);
        return ServiceResult<WatchGroupDto>.Success(new WatchGroupDto(group.Id, group.Name, group.SortOrder, 0));
    }

    /// <summary>删除分组：组内自选移回未分组，不连带删除。</summary>
    public async Task<ServiceResult<int>> RemoveGroupAsync(
        string userId,
        string groupId,
        CancellationToken cancellationToken = default)
    {
        var moved = await store.RemoveGroupAsync(userId, groupId, cancellationToken).ConfigureAwait(false);
        return ServiceResult<int>.Success(moved);
    }

    /// <summary>保存排序与分组。</summary>
    public async Task<ServiceResult<int>> ReorderAsync(
        string userId,
        WatchReorderRequest request,
        CancellationToken cancellationToken = default)
    {
        // 请求体里 items 缺失时不能直接遍历（会变成 500）；按参数错误返回，让前端能给出可读提示
        var entries = (request.Items ?? [])
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Code))
            .Select(entry => new WatchOrderEntry(
                entry.Code.Trim(),
                string.IsNullOrWhiteSpace(entry.GroupId) ? null : entry.GroupId,
                entry.Note))
            .ToList();

        if (entries.Count == 0)
        {
            return ServiceResult<int>.Fail(ErrorCode.InvalidParameter, "排序列表不能为空");
        }

        await store.ReorderAsync(userId, entries, cancellationToken).ConfigureAwait(false);
        return ServiceResult<int>.Success(entries.Count);
    }

    private static List<string> Normalize(IReadOnlyList<string>? codes) =>
        (codes ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
