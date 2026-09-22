namespace SA.Domain.Entities.Research;

/// <summary>
/// 数据状态四态（实施计划 §6.1）。
/// </summary>
/// <remarks>
/// <para>
/// 这是本轮「不做投资建议、只讲事实」要求的落地方式：界面必须能区分
/// 「查到了但为空」与「这个字段对该行业不适用」，否则用户会把「不适用」误读成「数据没采到」，
/// 或者把「确实没有待解禁」误读成「还没查到」。
/// </para>
/// <list type="bullet">
/// <item><see cref="Ok"/>：有数据，显示实测值。</item>
/// <item><see cref="NoData"/>：该股该字段为空（实测东芯股份 <c>STAFF_NUM</c> 为 null）。</item>
/// <item><see cref="NotApplicable"/>：行业口径不同（实测平安银行毛利率/流动比率/ROIC/自由现金流为 null）。</item>
/// <item><see cref="NoUpcoming"/>：<b>确实没有</b>而非「没查到」（实测东芯股份 <c>xsjj</c> 为空且已全流通）。
/// 措辞必须与 <see cref="NoData"/> 区分——这个区别对判断筹码压力很关键。</item>
/// </list>
/// </remarks>
public static class ResearchStatuses
{
    /// <summary>有数据。</summary>
    public const string Ok = "ok";

    /// <summary>暂无数据。</summary>
    public const string NoData = "noData";

    /// <summary>不适用（行业口径不同）。</summary>
    public const string NotApplicable = "notApplicable";

    /// <summary>暂无待解禁（确实没有，不是没查到）。</summary>
    public const string NoUpcoming = "noUpcoming";
}

/// <summary>
/// 主营构成的一项（东财 F10 <c>BusinessAnalysis</c> 的 <c>zygcfx</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 主键为（代码, 报告期, 口径, 项目名）。<b>三套口径并列，不能相加</b>：
/// 实测 <c>MAINOP_TYPE</c> 取值 1=按行业/大类、2=按产品、3=按地区，
/// 各自合计为 100%，混算会得到翻倍的收入。
/// </para>
/// <para>
/// <b>比率字段是小数而不是百分数</b>：实测东芯股份 2025 年报 NAND 的
/// <c>MBI_RATIO</c> 为 <c>0.652002</c>（即 65.20%）、<c>GROSS_RPOFIT_RATIO</c> 为
/// <c>0.245146</c>（即 24.51%）。本实体<b>原样存小数</b>，展示时再转百分数——
/// 与其它财务表「存百分数」的约定不同，因此这里显式标注以免误用。
/// </para>
/// <para>
/// <b>精度</b>：这些比率按 <c>double</c> 存储（与仓库既有的财务表约定一致，
/// SQLite 只有 REAL），因此 <c>0.652002</c> 读回是 <c>0.65200199999999997</c>。
/// 展示时四舍五入到 2 位（65.20%）这个误差不可见，但<b>不要做精确相等比较</b>。
/// </para>
/// <para>
/// <b><c>ITEM_NAME</c> 里的 <c>其中:</c> 前缀是子项</b>：实测东芯 2021 中报有
/// <c>其中:SDRAM</c> / <c>其中:LPDRAM</c> / <c>其中:DDR3</c> / <c>其中:PSRAM</c>，
/// 它们<b>会计入父项</b>（DRAM），不能当作同级产品并列展示，否则收入重复计算。
/// 本实体用 <see cref="IsSubItem"/> 标记，由展示层过滤。
/// </para>
/// <para>
/// <b>早期报告期是招股书口径</b>：实测 <c>2021-09-30</c> 的 <c>ITEM_NAME</c> 为
/// 「客户合同产生的收入」，<c>MAIN_BUSINESS_COST</c> 与 <c>GROSS_RPOFIT_RATIO</c> 均为 null
/// → 只展示收入，不展示毛利率。
/// </para>
/// </remarks>
public sealed class BusinessComposition
{
    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>报告期。</summary>
    public DateOnly ReportDate { get; set; }

    /// <summary>
    /// 口径：1=按行业/大类、2=按产品、3=按地区（上游 <c>MAINOP_TYPE</c>）。
    /// </summary>
    public int MainOpType { get; set; }

    /// <summary>项目名（上游 <c>ITEM_NAME</c>），可能带 <c>其中:</c> 前缀。</summary>
    public required string ItemName { get; set; }

    /// <summary>排名（上游 <c>RANK</c>）。</summary>
    public int Rank { get; set; }

    /// <summary>主营收入（元）。</summary>
    public decimal? Income { get; set; }

    /// <summary>收入占比（<b>小数</b>，0.652002 = 65.20%）。</summary>
    public decimal? IncomeRatio { get; set; }

    /// <summary>主营成本（元）；招股书口径为 null。</summary>
    public decimal? Cost { get; set; }

