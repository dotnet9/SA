namespace SA.Domain.Entities.Watchlist;

/// <summary>
/// 自选分组。对应详细设计 §3.2 的 <c>WatchGroup</c>。
/// </summary>
public sealed class WatchGroup
{
    /// <summary>分组 Id。</summary>
    public required string Id { get; set; }

    /// <summary>所属用户。</summary>
    public required string UserId { get; set; }

    /// <summary>分组名。</summary>
    public required string Name { get; set; }

    /// <summary>展示顺序（升序）。</summary>
    public int SortOrder { get; set; }

    /// <summary>创建时间。</summary>
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// 自选项。对应详细设计 §3.2 的 <c>WatchItem</c>：以（用户, 代码）为主键，同一只股票在一个账号下只出现一次。
/// </summary>
public sealed class WatchItem
{
    /// <summary>所属用户。</summary>
    public required string UserId { get; set; }

    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>所属分组；为 null 表示「未分组」。</summary>
    public string? GroupId { get; set; }

    /// <summary>组内排序。</summary>
    public int SortOrder { get; set; }

    /// <summary>加入时间。</summary>
    public DateTimeOffset AddedAt { get; set; }

    /// <summary>备注（原型支持一句话备注，用于记录关注理由）。</summary>
    public string? Note { get; set; }
}
