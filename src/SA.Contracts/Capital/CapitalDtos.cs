using SA.Domain.Entities.Capital;

namespace SA.Contracts.Capital;

/// <summary>资金流一期（金额为亿元，比率为百分数）。</summary>
/// <param name="Date">交易日。</param>
/// <param name="MainNet">主力净额（亿元）。</param>
/// <param name="SuperLargeNet">超大单净额（亿元）。</param>
/// <param name="LargeNet">大单净额（亿元）。</param>
/// <param name="MediumNet">中单净额（亿元）。</param>
/// <param name="SmallNet">小单净额（亿元）。</param>
/// <param name="MainRatio">主力净占比（百分数）。</param>
/// <param name="Close">收盘价。</param>
/// <param name="ChangePercent">涨跌幅（百分数）。</param>
public sealed record FundFlowPointDto(
    string Date,
    decimal MainNet,
    decimal SuperLargeNet,
    decimal LargeNet,
    decimal MediumNet,
    decimal SmallNet,
    decimal? MainRatio,
    decimal? Close,
    decimal? ChangePercent);

/// <summary>资金流汇总。</summary>
/// <param name="Days">实际参与统计的交易日数（样本数，不是请求的窗口长度）。</param>
/// <param name="MainNet">区间主力净额合计（亿元）。</param>
/// <param name="InflowDays">主力净流入天数。</param>
/// <param name="OutflowDays">主力净流出天数。</param>
/// <param name="LatestMainNet">最近一日主力净额（亿元）。</param>
/// <param name="LatestMainRatio">最近一日主力净占比（百分数）。</param>
public sealed record FundFlowSummaryDto(
    int Days,
    decimal MainNet,
    int InflowDays,
    int OutflowDays,
    decimal? LatestMainNet,
    decimal? LatestMainRatio);

/// <summary>龙虎榜记录。</summary>
/// <param name="TradeDate">上榜日期。</param>
/// <param name="Reason">上榜原因（上游原文）。</param>
/// <param name="Explain">榜单解读（上游原文）。</param>
/// <param name="Close">收盘价。</param>
/// <param name="ChangePercent">当日涨跌幅（百分数）。</param>
/// <param name="TurnoverRate">换手率（百分数）。</param>
/// <param name="NetAmount">净买入（亿元）。</param>
/// <param name="BuyAmount">买入额（亿元）。</param>
/// <param name="SellAmount">卖出额（亿元）。</param>
/// <param name="Next1Change">次日涨跌幅（百分数）。</param>
/// <param name="Next5Change">后 5 日涨跌幅（百分数）。</param>
/// <param name="Next10Change">后 10 日涨跌幅（百分数）。</param>
public sealed record BillboardDto(
    string TradeDate,
    string? Reason,
    string? Explain,
    decimal? Close,
    decimal? ChangePercent,
    decimal? TurnoverRate,
    decimal? NetAmount,
    decimal? BuyAmount,
    decimal? SellAmount,
    decimal? Next1Change,
    decimal? Next5Change,
    decimal? Next10Change);

/// <summary>大宗交易一笔。</summary>
/// <param name="TradeDate">成交日期。</param>
/// <param name="DealPrice">成交价（元）。</param>
/// <param name="PremiumRatio">折溢价率（百分数）。</param>
/// <param name="DealVolume">成交量（万股）。</param>
/// <param name="DealAmount">成交额（亿元）。</param>
/// <param name="BuyerName">买方营业部。</param>
/// <param name="SellerName">卖方营业部。</param>
/// <param name="Close">当日收盘价（元）。</param>
public sealed record BlockTradeDto(
    string TradeDate,
    decimal? DealPrice,
    decimal? PremiumRatio,
    decimal? DealVolume,
    decimal? DealAmount,
    string? BuyerName,
    string? SellerName,
    decimal? Close);

/// <summary>两融明细一期。</summary>
/// <param name="Date">交易日。</param>
/// <param name="FinanceBalance">融资余额（亿元）。</param>
/// <param name="FinanceBuy">融资买入额（亿元）。</param>
/// <param name="FinanceNetBuy">融资净买入（亿元）。</param>
/// <param name="LoanBalance">融券余额（亿元）。</param>
/// <param name="TotalBalance">融资融券余额（亿元）。</param>
/// <param name="FinanceBalanceRatio">融资余额占流通市值比（百分数）。</param>
/// <param name="Close">收盘价。</param>
public sealed record MarginDetailDto(
    string Date,
    decimal? FinanceBalance,
    decimal? FinanceBuy,
    decimal? FinanceNetBuy,
    decimal? LoanBalance,
    decimal? TotalBalance,
    decimal? FinanceBalanceRatio,
    decimal? Close);

/// <summary>陆股通持股一期（季频）。</summary>
/// <param name="HoldDate">报告期。</param>
/// <param name="DateType">报告期文案，如「2026二季末」。</param>
/// <param name="HoldShares">持股数量（万股）。</param>
/// <param name="AddShares">较上期增减（万股）。</param>
/// <param name="AddSharesAmp">较上期变化幅度（百分数）。</param>
/// <param name="HoldMarketCap">持股市值（亿元）。</param>
/// <param name="FreeSharesRatio">占流通股比例（百分数）。</param>
/// <param name="TotalSharesRatio">占总股本比例（百分数）。</param>
/// <param name="OrgQuantity">持股机构家数。</param>
public sealed record NorthboundDto(
    string HoldDate,
    string? DateType,
    decimal? HoldShares,
    decimal? AddShares,
    decimal? AddSharesAmp,
    decimal? HoldMarketCap,
    decimal? FreeSharesRatio,
    decimal? TotalSharesRatio,
    int? OrgQuantity);

/// <summary>
/// 资金面与筹码（<c>GET /api/stocks/{code}/capital</c>）。
/// </summary>
/// <param name="Code">证券代码。</param>
/// <param name="Name">证券名称。</param>
/// <param name="AsOf">数据时间。</param>
/// <param name="FundFlow">资金流序列（按日期升序）。</param>
/// <param name="Summary">资金流汇总（近 5 日与近 20 日）。</param>
/// <param name="Summary20">近 20 日汇总。</param>
/// <param name="Billboards">龙虎榜记录。</param>
/// <param name="BlockTrades">大宗交易。</param>
/// <param name="Margins">两融明细（按日期升序）。</param>
/// <param name="Northbound">陆股通持股（季频）。</param>
/// <param name="Insights">资金面结论。</param>
/// <param name="Notes">口径说明。</param>
public sealed record CapitalDto(
    string Code,
    string Name,
    string? AsOf,
    IReadOnlyList<FundFlowPointDto> FundFlow,
    FundFlowSummaryDto? Summary,
    FundFlowSummaryDto? Summary20,
    IReadOnlyList<BillboardDto> Billboards,
    IReadOnlyList<BlockTradeDto> BlockTrades,
    IReadOnlyList<MarginDetailDto> Margins,
    IReadOnlyList<NorthboundDto> Northbound,
    IReadOnlyList<string> Insights,
    IReadOnlyList<string> Notes);
