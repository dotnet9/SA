namespace SA.Domain.Entities.Market;

/// <summary>
/// 指数最新快照（市场概览顶部卡片）。
/// </summary>
public sealed class IndexQuote
{
    /// <summary>指数代码，如 <c>000001</c>。</summary>
    public required string Code { get; set; }

    /// <summary>指数名称，如「上证指数」。</summary>
    public required string Name { get; set; }

    /// <summary>东财市场标志：1=沪市，0=深市。</summary>
    public int Market { get; set; }

    /// <summary>展示顺序（与市场概览卡片顺序一致）。</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 是否在市场概览的指数卡区展示。
    /// </summary>
    /// <remarks>
    /// 沪深 300 也一并采集（趋势页的相对强弱基准），但不进概览卡片区，
    /// 用显式标记而不是「取前 N 条」来区分，避免日后插入新指数时布局悄悄错位。
    /// </remarks>
    public bool Displayed { get; set; } = true;

    /// <summary>最新点位。</summary>
    public decimal Price { get; set; }

    /// <summary>涨跌点数。</summary>
    public decimal Change { get; set; }

    /// <summary>涨跌幅（百分数）。</summary>
    public decimal Pct { get; set; }

    /// <summary>成交量（手）。</summary>
    public decimal Volume { get; set; }

    /// <summary>成交额（元）。</summary>
    public decimal Amount { get; set; }

    /// <summary>数据所属业务日期。</summary>
    public DateOnly AsOf { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
