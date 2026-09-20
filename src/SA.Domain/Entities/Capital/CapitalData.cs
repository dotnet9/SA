namespace SA.Domain.Entities.Capital;

/// <summary>
/// 个股逐日资金流（东财 <c>stock/fflow/daykline</c>）。
/// </summary>
/// <remarks>
/// <b>字段序经算术自洽核对</b>（300750，2026-09-18）：
/// <c>date, 主力净额, 小单净额, 中单净额, 大单净额, 超大单净额, 主力占比%, 小单%, 中单%, 大单%, 超大单%, 收盘价, 涨跌幅</c>。
/// 校验：超大单（-295,559,840）+ 大单（-407,389,824）= 主力净额（-702,949,664）✓；
/// 主力 + 中单 + 小单 = 0（-702,949,664 − 9,643,232 + 712,592,896 ≈ 0）✓。
/// 金额单位为<b>元</b>，比率为百分数。
/// </remarks>
public sealed class FundFlowDaily
{
    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>交易日。</summary>
    public DateOnly Date { get; set; }

    /// <summary>主力净额（元，= 大单 + 超大单）。</summary>
    public decimal MainNet { get; set; }

    /// <summary>超大单净额（元）。</summary>
    public decimal SuperLargeNet { get; set; }

    /// <summary>大单净额（元）。</summary>
    public decimal LargeNet { get; set; }

    /// <summary>中单净额（元）。</summary>
    public decimal MediumNet { get; set; }

    /// <summary>小单净额（元）。</summary>
    public decimal SmallNet { get; set; }

    /// <summary>主力净占比（百分数）。</summary>
    public decimal? MainRatio { get; set; }

    /// <summary>收盘价（元）。</summary>
    public decimal? Close { get; set; }

    /// <summary>涨跌幅（百分数）。</summary>
    public decimal? ChangePercent { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// 龙虎榜记录（东财 <c>RPT_DAILYBILLBOARD_DETAILSNEW</c>）。
/// </summary>
public sealed class BillboardRecord
{
    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>上榜日期。</summary>
    public DateOnly TradeDate { get; set; }

    /// <summary>上榜原因（上游原文，如「日涨幅偏离值达 7% 的前五只证券」）。</summary>
    public string? Reason { get; set; }

    /// <summary>榜单解读（上游原文，如「1家机构买入，成功率 52.33%」）。</summary>
    public string? Explain { get; set; }

    /// <summary>收盘价（元）。</summary>
    public decimal? Close { get; set; }

    /// <summary>当日涨跌幅（百分数）。</summary>
    public decimal? ChangePercent { get; set; }

    /// <summary>换手率（百分数）。</summary>
    public decimal? TurnoverRate { get; set; }

    /// <summary>龙虎榜净买入（元，正为净买入）。</summary>
    public decimal? NetAmount { get; set; }

    /// <summary>龙虎榜买入额（元）。</summary>
    public decimal? BuyAmount { get; set; }

    /// <summary>龙虎榜卖出额（元）。</summary>
    public decimal? SellAmount { get; set; }

    /// <summary>龙虎榜成交额（元）。</summary>
    public decimal? DealAmount { get; set; }

    /// <summary>次日涨跌幅（百分数，上游回填）。</summary>
    public decimal? Next1Change { get; set; }

    /// <summary>后 5 日涨跌幅（百分数）。</summary>
    public decimal? Next5Change { get; set; }

    /// <summary>后 10 日涨跌幅（百分数）。</summary>
    public decimal? Next10Change { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// 大宗交易（东财 <c>RPT_DATA_BLOCKTRADE</c>）。
/// </summary>
public sealed class BlockTrade
{
    /// <summary>自增主键：同一天可能有多笔，且买方/卖方名称可能相同，因此不用业务字段做主键。</summary>
    public long Id { get; set; }

    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>交易日期。</summary>
    public DateOnly TradeDate { get; set; }

    /// <summary>成交价（元）。</summary>
    public decimal? DealPrice { get; set; }

    /// <summary>折溢价率（百分数，负为折价）。</summary>
    public decimal? PremiumRatio { get; set; }

    /// <summary>成交量（股）。</summary>
    public decimal? DealVolume { get; set; }

    /// <summary>成交额（元）。</summary>
    public decimal? DealAmount { get; set; }

    /// <summary>买方营业部。</summary>
    public string? BuyerName { get; set; }

    /// <summary>卖方营业部。</summary>
    public string? SellerName { get; set; }

    /// <summary>当日收盘价（元）。</summary>
    public decimal? Close { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// 个股两融明细（东财 <c>RPTA_WEB_RZRQ_GGMX</c>）。
/// </summary>
/// <remarks>
/// 金额单位为元、数量单位为股，比率字段为百分数（如 <c>RZYEZB</c> 融资余额占流通市值比）。
/// </remarks>
public sealed class MarginDetail
{
    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>交易日。</summary>
    public DateOnly Date { get; set; }

    /// <summary>融资余额（元）。</summary>
    public decimal? FinanceBalance { get; set; }

    /// <summary>融资买入额（元）。</summary>
    public decimal? FinanceBuy { get; set; }

    /// <summary>融资净买入（元）。</summary>
    public decimal? FinanceNetBuy { get; set; }

    /// <summary>融券余额（元）。</summary>
    public decimal? LoanBalance { get; set; }

    /// <summary>融券余量（股）。</summary>
    public decimal? LoanVolume { get; set; }

    /// <summary>融资融券余额（元）。</summary>
    public decimal? TotalBalance { get; set; }

    /// <summary>融资余额占流通市值比（百分数）。</summary>
    public decimal? FinanceBalanceRatio { get; set; }

    /// <summary>收盘价（元）。</summary>
    public decimal? Close { get; set; }

    /// <summary>当日涨跌幅（百分数）。</summary>
    public decimal? ChangePercent { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// 陆股通持股（东财 <c>RPT_MUTUAL_HOLDRANK_NEW</c>）。
/// </summary>
/// <remarks>
/// <b>披露频率是季度</b>（实测 <c>DATE_TYPE</c> 为「2026二季末」），因此它只能说明「陆股通整体在这个季度
/// 增持还是减持」，不能当作逐日北向净买入使用——本项目的北向净流入仍为空态（公开接口已不再提供）。
/// </remarks>
public sealed class NorthboundHolding
{
    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>持股日期（= 报告期）。</summary>
    public DateOnly HoldDate { get; set; }

    /// <summary>报告期文案，如「2026二季末」。</summary>
    public string? DateType { get; set; }

    /// <summary>持股数量（股）。</summary>
    public decimal? HoldShares { get; set; }

    /// <summary>上期持股数量（股）。</summary>
    public decimal? PreviousHoldShares { get; set; }

    /// <summary>持股数量变化（股）。</summary>
    public decimal? AddShares { get; set; }

    /// <summary>持股数量变化幅度（百分数）。</summary>
    public decimal? AddSharesAmp { get; set; }

    /// <summary>持股市值（元）。</summary>
    public decimal? HoldMarketCap { get; set; }

    /// <summary>持股机构家数。</summary>
    public int? OrgQuantity { get; set; }

    /// <summary>上期机构家数。</summary>
    public int? PreviousOrgQuantity { get; set; }

    /// <summary>占流通股比例（百分数）。</summary>
    public decimal? FreeSharesRatio { get; set; }

    /// <summary>占总股本比例（百分数）。</summary>
    public decimal? TotalSharesRatio { get; set; }

    /// <summary>所属行业。</summary>
    public string? Industry { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
