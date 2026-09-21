namespace SA.Domain.Entities.Finance;

/// <summary>
/// 报告期类型（上游 <c>REPORT_TYPE</c> 的实测取值）。
/// </summary>
/// <remarks>
/// 实测（2026-09-21）：<c>2026-06-30</c> 的行 <c>REPORT_TYPE</c> 为「中报」，
/// <c>2025-12-31</c> 的为「年报」，一季报/三季报同理。上游还有一个
/// <c>REPORT_DATE_NAME</c>（形如「2026中报」），本系统只用 <c>REPORT_TYPE</c>。
/// </remarks>
public static class FundamentalReportTypes
{
    /// <summary>一季报。</summary>
    public const string Quarter1 = "一季报";

    /// <summary>中报（半年报）。</summary>
    public const string Interim = "中报";

    /// <summary>三季报。</summary>
    public const string Quarter3 = "三季报";

    /// <summary>年报。</summary>
    public const string Annual = "年报";

    /// <summary>
    /// 是否为年报。
    /// </summary>
    /// <remarks>
    /// 连续性条件（「连续 5 年 ROE &gt; 15%」）<b>只取年报</b>：季报是累计口径，
    /// 把中报与年报混在一条序列里比较会得出无意义的结果（实施计划 §5.3）。
    /// </remarks>
    public static bool IsAnnual(string? reportType) =>
        string.Equals(reportType?.Trim(), Annual, StringComparison.Ordinal);

    /// <summary>
    /// 由报告期推断是否为年报（报告期在 12 月）。
    /// </summary>
    /// <remarks>
    /// 兜底用：上游偶有行的 <c>REPORT_TYPE</c> 为空，此时按「报告期落在 12 月」判断，
    /// 避免这类行被静默排除在年度序列之外。
    /// </remarks>
    public static bool IsAnnualDate(DateOnly reportDate) => reportDate.Month == 12;
}

/// <summary>
/// 公司类型（上游 <c>ORG_TYPE</c> 的实测取值）。
/// </summary>
/// <remarks>
/// <para>
/// 实测分布（2026-06-30 前 5 页 2,500 行）：通用 2,432 / 银行 35 / 证券 28 / 保险 5。
/// </para>
/// <para>
/// 这个字段决定了「哪些指标对该标的<b>不适用</b>」：实测平安银行（银行）
/// <c>XSMLL</c>（毛利率）、<c>LD</c>（流动比率）、<c>ROIC</c>、<c>FCFF_FORWARD</c> 全为 <c>null</c>，
/// 而 <c>ZCFZL</c>（资产负债率）为 90.91。按「毛利率 &gt; 30%」筛选会把整个银行保险板块
/// <b>静默排除</b>——那不是数据缺失，是行业口径不同，必须能区分并显式提示。
/// </para>
/// </remarks>
public static class FundamentalOrgTypes
{
    /// <summary>通用（非金融）。</summary>
    public const string General = "通用";

    /// <summary>银行。</summary>
    public const string Bank = "银行";

    /// <summary>保险。</summary>
    public const string Insurance = "保险";

    /// <summary>证券。</summary>
    public const string Securities = "证券";

    /// <summary>
    /// 是否为金融业（银行 / 保险 / 证券）。
    /// </summary>
    public static bool IsFinancial(string? orgType)
    {
        var value = orgType?.Trim();
        return value is Bank or Insurance or Securities;
    }

    /// <summary>
    /// 对金融业不适用（上游恒为 <c>null</c>）的指标字段名。
    /// </summary>
    /// <remarks>
    /// 界面据此把空值显示成「不适用」而不是「暂无数据」，选股器据此在筛选时给出提示。
    /// 字段名与 <see cref="FundamentalMetric"/> 的属性名一致，便于前端直接对照。
    /// </remarks>
    public static IReadOnlyList<string> NotApplicableFields { get; } =
    [
        nameof(FundamentalMetric.GrossMargin),
        nameof(FundamentalMetric.CurrentRatio),
        nameof(FundamentalMetric.QuickRatio),
        nameof(FundamentalMetric.Roic),
        nameof(FundamentalMetric.FreeCashFlow)
    ];

    /// <summary>该字段对金融业是否不适用。</summary>
    public static bool IsNotApplicableForFinancials(string field) =>
        NotApplicableFields.Contains(field, StringComparer.Ordinal);
}

