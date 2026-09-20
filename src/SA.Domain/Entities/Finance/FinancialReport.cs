namespace SA.Domain.Entities.Finance;

/// <summary>
/// 一期业绩报表。对应东财 <c>RPT_LICO_FN_CPD</c>（业绩报表），
/// 主键为（代码, 报告期），同一报告期重复采集即覆盖（业绩快报→正式报告会更新同一期）。
/// </summary>
/// <remarks>
/// <b>金额单位统一为元</b>（上游给的就是元），接口层再换算成亿元（详细设计 §1.3）。
/// 比率为百分数（<c>54.80</c> 表示 +54.80%）。
/// </remarks>
public sealed class FinancialReport
{
    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>报告期（如 2026-06-30）。</summary>
    public DateOnly ReportDate { get; set; }

    /// <summary>报告类型文案，如「2026年 半年报」。</summary>
    public string? ReportType { get; set; }

    /// <summary>报告期简称，如「2026Q2」。</summary>
    public string? Quarter { get; set; }

    /// <summary>营业总收入（元）。</summary>
    public decimal? Revenue { get; set; }

    /// <summary>营业总收入同比（百分数）。</summary>
    public decimal? RevenueYoy { get; set; }

    /// <summary>归母净利润（元）。</summary>
    public decimal? NetProfit { get; set; }

    /// <summary>归母净利润同比（百分数）。</summary>
    public decimal? NetProfitYoy { get; set; }

    /// <summary>扣非每股收益（元）。</summary>
    public decimal? DeductedEps { get; set; }

    /// <summary>基本每股收益（元）。</summary>
    public decimal? Eps { get; set; }

    /// <summary>加权 ROE（百分数）。</summary>
    public decimal? Roe { get; set; }

    /// <summary>每股净资产（元）。</summary>
    public decimal? Bps { get; set; }

    /// <summary>每股经营现金流（元）。</summary>
    public decimal? OperatingCashFlowPerShare { get; set; }

    /// <summary>销售毛利率（百分数）。</summary>
    public decimal? GrossMargin { get; set; }

    /// <summary>营收环比（百分数）。</summary>
    public decimal? RevenueQoq { get; set; }

    /// <summary>净利环比（百分数）。</summary>
    public decimal? NetProfitQoq { get; set; }

    /// <summary>分红方案，如「10派14.11元(含税)」。</summary>
    public string? DividendPlan { get; set; }

    /// <summary>最新股息率（百分数）。</summary>
    public decimal? DividendYield { get; set; }

    /// <summary>公告日期。</summary>
    public DateOnly? NoticeDate { get; set; }

    /// <summary>所属行业（上游 BOARD_NAME）。</summary>
    public string? Industry { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// 业绩预告。主键为（代码, 报告期）：同一报告期只保留<b>最新一次披露</b>。
/// </summary>
/// <remarks>
/// <b>为什么不是每条披露一行</b>：实测上游同一报告期会返回多条记录——
/// 既有「首次预告 + 修正公告」的时间序列，也有同一天按不同口径（净利润 / 扣非净利润）同时给出的多条
/// （例如 600519 在 2025-01-03 就有两条完全同型的记录）。
/// 若按「代码 + 报告期 + 公告日」建主键，这些同型记录会撞主键并导致<b>整批写入失败</b>
/// （实测表现就是「业绩预告一条都存不进去」）。业务上页面需要的也正是「每个报告期的最新预告」，
/// 因此在这里收敛为一条，并保留口径字段说明它是按哪个口径预告的。
/// </remarks>
public sealed class EarningsForecast
{
    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>报告期。</summary>
    public DateOnly ReportDate { get; set; }

    /// <summary>预告口径（上游 PREDICT_FINANCE），如「归属于母公司股东的净利润」。</summary>
    public string? Caliber { get; set; }

    /// <summary>预告类型，如「预增」「略减」。</summary>
    public string? ForecastType { get; set; }

    /// <summary>预告摘要。</summary>
    public string? Summary { get; set; }

    /// <summary>预计净利润下限（元）。</summary>
    public decimal? NetProfitMin { get; set; }

    /// <summary>预计净利润上限（元）。</summary>
    public decimal? NetProfitMax { get; set; }

    /// <summary>同比变动下限（百分数）。</summary>
    public decimal? ChangeMin { get; set; }

    /// <summary>同比变动上限（百分数）。</summary>
    public decimal? ChangeMax { get; set; }

    /// <summary>公告日期。</summary>
    public DateOnly? NoticeDate { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
