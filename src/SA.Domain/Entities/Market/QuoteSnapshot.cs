namespace SA.Domain.Entities.Market;

/// <summary>
/// 个股最新行情快照（一码一行，整体替换而非逐条更新）。
/// </summary>
/// <remarks>
/// 盘中由采集任务按 <c>Collector:QuoteIntervalSeconds</c> 整体覆盖；收盘后保留收盘值。
/// 排行榜、涨跌家数、行业聚合都基于本表，因此字段以聚合所需的最小集为准（详细设计 §12）。
/// 金额统一以<b>元</b>入库，DTO 输出时换算为亿元（详细设计 §1.3）。
/// </remarks>
public sealed class QuoteSnapshot
{
    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>最新价（元）。</summary>
    public decimal Price { get; set; }

    /// <summary>涨跌额（元）。</summary>
    public decimal Change { get; set; }

    /// <summary>涨跌幅（百分数，<c>1.86</c> 表示 +1.86%）。</summary>
    public decimal Pct { get; set; }

    /// <summary>成交量（手）。</summary>
    public decimal Volume { get; set; }

    /// <summary>成交额（元）。</summary>
    public decimal Amount { get; set; }

    /// <summary>换手率（百分数）。</summary>
    public decimal Turnover { get; set; }

    /// <summary>量比。</summary>
    public decimal VolRatio { get; set; }

    /// <summary>今开（元）。</summary>
    public decimal Open { get; set; }

    /// <summary>最高（元）。</summary>
    public decimal High { get; set; }

    /// <summary>最低（元）。</summary>
    public decimal Low { get; set; }

    /// <summary>昨收（元）。</summary>
    public decimal PrevClose { get; set; }

    /// <summary>总市值（元）。</summary>
    public decimal MarketCap { get; set; }

    /// <summary>流通市值（元）。</summary>
    public decimal FloatCap { get; set; }

    /// <summary>市盈率（动态）。</summary>
    public decimal Pe { get; set; }

    /// <summary>市盈率（TTM）。</summary>
    public decimal PeTtm { get; set; }

    /// <summary>市净率。</summary>
    public decimal Pb { get; set; }

    /// <summary>数据所属业务日期。</summary>
    public DateOnly AsOf { get; set; }

    /// <summary>写入时间（用于新鲜度提示）。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
