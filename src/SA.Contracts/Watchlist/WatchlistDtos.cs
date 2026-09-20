namespace SA.Contracts.Watchlist;

/// <summary>
/// 自选分组。
/// </summary>
/// <param name="Id">分组 Id。</param>
/// <param name="Name">分组名。</param>
/// <param name="SortOrder">展示顺序。</param>
/// <param name="Count">组内股票数（含子项计数，便于界面直接显示）。</param>
public sealed record WatchGroupDto(string Id, string Name, int SortOrder, int Count);

/// <summary>
/// 自选股一行：行情 + 分组 + 备注。
/// </summary>
/// <param name="Code">代码。</param>
/// <param name="Name">名称。</param>
/// <param name="GroupId">所属分组；null 为未分组。</param>
/// <param name="SortOrder">组内顺序。</param>
/// <param name="Note">备注。</param>
/// <param name="AddedAt">加入时间。</param>
/// <param name="Price">最新价；无行情为 null。</param>
/// <param name="Chg">涨跌额。</param>
/// <param name="Pct">涨跌幅。</param>
/// <param name="Volume">成交量（手）。</param>
/// <param name="Amount">成交额（亿元）。</param>
/// <param name="Turnover">换手率（百分数）。</param>
/// <param name="VolRatio">量比。</param>
/// <param name="Industry">东财行业。</param>
/// <param name="IsSt">是否 ST / 退市风险。</param>
/// <param name="AsOf">行情口径日。</param>
public sealed record WatchItemDto(
    string Code,
    string Name,
    string? GroupId,
    int SortOrder,
    string? Note,
    string AddedAt,
    decimal? Price,
    decimal? Chg,
    decimal? Pct,
    decimal? Volume,
    decimal? Amount,
    decimal? Turnover,
    decimal? VolRatio,
    string? Industry,
    bool IsSt,
    string? AsOf);

/// <summary>
/// 自选列表响应。
/// </summary>
/// <param name="Groups">分组（含「未分组」时其 Id 为 null，界面按排序自行插入）。</param>
/// <param name="Items">自选项。</param>
/// <param name="AsOf">行情口径日。</param>
/// <param name="CanEdit">当前账号是否具备 <c>watchlist.edit</c>（界面据此隐藏编辑入口）。</param>
/// <param name="Quota">自选数量上限（来自角色配额，超限时接口返回 3002）。</param>
public sealed record WatchlistDto(
    IReadOnlyList<WatchGroupDto> Groups,
    IReadOnlyList<WatchItemDto> Items,
    string? AsOf,
    bool CanEdit,
    int Quota);

/// <summary>新增自选请求。</summary>
/// <param name="Codes">证券代码集合（支持批量）。</param>
/// <param name="GroupId">目标分组；null 为未分组。</param>
/// <param name="Note">备注。</param>
public sealed record WatchAddRequest(IReadOnlyList<string> Codes, string? GroupId, string? Note);

/// <summary>删除自选请求（批量）。</summary>
/// <param name="Codes">证券代码集合。</param>
public sealed record WatchRemoveRequest(IReadOnlyList<string> Codes);

/// <summary>新建分组请求。</summary>
/// <param name="Name">分组名。</param>
public sealed record WatchGroupCreateRequest(string Name);

/// <summary>重命名分组请求。</summary>
/// <param name="Name">新分组名。</param>
public sealed record WatchGroupRenameRequest(string Name);

/// <summary>排序/分组请求：按数组顺序写入 <c>SortOrder</c>。</summary>
/// <param name="Items">按目标顺序排列的条目。</param>
public sealed record WatchReorderRequest(IReadOnlyList<WatchReorderEntry> Items);

/// <summary>排序条目。</summary>
/// <param name="Code">证券代码。</param>
/// <param name="GroupId">目标分组；null 为未分组。</param>
/// <param name="Note">备注；null 表示不改动。</param>
public sealed record WatchReorderEntry(string Code, string? GroupId, string? Note);
