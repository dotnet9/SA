namespace SA.Contracts.Finance;

/// <summary>
/// 一期财务数据（接口口径：金额为亿元，比率为百分数）。
/// </summary>
/// <param name="ReportDate">报告期。</param>
/// <param name="ReportType">报告类型，如「2026年 半年报」。</param>
/// <param name="Quarter">报告期简称，如「2026Q2」。</param>
/// <param name="Revenue">营业总收入（亿元）。</param>
/// <param name="RevenueYoy">营收同比（百分数）。</param>
/// <param name="NetProfit">归母净利润（亿元）。</param>
/// <param name="NetProfitYoy">净利同比（百分数）。</param>
/// <param name="Eps">基本每股收益（元）。</param>
/// <param name="DeductedEps">扣非每股收益（元）。</param>
/// <param name="Roe">加权 ROE（百分数）。</param>
/// <param name="Bps">每股净资产（元）。</param>
/// <param name="OperatingCashFlowPerShare">每股经营现金流（元）。</param>
/// <param name="GrossMargin">销售毛利率（百分数）。</param>
/// <param name="RevenueQoq">营收环比（百分数）。</param>
/// <param name="NetProfitQoq">净利环比（百分数）。</param>
/// <param name="DividendPlan">分红方案。</param>
/// <param name="DividendYield">股息率（百分数）。</param>
/// <param name="NoticeDate">公告日期。</param>
public sealed record FinancePeriodDto(
    string ReportDate,
    string? ReportType,
    string? Quarter,
    decimal? Revenue,
    decimal? RevenueYoy,
    decimal? NetProfit,
    decimal? NetProfitYoy,
    decimal? Eps,
    decimal? DeductedEps,
    decimal? Roe,
    decimal? Bps,
    decimal? OperatingCashFlowPerShare,
    decimal? GrossMargin,
    decimal? RevenueQoq,
    decimal? NetProfitQoq,
    string? DividendPlan,
    decimal? DividendYield,
    string? NoticeDate);

/// <summary>
/// 业绩预告（每个报告期只保留最新一次披露）。
/// </summary>
/// <param name="ReportDate">报告期。</param>
/// <param name="Caliber">预告口径，如「归属于母公司股东的净利润」。</param>
/// <param name="ForecastType">预告类型，如「预增」。</param>
/// <param name="Summary">预告正文。</param>
/// <param name="NetProfitMin">预计净利润下限（亿元）。</param>
/// <param name="NetProfitMax">预计净利润上限（亿元）。</param>
/// <param name="ChangeMin">同比变动下限（百分数）。</param>
/// <param name="ChangeMax">同比变动上限（百分数）。</param>
/// <param name="NoticeDate">公告日期。</param>
public sealed record EarningsForecastDto(
    string ReportDate,
    string? Caliber,
    string? ForecastType,
    string? Summary,
    decimal? NetProfitMin,
    decimal? NetProfitMax,
    decimal? ChangeMin,
    decimal? ChangeMax,
    string? NoticeDate);

/// <summary>
/// 财务指标趋势的一个点（用于图表，按报告期排列）。
/// </summary>
/// <param name="Label">报告期标签，如 <c>2026Q2</c>。</param>
/// <param name="Revenue">营业总收入（亿元）。</param>
/// <param name="NetProfit">归母净利润（亿元）。</param>
/// <param name="Roe">加权 ROE（百分数）。</param>
/// <param name="GrossMargin">毛利率（百分数）。</param>
/// <param name="RevenueYoy">营收同比（百分数）。</param>
/// <param name="NetProfitYoy">净利同比（百分数）。</param>
/// <param name="OperatingCashFlowPerShare">每股经营现金流（元）。</param>
/// <param name="Eps">每股收益（元）。</param>
public sealed record FinanceTrendPointDto(
    string Label,
    decimal? Revenue,
    decimal? NetProfit,
    decimal? Roe,
    decimal? GrossMargin,
    decimal? RevenueYoy,
    decimal? NetProfitYoy,
    decimal? OperatingCashFlowPerShare,
    decimal? Eps);

/// <summary>
/// 盈利与财务表现（<c>GET /api/stocks/{code}/finance</c>）。
/// </summary>
/// <param name="Code">证券代码。</param>
/// <param name="Name">证券名称。</param>
/// <param name="AsOf">数据时间。</param>
/// <param name="Latest">最新一期。</param>
/// <param name="Periods">按期倒序的明细（最近若干期）。</param>
/// <param name="Trend">图表用的趋势序列（按期升序）。</param>
/// <param name="Forecasts">业绩预告。</param>
/// <param name="Insights">财务结论（可比口径由上游给出）。</param>
/// <param name="Notes">口径说明。</param>
public sealed record FinanceDto(
    string Code,
    string Name,
    string? AsOf,
    FinancePeriodDto? Latest,
    IReadOnlyList<FinancePeriodDto> Periods,
    IReadOnlyList<FinanceTrendPointDto> Trend,
    IReadOnlyList<EarningsForecastDto> Forecasts,
    IReadOnlyList<string> Insights,
    IReadOnlyList<string> Notes);