/// <summary>
/// 连续性条件的判定结果。
/// </summary>
/// <param name="Satisfied">是否满足「连续 N 年」。</param>
/// <param name="Streak">从最新年度起连续<b>满足条件</b>的年数（遇到不满足或缺年报即停）。</param>
/// <param name="Available">
/// 从最新年度起连续<b>有年报</b>的年数（遇到缺年报即停），与条件是否满足无关。
/// </param>
/// <param name="FailedYear">导致连续中断的年份；全部满足或无数据时为 null。</param>
public readonly record struct ContinuousEvaluation(bool Satisfied, int Streak, int Available, int? FailedYear)
{
    /// <summary>
    /// 历史数据是否不足以支撑判定（可用年数少于要求年数）。
    /// </summary>
    /// <remarks>
    /// 界面据此显示「历史数据不足（已有 M/N 年）」而不是「不满足」——
    /// 两者含义不同：前者是「还没采到」，后者是「确实没做到」。
    /// </remarks>
    public bool InsufficientHistory(int years) => Available < years;
}

/// <summary>
/// 连续性条件的判定规则（实施计划 §5.3）。
/// </summary>
/// <remarks>
/// <para>
/// 价值投资的核心是<b>持续性</b>而非单期数值：「连续 5 年 ROE &gt; 15%」比「今年 ROE 高」
/// 有意义得多。判定有三条硬规则：
/// </para>
/// <list type="number">
/// <item>只取<b>年报</b>（季报是累计口径，混在一条序列里比较无意义）；</item>
/// <item>相邻年份<b>不得跳年</b>：缺年报即中断，避免「2019 与 2025 都 &gt;15%」被误判为连续；</item>
/// <item>任一年缺失该字段值 → 该年不算满足（但仍有年报，因此计入 <see cref="ContinuousEvaluation.Available"/>）。</item>
/// </list>
/// </remarks>
public static class ContinuousConditionRules
{
    /// <summary>
    /// 判断一期指标是否为年报。
    /// </summary>
    /// <remarks>
    /// <b>报告期类型存在时以它为准</b>，只有类型缺失（上游偶有）才退化为「报告期落在 12 月」。
    /// 反过来用「或」会让「2025-12-31 的中报」这种异常组合被当成两条年报，
    /// 从而把同一年的两期算成两年，误判连续性。
    /// </remarks>
    public static bool IsAnnualMetric(FundamentalMetric metric) =>
        string.IsNullOrWhiteSpace(metric.ReportType)
            ? FundamentalReportTypes.IsAnnualDate(metric.ReportDate)
            : FundamentalReportTypes.IsAnnual(metric.ReportType);

    /// <summary>
    /// 判定一条连续性条件。
    /// </summary>
    /// <param name="annualSeries">该标的的年报序列（顺序不限，内部会按报告期排序）。</param>
    /// <param name="value">取值器：从一期指标里取出被判定字段的值；无值返回 null。</param>
    /// <param name="min">下限（含）。</param>
    /// <param name="max">上限（含）。</param>
    /// <param name="years">要求的连续年数。</param>
    public static ContinuousEvaluation Evaluate(
        IEnumerable<FundamentalMetric> annualSeries,
        Func<FundamentalMetric, decimal?> value,
        decimal? min,
        decimal? max,
        int years)
    {
        var required = Math.Max(1, years);

        // 一个自然年度只保留一期（上游同一报告期可能重复出现，按报告期取最新那条）
        var byYear = new Dictionary<int, FundamentalMetric>();
        foreach (var metric in annualSeries)
        {
            if (!IsAnnualMetric(metric))
            {
                continue;
            }

            var year = metric.ReportDate.Year;
            if (!byYear.TryGetValue(year, out var current) || metric.ReportDate > current.ReportDate)
            {
                byYear[year] = metric;
            }
        }

        if (byYear.Count == 0)
        {
            return new ContinuousEvaluation(false, 0, 0, null);
        }

        var newest = byYear.Keys.Max();

        // 可用年数：从最新年度起「连续有年报」的年数，与条件是否满足无关。
        // 必须与 streak 分开算：否则「某年不达标」会被当成「数据不足」，
        // 而这两者的界面提示完全不同（「已有 M/N 年」vs「不满足」）。
        var available = 0;
        for (var year = newest; byYear.ContainsKey(year); year--)
        {
            available++;
        }

        // 连续满足年数：从最新年度往回走，遇到第一个不满足即停。
        // 缺年报同样中断（跳年不得视为连续）。
        var streak = 0;
        int? failedYear = null;

        for (var year = newest; byYear.ContainsKey(year); year--)
        {
            var current = value(byYear[year]);
            var ok = current is not null
                     && (min is null || current >= min)
                     && (max is null || current <= max);

            if (!ok)
            {
                failedYear ??= year;
                break;
            }

            streak++;
        }

        return new ContinuousEvaluation(streak >= required, streak, available, failedYear);
    }
}
