namespace SA.Domain.Entities.Collect;

/// <summary>
/// 回补断点。对应新增表 <c>SyncCursor</c>（实施计划 §5.3）。
/// </summary>
/// <remarks>
/// 全市场 1 年日线约 5,900 个请求，不可能在一次调度周期内跑完，也不该在重启后从零重来。
/// 因此按「数据集 + 标的」记录进度：只补尚未完成的标的，已完成的直接跳过。
/// </remarks>
public sealed class SyncCursor
{
    /// <summary>数据集名，取值见 <see cref="SyncDatasets"/>。</summary>
    public required string Dataset { get; set; }

    /// <summary>标的代码；全市场级任务用 <c>*</c>。</summary>
    public required string Code { get; set; }

    /// <summary>已入库的最后一个交易日。</summary>
    public DateOnly? LastDate { get; set; }

    /// <summary>状态：ok / pending / err。</summary>
    public required string Status { get; set; }

    /// <summary>失败原因摘要。</summary>
    public string? Note { get; set; }

    /// <summary>更新时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// 同步数据集名。
/// </summary>
public static class SyncDatasets
{
    /// <summary>日线。</summary>
    public const string Daily = "daily";

    /// <summary>技术指标。</summary>
    public const string Indicator = "indicator";
}

/// <summary>
/// 回补游标状态。
/// </summary>
public static class SyncStatuses
{
    /// <summary>已完成到 <c>LastDate</c>。</summary>
    public const string Ok = "ok";

    /// <summary>待回补 / 回补中。</summary>
    public const string Pending = "pending";

    /// <summary>失败，等待重试。</summary>
    public const string Error = "err";
}
