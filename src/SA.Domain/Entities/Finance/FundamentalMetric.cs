namespace SA.Domain.Entities.Finance;

/// <summary>
/// 基本面指标（全市场横截面因子）。对应东财 <c>RPT_F10_FINANCE_MAINFINADATA</c>，
/// 主键为（代码, 报告期）：同一报告期重复采集即覆盖。
/// </summary>
/// <remarks>
/// <para>
/// <b>与 <see cref="FinancialReport"/> 的分工</b>：后者是「单只个股的业绩报表序列」（<c>RPT_LICO_FN_CPD</c>），
/// 按代码逐只拉取，含股息率；本实体是「按报告期扫全市场」的横截面因子表，
/// 一次请求即覆盖全市场一个报告期的 100+ 个因子（实测 27 页 / 13,420 行），
/// 用于选股器与个股价值研究的历史序列。
/// </para>
/// <para>
/// <b>主键设计使历史序列与全市场快照天然共存</b>：全市场扫描只写「最新已披露报告期」，
/// 而单只个股的历史序列写的是同一张表里的其它报告期（实测东芯股份 29 个报告期可一次拿到），
/// 因此<b>不需要第二张表</b>（实施计划 §5.1）。
/// </para>
/// <para>
/// <b>单位约定</b>：金额一律为元（上游即元）；比率与百分比字段一律为百分数
/// （<c>17.36</c> 表示 17.36%），与 <see cref="FinancialReport"/> 保持一致。
/// </para>
/// <para>
/// <b>两个刻意的缺口</b>：
/// </para>
/// <list type="bullet">
/// <item><c>PRATIO</c>（上游值为 63.53）<b>不落库也不展示</b>：实测数值上像「研发人员占比」，
/// 但无法证实，按实施计划 §2.3 第 4 条，核对清楚前不展示（不得标注为「研发人员占比」）。</item>
/// <item>股息率不在这里：<c>RPT_F10_FINANCE_MAINFINADATA</c> 不含股息率，
/// 仍以 <see cref="FinancialReport.DividendYield"/>（<c>RPT_LICO_FN_CPD</c> 的 <c>ZXGXL</c>）为准。</item>
/// </list>
/// </remarks>
public sealed class FundamentalMetric
{
    /// <summary>证券代码（6 位）。</summary>
    public required string Code { get; set; }

    /// <summary>报告期（如 2026-06-30）。</summary>
    public DateOnly ReportDate { get; set; }

    /// <summary>报告期类型：一季报 / 中报 / 三季报 / 年报（上游 <c>REPORT_TYPE</c>）。</summary>
    public string? ReportType { get; set; }

    /// <summary>
    /// 公司类型（上游 <c>ORG_TYPE</c>）：通用 / 银行 / 保险 / 证券。
    /// </summary>
    /// <remarks>
    /// 这是「金融业不适用某些指标」的判别依据：实测平安银行（银行）的
    /// <c>XSMLL</c>（毛利率）/ <c>LD</c>（流动比率）/ <c>ROIC</c> / <c>FCFF_FORWARD</c> 全为 <c>null</c>，
    /// 而 <c>ZCFZL</c>（资产负债率）为 90.91。按「毛利率 &gt; 30%」筛选会把整个银行保险板块静默排除，
    /// 因此必须能识别出这一类标的并显式提示（实施计划 §2.3 第 1 条）。
    /// </remarks>
    public string? OrgType { get; set; }

    /// <summary>公告日期。</summary>
    public DateOnly? NoticeDate { get; set; }

    /* ---------- 盈利质量 ---------- */

    /// <summary>加权净资产收益率（百分数，上游 <c>ROEJQ</c>）。</summary>
    public decimal? RoeWeighted { get; set; }

    /// <summary>扣非加权净资产收益率（百分数，上游 <c>ROEKCJQ</c>）。</summary>
    public decimal? RoeDeducted { get; set; }

    /// <summary>销售毛利率（百分数，上游 <c>XSMLL</c>）；金融业为 null。</summary>
    public decimal? GrossMargin { get; set; }

    /// <summary>销售净利率（百分数，上游 <c>XSJLL</c>）。</summary>
    public decimal? NetMargin { get; set; }

    /// <summary>投入资本回报率（百分数，上游 <c>ROIC</c>）；金融业为 null。</summary>
    public decimal? Roic { get; set; }

    /// <summary>经营现金流 ÷ 营业总收入（上游 <c>JYXJLYYSR</c>）。</summary>
    public decimal? OperatingCashFlowToRevenue { get; set; }

    /// <summary>经营现金流净额（元，上游 <c>NETCASH_OPERATE_PK</c>）。</summary>
    public decimal? OperatingCashFlow { get; set; }

    /// <summary>
    /// 经营现金流 ÷ 净利润（上游 <c>NCO_NETPROFIT</c>）。
    /// </summary>
    /// <remarks>「赚的是不是真钱」的核心比值：实测东芯股份 2026 中报为 0.835。</remarks>
    public decimal? OperatingCashFlowToNetProfit { get; set; }

    /// <summary>经营现金流 ÷ 营业利润（上游 <c>NCO_OP</c>）。</summary>
    public decimal? OperatingCashFlowToOperatingProfit { get; set; }

