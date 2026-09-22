namespace SA.Contracts.Research;

/// <summary>
/// 价值研究检查项的一项。
/// </summary>
/// <param name="Key">字段键（与选股器字段名一致，便于前端复用）。</param>
/// <param name="Name">中文名。</param>
/// <param name="Group">所属组键。</param>
/// <param name="Value">已格式化好的展示值；无数据时为 null。</param>
/// <param name="Unit">单位。</param>
/// <param name="AsOf">口径日期（报告期 / 行情日）。</param>
/// <param name="Status">
/// 数据状态，取值见 <c>ResearchStatuses</c>：ok / noData / notApplicable / noUpcoming。
/// <b>界面必须区分这四态</b>：把「不适用」显示成「暂无数据」会误导用户去等一个永远不会来的数据。
/// </param>
/// <param name="Source">数据来源说明。</param>
/// <param name="History">历史序列（仅「周期位置」组有）。</param>
public sealed record ValueResearchItemDto(
    string Key,
    string Name,
    string Group,
    string? Value,
    string? Unit,
    string? AsOf,
    string Status,
    string? Source,
    IReadOnlyList<ValueResearchHistoryPointDto>? History = null);

/// <summary>历史序列的一个点。</summary>
/// <param name="Period">报告期（<c>2025-12-31</c>）。</param>
/// <param name="Value">数值。</param>
public sealed record ValueResearchHistoryPointDto(string Period, decimal? Value);

/// <summary>
/// 检查组。
/// </summary>
/// <param name="Key">组键。</param>
/// <param name="Name">组名。</param>
/// <param name="Question">这一组回答的问题（界面直接展示，用户不必猜为什么看这些）。</param>
/// <param name="Items">检查项。</param>
/// <param name="DefaultExpanded">是否默认展开（默认只展开前两组）。</param>
public sealed record ValueResearchGroupDto(
    string Key,
    string Name,
    string Question,
    IReadOnlyList<ValueResearchItemDto> Items,
    bool DefaultExpanded);

/// <summary>结论区的一个关键数字。</summary>
/// <param name="Name">名称。</param>
/// <param name="Value">展示值。</param>
/// <param name="Status">数据状态。</param>
public sealed record ValueResearchMetricDto(string Name, string Value, string Status);

/// <summary>
/// 结论区：<b>只陈述客观数值，不给任何判断</b>（实施计划 §1.3）。
/// </summary>
/// <remarks>
/// 这里没有「买入 / 看好 / 估值合理」这类措辞，也没有综合评分——那是投资建议，
/// 本轮明确不做。界面上呈现的就是一串事实。
/// </remarks>
/// <param name="Summary">一行客观摘要，形如「2026 中报：营收 14.97 亿（+336.4%）、毛利率 67.66%」。</param>
/// <param name="Metrics">关键数字。</param>
/// <param name="ReportDate">口径报告期。</param>
public sealed record ValueResearchConclusionDto(
    string Summary,
    IReadOnlyList<ValueResearchMetricDto> Metrics,
    string? ReportDate);

/// <summary>
/// 价值研究聚合结果（<c>GET /api/stocks/{code}/value-research</c>）。
/// </summary>
/// <param name="Code">代码。</param>
/// <param name="Name">名称。</param>
/// <param name="Board">板块。</param>
/// <param name="Industry">行业。</param>
/// <param name="Conclusion">结论区。</param>
/// <param name="Groups">七组检查清单。</param>
/// <param name="CaliberNotes">口径说明。</param>
/// <param name="FundamentalAsOf">基本面口径报告期。</param>
public sealed record ValueResearchDto(
    string Code,
    string Name,
    string Board,
    string? Industry,
    ValueResearchConclusionDto Conclusion,
    IReadOnlyList<ValueResearchGroupDto> Groups,
    IReadOnlyList<string> CaliberNotes,
    string? FundamentalAsOf);