    /// <summary>成本占比（<b>小数</b>）。</summary>
    public decimal? CostRatio { get; set; }

    /// <summary>主营利润（元）。</summary>
    public decimal? Profit { get; set; }

    /// <summary>利润占比（<b>小数</b>）。</summary>
    public decimal? ProfitRatio { get; set; }

    /// <summary>毛利率（<b>小数</b>，0.245146 = 24.51%）；招股书口径为 null。</summary>
    public decimal? GrossProfitRatio { get; set; }

    /// <summary>
    /// 是否为「<c>其中:</c>」子项。
    /// </summary>
    /// <remarks>
    /// 子项计入父项，展示时必须排除，否则收入重复计算。判定放在写入侧，
    /// 避免每个读取方各写一遍前缀判断而漂移。
    /// </remarks>
    public bool IsSubItem { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// 股本变动一行（东财 F10 <c>CapitalStockStructure</c> 的 <c>lngbbd</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 主键为（代码, 变动日期）。<c>CHANGE_REASON</c> 是现成的治理与筹码线索：
/// 实测东芯股份含「首发限售股份上市」「限制性股票」「股份性质变更」，
/// 宁德时代含「自主行权」「配售H股上市」。
/// </para>
/// <para>
/// 最新一行同时充当「最新股本结构」（<c>gbjg</c> 给的是同一份口径的当前值）。
/// </para>
/// </remarks>
public sealed class ShareChange
{
    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>变动日期（上游 <c>END_DATE</c>）。</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>总股本（股）。</summary>
    public decimal? TotalShares { get; set; }

    /// <summary>
    /// 有限售条件股份（股）。
    /// </summary>
    /// <remarks>
    /// <b>实测 <c>null</c> 与 <c>0</c> 都表示「没有限售股」</b>：东芯股份为 <c>null</c>
    /// （且 <c>TOTAL_SHARES == UNLIMITED_SHARES</c>，已全流通），贵州茅台为 <c>0</c>。
    /// 因此展示层把两者都当作 0，不能把 null 显示成「暂无数据」。
    /// </remarks>
    public decimal? LimitedShares { get; set; }

    /// <summary>无限售条件股份（股）。</summary>
    public decimal? UnlimitedShares { get; set; }

    /// <summary>已上市流通 A 股（股）。</summary>
    public decimal? ListedAShares { get; set; }

    /// <summary>变动原因（上游 <c>CHANGE_REASON</c>）。</summary>
    public string? ChangeReason { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// 限售解禁一项（东财 F10 <c>CapitalStockStructure</c> 的 <c>xsjj</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 主键为（代码, 解禁日, 解禁类型）。实测字段：
/// <c>LIFT_DATE</c> 解禁日、<c>LIFT_NUM</c> 解禁股数、<c>LIFT_TYPE</c> 解禁类型、
/// <c>TOTAL_SHARES_RATIO</c> 占总股本比例（<b>百分数</b>）、
/// <c>UNLIMITED_A_SHARES_RATIO</c> 占流通 A 股比例（<b>百分数</b>）。
/// </para>
/// <para>
/// 实测中芯国际 <c>688981</c>：2027-06-23 解禁 547,182,073 股（占总股本 6.39%），
/// 类型「定向增发机构配售股份」。
/// </para>
/// <para>
/// <b><c>xsjj</c> 为空是「暂无待解禁」而不是「暂无数据」</b>（实施计划 §2.4.2）：
/// 东芯股份即为此例，其 <c>TOTAL_SHARES == UNLIMITED_SHARES == LISTED_A_SHARES</c>，确已全流通。
/// 界面必须写成「暂无待解禁」，措辞与「暂无数据」区分。
/// </para>
/// </remarks>
public sealed class UpcomingUnlock
{
    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>解禁日。</summary>
    public DateOnly LiftDate { get; set; }

    /// <summary>解禁类型，如「定向增发机构配售股份」。</summary>
    public required string LiftType { get; set; }

    /// <summary>解禁股数（股）。</summary>
    public decimal? LiftShares { get; set; }

    /// <summary>占总股本比例（<b>百分数</b>，6.39 = 6.39%）。</summary>
    public decimal? TotalSharesRatio { get; set; }

    /// <summary>占流通 A 股比例（<b>百分数</b>）。</summary>
    public decimal? UnlimitedASharesRatio { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// 主营概况（业务范围 + 管理层经营评述原文），按代码一行。
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="BusinessComposition"/> 分表：后者按报告期多行，而这两项是<b>按代码唯一</b>的
/// （上游 <c>zyfw</c> / <c>jyps</c> 各只有一条）。放进构成表会在每个报告期重复存一遍长文本
/// （经营评述实测约 4,500 字）。
/// </para>
/// <para>
/// <b>经营评述只做原文展示，不做任何解析</b>（实施计划 §1.3）：
/// 不做关键词抽取、不做情绪判断、不做事件自动归类。
/// </para>
/// </remarks>
public sealed class BusinessProfile
{
    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>业务范围原文（上游 <c>zyfw[0].BUSINESS_SCOPE</c>）。</summary>
    public string? BusinessScope { get; set; }

    /// <summary>管理层经营评述原文（上游 <c>jyps[0].BUSINESS_REVIEW</c>）。</summary>
    public string? BusinessReview { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// 公告（东财 <c>np-anotice-stock</c> 的 <c>api/security/ann</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 主键为 <c>art_code</c>（上游的文章 Id）。实测东芯股份 <c>total_hits</c> 为 892。
/// </para>
/// <para>
/// <b>只存标题、日期、类型与原文链接，不做正文解析</b>（实施计划 §1.3）：
/// 不做关键词抽取、不做情绪判断、不做事件自动归类。
/// </para>
/// <para>
/// <c>columns[].column_name</c> 是公告类型（实测含「签订协议」「调研活动」「法律意见书」），
/// 一个公告可能有多个类型，这里取第一个作为主类型并保留全部到 <see cref="ColumnNames"/>。
/// </para>
/// </remarks>
public sealed class Announcement
{
    /// <summary>文章 Id（上游 <c>art_code</c>）。</summary>
    public required string ArtCode { get; set; }

    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>标题。</summary>
    public required string Title { get; set; }

    /// <summary>公告日期。</summary>
    public DateOnly NoticeDate { get; set; }

    /// <summary>公告类型（上游 <c>columns[0].column_name</c>）。</summary>
    public string? ColumnName { get; set; }

    /// <summary>全部公告类型，逗号分隔（一个公告可能同时属于多个类型）。</summary>
    public string? ColumnNames { get; set; }

    /// <summary>公告子类型（上游 <c>codes[].ann_type</c>，实测含 <c>A,KCB,SHA</c> 与 <c>INV</c>）。</summary>
    public string? AnnType { get; set; }

    /// <summary>来源类型（上游 <c>source_type</c>）。</summary>
    public string? SourceType { get; set; }

    /// <summary>原文链接。</summary>
    public string? Url { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// 研报（东财 <c>reportapi</c> 的 <c>report/list</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 主键为 <c>infoCode</c>（上游的研报 Id）。实测东芯股份 2025-01-01 ~ 2026-09-21 共 5 篇。
/// </para>
/// <para>
/// <b>不展示目标价</b>：实测 <c>indvAimPriceT</c> / <c>indvAimPriceL</c>（目标价上下限）
/// <b>全部为空字符串</b>，该源基本不提供目标价。按实施计划 §1.3 只展示评级与三年盈利预测，
/// 不造目标价、不拿别处的价凑。因此本实体<b>没有目标价字段</b>。
/// </para>
/// <para>
/// 三年 PE 预测原样展示、不做解读：实测东芯股份为 127 / 114.2 / 99.8，
/// 高 PE 本身是有效信息（当期利润低、周期位置），正是价值投资者需要的上下文。
/// </para>
/// </remarks>
public sealed class ResearchReport
{
    /// <summary>研报 Id（上游 <c>infoCode</c>）。</summary>
    public required string InfoCode { get; set; }

    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>研报标题。</summary>
    public required string Title { get; set; }

    /// <summary>券商全称。</summary>
    public string? OrgName { get; set; }

    /// <summary>券商简称。</summary>
    public string? OrgShortName { get; set; }

    /// <summary>分析师。</summary>
    public string? Researcher { get; set; }

    /// <summary>发布日期。</summary>
    public DateOnly PublishDate { get; set; }

    /// <summary>评级名称，如「买入」「增持」。</summary>
    public string? RatingName { get; set; }

    /// <summary>评级变动（上游 <c>ratingChange</c>）。</summary>
    public int? RatingChange { get; set; }

    /// <summary>所属行业（上游 <c>indvInduName</c>，实测「半导体」）。</summary>
    public string? IndustryName { get; set; }

    /// <summary>本年度预测每股收益（元）。</summary>
    public decimal? PredictThisYearEps { get; set; }

    /// <summary>本年度预测 PE。</summary>
    public decimal? PredictThisYearPe { get; set; }

    /// <summary>下一年度预测每股收益（元）。</summary>
    public decimal? PredictNextYearEps { get; set; }

    /// <summary>下一年度预测 PE。</summary>
    public decimal? PredictNextYearPe { get; set; }

    /// <summary>后年预测每股收益（元）。</summary>
    public decimal? PredictNextTwoYearEps { get; set; }

    /// <summary>后年预测 PE。</summary>
    public decimal? PredictNextTwoYearPe { get; set; }

    /// <summary>原文链接参数（上游 <c>encodeUrl</c>）。</summary>
    public string? EncodeUrl { get; set; }

    /// <summary>原文链接（由 <see cref="EncodeUrl"/> 拼出）。</summary>
    public string? Url { get; set; }

    /// <summary>写入时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