    /// <summary>自由现金流（元，上游 <c>FCFF_FORWARD</c>）；金融业为 null。</summary>
    public decimal? FreeCashFlow { get; set; }

    /* ---------- 安全性 ---------- */

    /// <summary>资产负债率（百分数，上游 <c>ZCFZL</c>）。</summary>
    public decimal? DebtRatio { get; set; }

    /// <summary>流动比率（上游 <c>LD</c>）；金融业为 null。</summary>
    public decimal? CurrentRatio { get; set; }

    /// <summary>速动比率（上游 <c>SD</c>）。</summary>
    public decimal? QuickRatio { get; set; }

    /// <summary>有息负债率（百分数，上游 <c>INTEREST_DEBT_RATIO</c>）。</summary>
    public decimal? InterestDebtRatio { get; set; }

    /// <summary>利息保障倍数（上游 <c>INTSTCOVRATE</c>）。</summary>
    public decimal? InterestCoverageRatio { get; set; }

    /// <summary>清算价值比率（上游 <c>LIQUIDATION_RATIO</c>）。</summary>
    public decimal? LiquidationRatio { get; set; }

    /* ---------- 周转 ---------- */

    /// <summary>存货周转天数（上游 <c>CHZZTS</c>）。</summary>
    public decimal? InventoryTurnoverDays { get; set; }

    /// <summary>应收账款周转天数（上游 <c>YSZKZZTS</c>）。</summary>
    public decimal? ReceivableTurnoverDays { get; set; }

    /// <summary>总资产周转天数（上游 <c>ZZCZZTS</c>）。</summary>
    public decimal? AssetTurnoverDays { get; set; }

    /* ---------- 成长 ---------- */

    /// <summary>营业总收入同比（百分数，上游 <c>TOTALOPERATEREVETZ</c>）。</summary>
    public decimal? RevenueYoy { get; set; }

    /// <summary>归母净利润同比（百分数，上游 <c>PARENTNETPROFITTZ</c>）。</summary>
    public decimal? NetProfitYoy { get; set; }

    /// <summary>扣非净利润同比（百分数，上游 <c>KCFJCXSYJLRTZ</c>）。</summary>
    public decimal? DeductedNetProfitYoy { get; set; }

    /* ---------- 每股指标 ---------- */

    /// <summary>基本每股收益（元，上游 <c>EPSJB</c>）。</summary>
    public decimal? Eps { get; set; }

    /// <summary>扣非每股收益（元，上游 <c>EPSKCJB</c>）。</summary>
    /// <remarks>
    /// 字段名是 <c>EPSKCJB</c>，不是实施计划 §2.3 第 3 条写的 <c>DEDUCT_BASIC_EPS</c>
    /// （后者是另一个报表 <c>RPT_LICO_FN_CPD</c> 的字段名）。实测 2026-09-21 逐字段核对确认。
    /// </remarks>
    public decimal? EpsDeducted { get; set; }

    /// <summary>每股净资产（元，上游 <c>BPS</c>）。</summary>
    public decimal? Bps { get; set; }

    /// <summary>每股经营现金流（元，上游 <c>MGJYXJJE</c>）。</summary>
    public decimal? OperatingCashFlowPerShare { get; set; }

    /* ---------- 研发投入 ---------- */

    /// <summary>研发费用（元，上游 <c>RDEXPEND</c>）。</summary>
    public decimal? RndExpense { get; set; }

    /// <summary>研发费用率（百分数，上游 <c>RE_RATIO_PK</c>）。</summary>
    public decimal? RndExpenseRatio { get; set; }

    /// <summary>研发人员数（上游 <c>RDPERSONNEL</c>）。</summary>
    public decimal? RndPersonnel { get; set; }

    /* ---------- 规模 ---------- */

    /// <summary>营业总收入（元，上游 <c>TOTALOPERATEREVE</c>）。</summary>
    public decimal? Revenue { get; set; }

    /// <summary>归母净利润（元，上游 <c>PARENTNETPROFIT</c>）。</summary>
    public decimal? NetProfit { get; set; }

    /// <summary>总资产（元，上游 <c>TOTAL_ASSETS_PK</c>）。</summary>
    public decimal? TotalAssets { get; set; }

    /// <summary>股东权益合计（元，上游 <c>TOTAL_EQUITY_PK</c>）。</summary>
    public decimal? TotalEquity { get; set; }

    /// <summary>负债合计（元，上游 <c>LIABILITY</c>）。</summary>
    public decimal? Liability { get; set; }

    /// <summary>总股本（股，上游 <c>TOTAL_SHARE</c>）。</summary>
    public decimal? TotalShare { get; set; }

    /// <summary>流通 A 股（股，上游 <c>A_FREE_SHARE</c>）。</summary>
    public decimal? FreeShare { get; set; }

    /// <summary>
    /// 员工总数（上游 <c>STAFF_NUM</c>）。
    /// </summary>
    /// <remarks>
    /// <b>实测常为 null</b>（东芯股份即为 null）。按实施计划 §1.1 的要求，
    /// 界面必须显示「暂无数据」，不得回退到估算值或行业均值。
    /// </remarks>
    public decimal? StaffNumber { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