/// <summary>历史财务序列的一个报告期。</summary>
/// <param name="ReportDate">报告期。</param>
/// <param name="ReportType">报告期类型（一季报 / 中报 / 三季报 / 年报）。</param>
/// <param name="Revenue">营业总收入（元）。</param>
/// <param name="NetProfit">归母净利润（元）。</param>
/// <param name="RoeWeighted">加权 ROE（百分数）。</param>
/// <param name="GrossMargin">毛利率（百分数）。</param>
/// <param name="NetMargin">净利率（百分数）。</param>
public sealed record FundamentalHistoryPointDto(
    string ReportDate,
    string? ReportType,
    decimal? Revenue,
    decimal? NetProfit,
    decimal? RoeWeighted,
    decimal? GrossMargin,
    decimal? NetMargin);

/// <summary>历史财务序列（<c>GET /api/stocks/{code}/fundamental-history</c>）。</summary>
/// <param name="Code">代码。</param>
/// <param name="Points">按报告期升序。</param>
/// <param name="AnnualPoints">仅年报，用于「连续 N 年」的判定回显。</param>
public sealed record FundamentalHistoryDto(
    string Code,
    IReadOnlyList<FundamentalHistoryPointDto> Points,
    IReadOnlyList<FundamentalHistoryPointDto> AnnualPoints);

/// <summary>主营构成的一项。</summary>
/// <param name="ReportDate">报告期。</param>
/// <param name="MainOpType">口径：1=按行业/大类、2=按产品、3=按地区。</param>
/// <param name="ItemName">项目名。</param>
/// <param name="Income">收入（元）。</param>
/// <param name="IncomeRatio">
/// 收入占比（<b>小数</b>，0.652002 = 65.20%）。
/// 上游给的就是小数，接口层不转换，由前端统一格式化——转换两遍会得到 6520%。
/// </param>
/// <param name="GrossProfitRatio">毛利率（<b>小数</b>）；招股书口径为 null。</param>
/// <param name="IsSubItem">是否为「<c>其中:</c>」子项（子项计入父项，界面须过滤）。</param>
public sealed record BusinessCompositionItemDto(
    string ReportDate,
    int MainOpType,
    string ItemName,
    decimal? Income,
    decimal? IncomeRatio,
    decimal? GrossProfitRatio,
    bool IsSubItem);

/// <summary>
/// 主营构成（<c>GET /api/stocks/{code}/business-composition</c>）。
/// </summary>
/// <param name="Code">代码。</param>
/// <param name="ReportDates">可选报告期（降序）。</param>
/// <param name="Items">构成项（已剔除子项）。</param>
/// <param name="SubItems">子项（单独返回，供「展开子项」用，不与父项并列）。</param>
/// <param name="BusinessScope">业务范围原文。</param>
/// <param name="BusinessReview">经营评述原文（只展示，不解析）。</param>
public sealed record BusinessCompositionDto(
    string Code,
    IReadOnlyList<string> ReportDates,
    IReadOnlyList<BusinessCompositionItemDto> Items,
    IReadOnlyList<BusinessCompositionItemDto> SubItems,
    string? BusinessScope,
    string? BusinessReview);

/// <summary>股本变动一行。</summary>
/// <param name="EndDate">变动日期。</param>
/// <param name="TotalShares">总股本（股）。</param>
/// <param name="LimitedShares">有限售条件股份（股）；null 与 0 都表示「没有限售股」。</param>
/// <param name="UnlimitedShares">无限售条件股份（股）。</param>
/// <param name="ChangeReason">变动原因。</param>
public sealed record ShareChangeDto(
    string EndDate,
    decimal? TotalShares,
    decimal? LimitedShares,
    decimal? UnlimitedShares,
    string? ChangeReason);

/// <summary>限售解禁一项。</summary>
/// <param name="LiftDate">解禁日。</param>
/// <param name="LiftType">解禁类型。</param>
/// <param name="LiftShares">解禁股数（股）。</param>
/// <param name="TotalSharesRatio">占总股本比例（百分数）。</param>
/// <param name="UnlimitedASharesRatio">占流通 A 股比例（百分数）。</param>
public sealed record UpcomingUnlockDto(
    string LiftDate,
    string LiftType,
    decimal? LiftShares,
    decimal? TotalSharesRatio,
    decimal? UnlimitedASharesRatio);

