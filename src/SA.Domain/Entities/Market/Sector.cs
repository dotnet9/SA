namespace SA.Domain.Entities.Market;

/// <summary>
/// 东财行业板块快照（行业热力与行业排行）。
/// </summary>
/// <remarks>
/// 板块码为东财 <c>BK</c> 码（如 <c>BK1201</c>）。行业口径说明见 <see cref="Instrument"/>。
/// </remarks>
public sealed class Sector
{
    /// <summary>板块码，如 <c>BK1201</c>。</summary>
    public required string Code { get; set; }

    /// <summary>板块名，如「电子」。</summary>
    public required string Name { get; set; }

    /// <summary>板块涨跌幅（百分数）。</summary>
    public decimal Pct { get; set; }

    /// <summary>主力净流入（元）。</summary>
    public decimal MainNet { get; set; }

    /// <summary>上涨家数。</summary>
    public int UpCount { get; set; }

    /// <summary>下跌家数。</summary>
    public int DownCount { get; set; }

    /// <summary>领涨股名称。</summary>
    public string? LeaderName { get; set; }

    /// <summary>领涨股代码。</summary>
    public string? LeaderCode { get; set; }

    /// <summary>板块市盈率（东财 <c>f115</c>，缺失时为 0）。</summary>
    public decimal Pe { get; set; }

    /// <summary>数据所属业务日期。</summary>
    public DateOnly AsOf { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
