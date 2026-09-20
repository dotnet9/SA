namespace SA.Domain.Entities.Market;

/// <summary>
/// 市场级日度统计。对应本批<b>新增表</b> <c>MarketStat</c>（与实施计划 §5.3 的新增表登记同一处理方式）。
/// </summary>
/// <remarks>
/// 存在的理由：页面上的这几个数字<b>无法由个股快照推导</b>——
/// 涨跌停家数需要交易所口径的专用端点（按涨跌幅阈值反推在 20%/30% 涨跌幅板块与 ST 上都会算错）；
/// 全市场资金分层来自大盘资金流端点；两融余额来自数据中心报表。
/// 把它们按业务日落在同一张表里，市场页读一次即可，也顺带留下了每日的历史轨迹。
/// </remarks>
public sealed class MarketStat
{
    /// <summary>统计口径日。</summary>
    public DateOnly Date { get; set; }

    /// <summary>涨停家数（交易所口径）。</summary>
    public int LimitUp { get; set; }

    /// <summary>跌停家数（交易所口径）。</summary>
    public int LimitDown { get; set; }

    /// <summary>两市主力净额（元）。</summary>
    public decimal MainNet { get; set; }

    /// <summary>超大单净额（元）。</summary>
    public decimal SuperLarge { get; set; }

    /// <summary>大单净额（元）。</summary>
    public decimal Large { get; set; }

    /// <summary>中单净额（元）。</summary>
    public decimal Medium { get; set; }

    /// <summary>小单净额（元）。</summary>
    public decimal Small { get; set; }

    /// <summary>融资余额（元，沪深合计）。</summary>
    public decimal FinanceBalance { get; set; }

    /// <summary>融券余额（元，沪深合计）。</summary>
    public decimal LoanBalance { get; set; }

    /// <summary>两融数据的口径日（与 <see cref="Date"/> 可能不同：两融披露晚于行情）。</summary>
    public DateOnly? MarginDate { get; set; }

    /// <summary>资金流数据的口径日（大盘资金流按指数行情发布）。</summary>
    public DateOnly? FundFlowDate { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