/// <summary>
/// 股本结构与限售解禁（<c>GET /api/stocks/{code}/share-structure</c>）。
/// </summary>
/// <param name="Code">代码。</param>
/// <param name="Changes">股本变动历史（降序）。</param>
/// <param name="UpcomingUnlocks">待解禁（升序）。</param>
/// <param name="UnlockStatus">
/// <c>ok</c> 表示有待解禁；<c>noUpcoming</c> 表示<b>确实没有待解禁</b>（已全流通），
/// 措辞必须与「暂无数据」区分。
/// </param>
public sealed record ShareStructureDto(
    string Code,
    IReadOnlyList<ShareChangeDto> Changes,
    IReadOnlyList<UpcomingUnlockDto> UpcomingUnlocks,
    string UnlockStatus);

/// <summary>公告一行。</summary>
/// <param name="ArtCode">文章 Id。</param>
/// <param name="Title">标题。</param>
/// <param name="NoticeDate">公告日期。</param>
/// <param name="ColumnName">公告类型。</param>
/// <param name="AnnType">公告子类型。</param>
/// <param name="Url">原文链接。</param>
public sealed record AnnouncementDto(
    string ArtCode,
    string Title,
    string NoticeDate,
    string? ColumnName,
    string? AnnType,
    string? Url);

/// <summary>
/// 公告列表（<c>GET /api/stocks/{code}/announcements</c>）。
/// </summary>
/// <param name="Code">代码。</param>
/// <param name="Total">该标的已采集的公告总数。</param>
/// <param name="Types">类型 → 条数（界面按类型分组，默认折叠）。</param>
/// <param name="Items">公告列表（按日期降序）。</param>
public sealed record AnnouncementListDto(
    string Code,
    int Total,
    IReadOnlyDictionary<string, int> Types,
    IReadOnlyList<AnnouncementDto> Items);

/// <summary>研报一行。</summary>
/// <param name="InfoCode">研报 Id。</param>
/// <param name="Title">标题。</param>
/// <param name="OrgShortName">券商简称。</param>
/// <param name="Researcher">分析师。</param>
/// <param name="PublishDate">发布日期。</param>
/// <param name="RatingName">评级名称。</param>
/// <param name="IndustryName">所属行业。</param>
/// <param name="PredictThisYearEps">本年度预测 EPS。</param>
/// <param name="PredictThisYearPe">本年度预测 PE。</param>
/// <param name="PredictNextYearEps">下一年度预测 EPS。</param>
/// <param name="PredictNextYearPe">下一年度预测 PE。</param>
/// <param name="PredictNextTwoYearEps">后年预测 EPS。</param>
/// <param name="PredictNextTwoYearPe">后年预测 PE。</param>
/// <param name="Url">原文链接。</param>
public sealed record ResearchReportDto(
    string InfoCode,
    string Title,
    string? OrgShortName,
    string? Researcher,
    string PublishDate,
    string? RatingName,
    string? IndustryName,
    decimal? PredictThisYearEps,
    decimal? PredictThisYearPe,
    decimal? PredictNextYearEps,
    decimal? PredictNextYearPe,
    decimal? PredictNextTwoYearEps,
    decimal? PredictNextTwoYearPe,
    string? Url);

/// <summary>
/// 研报列表（<c>GET /api/stocks/{code}/research</c>）。
/// </summary>
/// <remarks>
/// <b>没有目标价字段</b>：实测上游 <c>indvAimPriceT</c> / <c>indvAimPriceL</c> 全为空字符串，
/// 按实施计划 §1.3 不展示目标价、不造目标价。
/// </remarks>
/// <param name="Code">代码。</param>
/// <param name="Items">研报列表（按发布日期降序）。</param>
/// <param name="AimPriceNote">目标价缺失的说明（界面据此显示空态原因，而不是留空）。</param>
public sealed record ResearchListDto(
    string Code,
    IReadOnlyList<ResearchReportDto> Items,
    string AimPriceNote);
