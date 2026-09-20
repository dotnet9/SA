namespace SA.Domain.Entities.Equity;

/// <summary>
/// 十大股东 / 十大流通股东的一条记录。
/// </summary>
/// <remarks>
/// 两张表（<c>RPT_F10_EH_HOLDERS</c> 与 <c>RPT_F10_EH_FREEHOLDERS</c>）字段高度重合但口径不同：
/// 前者是全部股份（含限售），后者只含流通股，且披露日期通常更晚。
/// 用 <see cref="IsFreeFloat"/> 区分而不是建两张表：页面总是两者并排展示，合并后查询更简单，
/// 也避免同一套解析逻辑写两遍。
/// </remarks>
public sealed class TopHolder
{
    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>报告期（上游 END_DATE）。</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>股东排名（1 起）。</summary>
    public int Rank { get; set; }

    /// <summary>是否为「十大流通股东」口径。</summary>
    public bool IsFreeFloat { get; set; }

    /// <summary>股东名称。</summary>
    public required string HolderName { get; set; }

    /// <summary>持股数量（股）。</summary>
    public decimal HoldNum { get; set; }

    /// <summary>持股占总股本比例（百分数）。</summary>
    public decimal? HoldRatio { get; set; }

    /// <summary>持股占流通股比例（百分数，仅流通口径有值）。</summary>
    public decimal? FreeHoldRatio { get; set; }

    /// <summary>较上期变动文案（上游给「不变」「新进」这类文字，直接透传）。</summary>
    public string? HoldChange { get; set; }

    /// <summary>股东类型（流通口径有值），如「投资公司」。</summary>
    public string? HolderType { get; set; }

    /// <summary>股份类型，如「流通A股」「限售流通股」。</summary>
    public string? SharesType { get; set; }

    /// <summary>持股市值（元）。</summary>
    public decimal? MarketCap { get; set; }

    /// <summary>公告日期。</summary>
    public DateOnly? NoticeDate { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// 股东户数（一期）。
/// </summary>
/// <remarks>
/// 户数下降通常意味着筹码集中，因此页面按时间展示趋势而不是只给一个数字。
/// 上游同时给出户均持股与户均市值，均按「元 / 股」原样保存，接口层再换算。
/// </remarks>
public sealed class HolderCount
{
    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>报告期（上游 END_DATE）。</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>股东户数。</summary>
    public int HolderNum { get; set; }

    /// <summary>上一期股东户数。</summary>
    public int? PreviousHolderNum { get; set; }

    /// <summary>户数变化量（正为增加）。</summary>
    public int? HolderNumChange { get; set; }

    /// <summary>户数变化比例（百分数）。</summary>
    public decimal? HolderNumRatio { get; set; }

    /// <summary>户均持股数（股）。</summary>
    public decimal? AvgHoldNum { get; set; }

    /// <summary>户均持股市值（元）。</summary>
    public decimal? AvgMarketCap { get; set; }

    /// <summary>总市值（元）。</summary>
    public decimal? TotalMarketCap { get; set; }

    /// <summary>总股本（股）。</summary>
    public decimal? TotalShares { get; set; }

    /// <summary>股本变动原因，如「股权激励 其他事件」。</summary>
    public string? ChangeReason { get; set; }

    /// <summary>报告期文案，如「2026 2季末」。</summary>
    public string? ReportName { get; set; }

    /// <summary>公告日期。</summary>
    public DateOnly? NoticeDate { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// 股权质押（一日一期）。
/// </summary>
/// <remarks>
/// <b>单位已在实测中核对</b>（300750，2026-09-18）：<c>REPURCHASE_BALANCE</c> 为<b>万股</b>
/// （2515 万股），<c>PLEDGE_MARKET_CAP</c> 为<b>万元</b>（759,404.25 万元 ≈ 75.94 亿元，
/// 与 2515 万股 × 301.95 元一致），<c>PLEDGE_RATIO</c> 为百分数。
/// </remarks>
public sealed class PledgeStat
{
    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>数据日期。</summary>
    public DateOnly TradeDate { get; set; }

    /// <summary>质押比例（百分数，占总股本）。</summary>
    public decimal? PledgeRatio { get; set; }

    /// <summary>质押股数（万股）。</summary>
    public decimal? PledgeSharesWan { get; set; }

    /// <summary>质押笔数。</summary>
    public int? PledgeDealNum { get; set; }

    /// <summary>质押市值（万元）。</summary>
    public decimal? PledgeMarketCapWan { get; set; }

    /// <summary>行业。</summary>
    public string? Industry { get; set; }

    /// <summary>近一年涨跌幅（百分数）。</summary>
    public decimal? Year1ChangePercent { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
