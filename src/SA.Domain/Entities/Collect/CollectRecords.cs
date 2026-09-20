namespace SA.Domain.Entities.Collect;

/// <summary>
/// 数据源健康状态。对应详细设计 §3.3 的 <c>DataSourceStatus</c> 表。
/// 每次采集任务执行后更新，供后台「数据源监控」页与市场页新鲜度提示使用。
/// </summary>
public sealed class DataSourceStatus
{
    /// <summary>数据源名，如「东方财富 · 行情快照」。</summary>
    public required string Name { get; set; }

    /// <summary>类型：主源 / 备源。</summary>
    public string? Type { get; set; }

    /// <summary>承载的域（逗号分隔），如「行情,指数,板块」。</summary>
    public string? Domains { get; set; }

    /// <summary>状态：ok / warn / err / idle。</summary>
    public required string Status { get; set; }

    /// <summary>最近一次成功时间。</summary>
    public DateTimeOffset? LastOkAt { get; set; }

    /// <summary>最近一次耗时（毫秒）。</summary>
    public int? LatencyMs { get; set; }

    /// <summary>连续失败次数（成功后归零）。</summary>
    public int FailCount { get; set; }

    /// <summary>可用率（百分数，滚动窗口）。</summary>
    public double? UptimePct { get; set; }

    /// <summary>最近一次错误摘要。</summary>
    public string? LastError { get; set; }

    /// <summary>更新时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// 数据源状态取值。
/// </summary>
public static class DataSourceStates
{
    /// <summary>正常。</summary>
    public const string Ok = "ok";

    /// <summary>降级（连续失败未达阈值或已切备源）。</summary>
    public const string Warn = "warn";

    /// <summary>不可用。</summary>
    public const string Err = "err";

    /// <summary>尚未采集过。</summary>
    public const string Idle = "idle";
}

/// <summary>
/// 采集任务执行记录。对应详细设计 §3.3 的 <c>CollectTaskLog</c> 表。
/// </summary>
public sealed class CollectTaskLog
{
    /// <summary>自增主键。</summary>
    public long Id { get; set; }

    /// <summary>任务名，如 <c>quote-snapshot</c>。</summary>
    public required string TaskName { get; set; }

    /// <summary>实际取数的数据源。</summary>
    public string? Source { get; set; }

    /// <summary>开始时间。</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>耗时（毫秒）。</summary>
    public int? CostMs { get; set; }

    /// <summary>状态：ok / warn / err。</summary>
    public required string Status { get; set; }

    /// <summary>写入行数。</summary>
    public int? RowsWritten { get; set; }

    /// <summary>错误摘要。</summary>
    public string? Error { get; set; }

    /// <summary>重试结果摘要。</summary>
    public string? RetryResult { get; set; }
}

/// <summary>
/// 采集任务状态取值。
/// </summary>
public static class CollectStatuses
{
    /// <summary>成功。</summary>
    public const string Ok = "ok";

    /// <summary>部分成功（降级、缺字段等）。</summary>
    public const string Warn = "warn";

    /// <summary>失败。</summary>
    public const string Err = "err";
}

/// <summary>
/// 交易日历缓存。对应新增表 <c>TradingDay</c>（实施计划 §5.3）：
/// 由指数日线推导，避免为日历单独引入一个数据源。
/// </summary>
public sealed class TradingDay
{
    /// <summary>日期。</summary>
    public DateOnly Date { get; set; }

    /// <summary>是否交易日。</summary>
    public bool IsOpen { get; set; }
}
