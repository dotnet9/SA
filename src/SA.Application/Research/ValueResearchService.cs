using SA.Application.Abstractions;
using SA.Application.Common;
using SA.Contracts.Common;
using SA.Contracts.Research;
using SA.Domain.Common;
using SA.Domain.Entities.Finance;
using SA.Domain.Entities.Research;

namespace SA.Application.Research;

/// <summary>
/// 个股「价值研究」聚合服务（实施计划 §6.1）。
/// </summary>
/// <remarks>
/// <para>
/// <b>结构：结论区 + 七组检查清单</b>，每组回答一个具体问题（「靠什么赚钱」「会不会突然出事」），
/// 而不是把无关信息堆在同屏。
/// </para>
/// <para>
/// <b>三态（实为四态）必须区分</b>，这是「不做投资建议、只讲事实」的落地方式：
/// </para>
/// <list type="bullet">
/// <item><c>ok</c>：有数据，显示实测值。</item>
/// <item><c>noData</c>：该股该字段为空（实测东芯股份 <c>STAFF_NUM</c> 为 null）。</item>
/// <item><c>notApplicable</c>：行业口径不同（实测平安银行毛利率/流动比率/ROIC/自由现金流为 null）。</item>
/// <item><c>noUpcoming</c>：<b>确实没有</b>待解禁（实测东芯股份 <c>xsjj</c> 为空且已全流通）——
/// 措辞必须与「暂无数据」区分，这个区别对判断筹码压力很关键。</item>
/// </list>
/// <para>
/// <b>结论区只陈述客观数值</b>，不给买入/卖出/持有判断、不给评分、不给目标价。
/// </para>
/// </remarks>
public sealed class ValueResearchService(
    IInstrumentStore instruments,
    IFundamentalStore fundamentals,
    IFinanceStore finance,
    IResearchStore research,
    IEquityStore equity,
    IQuoteSnapshotStore quotes)
{
    /// <summary>
    /// 历史财务序列（「周期位置」组的图表数据源）。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="years">最多回看多少年。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<ServiceResult<FundamentalHistoryDto>> GetHistoryAsync(
        string code,
        int years = 10,
        CancellationToken cancellationToken = default)
    {
        var all = await fundamentals.GetHistoryAsync(code, cancellationToken).ConfigureAwait(false);
        if (all.Count == 0)
        {
            return ServiceResult<FundamentalHistoryDto>.Fail(
                ErrorCode.DataNotReady, $"{code} 的历史财务尚未采集，请稍后重试");
        }

        var cutoff = all[^1].ReportDate.AddYears(-Math.Clamp(years, 1, 20));
        var points = all
            .Where(metric => metric.ReportDate >= cutoff)
            .Select(ToHistoryPoint)
            .ToList();

        var annual = all
            .Where(metric => ContinuousConditionRules.IsAnnualMetric(metric))
            .Where(metric => metric.ReportDate >= cutoff)
            .Select(ToHistoryPoint)
            .ToList();

        return ServiceResult<FundamentalHistoryDto>.Success(new FundamentalHistoryDto(code, points, annual));
    }

    private static FundamentalHistoryPointDto ToHistoryPoint(FundamentalMetric metric) =>
        new(
            SaTime.Format(metric.ReportDate),
            metric.ReportType,
            metric.Revenue,
            metric.NetProfit,
            metric.RoeWeighted,
            metric.GrossMargin,
            metric.NetMargin);

    /// <summary>
    /// 主营构成（含业务范围与经营评述原文）。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <remarks>
    /// <b>子项与父项分开返回</b>：「<c>其中:</c>」开头的项目已计入父项，
    /// 若与父项并列展示会让收入重复计算（实测东芯 2021 中报有 4 个子项）。
    /// 因此 <c>Items</c> 已剔除子项，子项单独放在 <c>SubItems</c> 供「展开子项」用。
    /// </remarks>
    public async Task<ServiceResult<BusinessCompositionDto>> GetCompositionAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var rows = await research.GetCompositionsAsync(code, cancellationToken).ConfigureAwait(false);
        var profile = await research.GetProfileAsync(code, cancellationToken).ConfigureAwait(false);

        if (rows.Count == 0)
        {
            return ServiceResult<BusinessCompositionDto>.Fail(
                ErrorCode.DataNotReady, $"{code} 的主营构成尚未采集，请稍后重试");
        }

        var reportDates = rows
            .Select(row => row.ReportDate)
            .Distinct()
            .OrderByDescending(date => date)
            .Select(SaTime.Format)
            .ToList();

        return ServiceResult<BusinessCompositionDto>.Success(new BusinessCompositionDto(
            Code: code,
            ReportDates: reportDates,
            Items: rows.Where(row => !row.IsSubItem).Select(ToCompositionItem).ToList(),
            SubItems: rows.Where(row => row.IsSubItem).Select(ToCompositionItem).ToList(),
            BusinessScope: profile?.BusinessScope,
            BusinessReview: profile?.BusinessReview));
    }

    private static BusinessCompositionItemDto ToCompositionItem(BusinessComposition row) =>
        new(
            SaTime.Format(row.ReportDate),
            row.MainOpType,
            row.ItemName,
            row.Income,
            // 占比与毛利率保持上游的小数口径（0.652002 = 65.20%），由前端统一 ×100。
            // 这里转一次、前端再转一次会得到 6520%。
            row.IncomeRatio,
            row.GrossProfitRatio,
            row.IsSubItem);

    /// <summary>
    /// 股本结构与限售解禁。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<ServiceResult<ShareStructureDto>> GetShareStructureAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var changes = await research.GetShareChangesAsync(code, cancellationToken).ConfigureAwait(false);
        var unlocks = await research.GetUpcomingUnlocksAsync(code, cancellationToken).ConfigureAwait(false);

        if (changes.Count == 0 && unlocks.Count == 0)
        {
            return ServiceResult<ShareStructureDto>.Fail(
                ErrorCode.DataNotReady, $"{code} 的股本结构尚未采集，请稍后重试");
        }

        return ServiceResult<ShareStructureDto>.Success(new ShareStructureDto(
            Code: code,
            Changes: changes.Select(row => new ShareChangeDto(
                SaTime.Format(row.EndDate),
                row.TotalShares,
                row.LimitedShares,
                row.UnlimitedShares,
                row.ChangeReason)).ToList(),
            UpcomingUnlocks: unlocks.Select(row => new UpcomingUnlockDto(
                SaTime.Format(row.LiftDate),
                row.LiftType,
                row.LiftShares,
                row.TotalSharesRatio,
                row.UnlimitedASharesRatio)).ToList(),
            // 空列表是「暂无待解禁」而不是「暂无数据」：界面措辞必须区分
            UnlockStatus: unlocks.Count == 0 ? ResearchStatuses.NoUpcoming : ResearchStatuses.Ok));
    }

    /// <summary>
    /// 公告列表（只含标题 / 日期 / 类型 / 链接，不含正文）。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="columnName">按类型过滤；为空表示全部。</param>
    /// <param name="limit">条数上限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<ServiceResult<AnnouncementListDto>> GetAnnouncementsAsync(
        string code,
        string? columnName = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var items = await research.GetAnnouncementsAsync(code, columnName, limit, cancellationToken).ConfigureAwait(false);
        var types = await research.GetAnnouncementTypesAsync(code, cancellationToken).ConfigureAwait(false);

        if (items.Count == 0 && types.Count == 0)
        {
            return ServiceResult<AnnouncementListDto>.Fail(
                ErrorCode.DataNotReady, $"{code} 的公告尚未采集，请稍后重试");
        }

        return ServiceResult<AnnouncementListDto>.Success(new AnnouncementListDto(
            Code: code,
            Total: types.Values.Sum(),
            Types: types,
            Items: items.Select(row => new AnnouncementDto(
                row.ArtCode,
                row.Title,
                SaTime.Format(row.NoticeDate),
                row.ColumnName,
                row.AnnType,
                row.Url)).ToList()));
    }

    /// <summary>
    /// 研报列表（<b>不含目标价</b>）。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="limit">篇数上限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<ServiceResult<ResearchListDto>> GetReportsAsync(
        string code,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var items = await research.GetReportsAsync(code, limit, cancellationToken).ConfigureAwait(false);

        return ServiceResult<ResearchListDto>.Success(new ResearchListDto(
            Code: code,
            Items: items.Select(row => new ResearchReportDto(
                row.InfoCode,
                row.Title,
                row.OrgShortName,
                row.Researcher,
                SaTime.Format(row.PublishDate),
                row.RatingName,
                row.IndustryName,
                row.PredictThisYearEps,
                row.PredictThisYearPe,
                row.PredictNextYearEps,
                row.PredictNextYearPe,
                row.PredictNextTwoYearEps,
                row.PredictNextTwoYearPe,
                row.Url)).ToList(),
            AimPriceNote: "公开研报接口未提供目标价（实测 indvAimPriceT / indvAimPriceL 全为空字符串），"
                          + "因此这里只展示评级与三年盈利预测，不提供目标价。"));
    }

    /// <summary>
    /// 七组的定义（顺序即展示顺序）。
    /// </summary>
    private static readonly (string Key, string Name, string Question)[] GroupDefinitions =
    [
        ("business", "生意", "靠什么赚钱"),
        ("cycle", "周期位置", "现在贵还是便宜（相对自身历史）"),
        ("moat", "竞争力", "有没有护城河"),
        ("quality", "财务质量", "赚的是不是真钱"),
        ("safety", "安全性", "会不会突然出事"),
        ("governance", "治理与筹码", "大股东在干什么"),
        ("valuation", "估值与观点", "市场怎么看")
    ];

    /// <summary>口径说明（与选股器一致，由后端下发）。</summary>
    private static readonly string[] CaliberNotes =
    [
        "金融业（银行/保险/证券）的毛利率、流动比率、速动比率、自由现金流、ROIC 上游不提供，"
            + "界面显示「不适用」而不是「暂无数据」——那是行业口径不同，不是数据缺失。",
        "资产负债率对金融业天然偏高（实测平安银行 90.91%，东芯股份 9.76%），两者不可用同一阈值判断。",
        "研报不展示目标价：实测上游 indvAimPriceT / indvAimPriceL 全为空字符串，"
            + "按「不造目标价、不拿别处的价凑」处理。",
        "经营评述为主营分析原文，只做展示、不做解析。",
        "主营构成的「按行业 / 按产品 / 按地区」是三套并列口径，各自合计 100%，不能相加。",
        "「其中:」开头的项目是子项，已计入父项，因此不与父项并列展示。",
        "限售解禁的「暂无待解禁」表示确实没有（已全流通），与「暂无数据」含义不同。"
    ];

    /// <summary>
    /// 组装价值研究结果。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<ServiceResult<ValueResearchDto>> GetAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var instrument = await instruments.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (instrument is null)
        {
            return ServiceResult<ValueResearchDto>.Fail(ErrorCode.NotFound, $"未找到标的：{code}");
        }

        var latest = (await fundamentals.GetHistoryAsync(code, cancellationToken).ConfigureAwait(false))
            .OrderByDescending(metric => metric.ReportDate)
            .FirstOrDefault();
        var annual = await fundamentals.GetAnnualByCodeAsync([code], cancellationToken).ConfigureAwait(false);
        annual.TryGetValue(code, out var annualSeries);

        var reports = await finance.GetReportsAsync(code, 24, cancellationToken).ConfigureAwait(false);
        var latestReport = reports.Count > 0 ? reports[^1] : null;

        var composition = await research.GetCompositionsAsync(code, cancellationToken).ConfigureAwait(false);
        var profile = await research.GetProfileAsync(code, cancellationToken).ConfigureAwait(false);
        var shareChanges = await research.GetShareChangesAsync(code, cancellationToken).ConfigureAwait(false);
        var unlocks = await research.GetUpcomingUnlocksAsync(code, cancellationToken).ConfigureAwait(false);
        var announcements = await research.GetAnnouncementsAsync(code, null, 20, cancellationToken).ConfigureAwait(false);
        var researchReports = await research.GetReportsAsync(code, 20, cancellationToken).ConfigureAwait(false);

        var holders = await equity.GetTopHoldersAsync(code, cancellationToken).ConfigureAwait(false);
        var holderCounts = await equity.GetHolderCountsAsync(code, cancellationToken).ConfigureAwait(false);
        var pledge = await equity.GetPledgeAsync(code, cancellationToken).ConfigureAwait(false);

        var quote = (await quotes.GetByCodesAsync([code], cancellationToken).ConfigureAwait(false))
            .TryGetValue(code, out var snapshot)
                ? snapshot
                : null;

        var isFinancial = FundamentalOrgTypes.IsFinancial(latest?.OrgType);

        var groups = new List<ValueResearchGroupDto>
        {
            BuildBusinessGroup(code, composition, profile),
            BuildCycleGroup(annualSeries),
            BuildMoatGroup(latest),
            BuildQualityGroup(latest),
            BuildSafetyGroup(latest, isFinancial),
            BuildGovernanceGroup(shareChanges, unlocks, holders, holderCounts, pledge),
            BuildValuationGroup(quote, researchReports, announcements, latest)
        };

        // 分组顺序与定义一致（构造顺序已保证，这里按定义再排一次以防漏项）
        groups = [.. GroupDefinitions
            .Select(definition => groups.FirstOrDefault(group => group.Key == definition.Key))
            .Where(group => group is not null)
            .Select(group => group!)];

        return ServiceResult<ValueResearchDto>.Success(new ValueResearchDto(
            Code: code,
            Name: instrument.Name,
            Board: instrument.Board,
            Industry: instrument.Industry,
            Conclusion: BuildConclusion(latest, latestReport, annualSeries),
            Groups: groups,
            CaliberNotes: CaliberNotes,
            FundamentalAsOf: latest is null ? null : SaTime.Format(latest.ReportDate)));
    }

    /// <summary>
    /// 结论区：一行客观事实 + 关键数字。
    /// </summary>
    /// <remarks>
    /// <b>只陈述数值，不做判断</b>：不出现「买入」「看好」「估值合理」等措辞，也不给综合评分。
    /// </remarks>
    private static ValueResearchConclusionDto BuildConclusion(
        FundamentalMetric? latest,
        FinancialReport? latestReport,
        IReadOnlyList<FundamentalMetric>? annualSeries)
    {
        if (latest is null)
        {
            return new ValueResearchConclusionDto(
                "暂无财报数据",
                [],
                null);
        }

        var parts = new List<string>();
        var period = $"{latest.ReportDate.Year} 年"
                     + (latest.ReportDate.Month switch
                     {
                         3 => "一季报",
                         6 => "中报",
                         9 => "三季报",
                         12 => "年报",
                         _ => SaTime.Format(latest.ReportDate)
                     });

        if (latest.Revenue is { } revenue)
        {
            var yoy = latest.RevenueYoy is { } growth ? $"（{growth:+0.0;-0.0}%）" : string.Empty;
            parts.Add($"营收 {Display.ToYi(revenue):F2} 亿{yoy}");
        }

        if (latest.GrossMargin is { } grossMargin)
        {
            parts.Add($"毛利率 {grossMargin:F2}%");
        }

        if (latest.RoeWeighted is { } roe)
        {
            parts.Add($"ROE {roe:F2}%");
        }

        if (latest.DebtRatio is { } debt)
        {
            parts.Add($"资产负债率 {debt:F2}%");
        }

        if (latest.OperatingCashFlowToNetProfit is { } cashRatio)
        {
            parts.Add($"经营现金流÷净利润 {cashRatio:F3}");
        }

        var metrics = new List<ValueResearchMetricDto>
        {
            Metric("营收", latest.Revenue is { } r ? $"{Display.ToYi(r):F2} 亿" : null, latest.Revenue is not null),
            Metric("毛利率", latest.GrossMargin is { } gm ? $"{gm:F2}%" : null, latest.GrossMargin is not null),
            Metric("净利率", latest.NetMargin is { } nm ? $"{nm:F2}%" : null, latest.NetMargin is not null),
            Metric("ROE", latest.RoeWeighted is { } roeValue ? $"{roeValue:F2}%" : null, latest.RoeWeighted is not null),
            Metric("资产负债率", latest.DebtRatio is { } dr ? $"{dr:F2}%" : null, latest.DebtRatio is not null),
            Metric(
                "经营现金流÷净利润",
                latest.OperatingCashFlowToNetProfit is { } nco ? $"{nco:F3}" : null,
                latest.OperatingCashFlowToNetProfit is not null)
        };

        // 连续年报年数：与连续性条件同一口径（缺年报即中断）
        var annualYears = AnnualYears(annualSeries);
        if (annualYears > 0)
        {
            metrics.Add(new ValueResearchMetricDto("已采集年报", $"{annualYears} 年", ResearchStatuses.Ok));
        }

        return new ValueResearchConclusionDto(
            $"{period}：{string.Join("／", parts)}",
            metrics,
            SaTime.Format(latest.ReportDate));
    }

    private static ValueResearchMetricDto Metric(string name, string? value, bool hasValue) =>
        new(name, value ?? "暂无数据", hasValue ? ResearchStatuses.Ok : ResearchStatuses.NoData);

    /* ------------------------------------------------------------------
       1 生意
       ------------------------------------------------------------------ */

    private static ValueResearchGroupDto BuildBusinessGroup(
        string code,
        IReadOnlyList<BusinessComposition> composition,
        BusinessProfile? profile)
    {
        var items = new List<ValueResearchItemDto>();

        // 最新报告期的「按产品」口径：价值投资者最先看的就是这条
        var latestDate = composition.Count > 0 ? composition.Max(row => row.ReportDate) : (DateOnly?)null;
        var byProduct = latestDate is null
            ? []
            : composition
                .Where(row => row.ReportDate == latestDate.Value && row.MainOpType == 2 && !row.IsSubItem)
                .OrderBy(row => row.Rank)
                .ToList();

        if (byProduct.Count > 0)
        {
            var top = byProduct[0];
            items.Add(new ValueResearchItemDto(
                Key: "mainProduct",
                Name: "最大产品",
                Group: "business",
                // 占比是小数字段，展示时 ×100
                Value: $"{top.ItemName} {Percent(top.IncomeRatio)}",
                Unit: null,
                AsOf: SaTime.Format(latestDate!.Value),
                Status: ResearchStatuses.Ok,
                Source: "东财 F10 主营构成（按产品）"));
        }

        items.Add(new ValueResearchItemDto(
            Key: "productCount",
            Name: "产品数",
            Group: "business",
            Value: byProduct.Count == 0 ? null : byProduct.Count.ToString(),
            Unit: "个",
            AsOf: latestDate is null ? null : SaTime.Format(latestDate.Value),
            Status: byProduct.Count == 0 ? ResearchStatuses.NoData : ResearchStatuses.Ok,
            Source: "东财 F10 主营构成（已排除「其中:」子项）"));

        items.Add(new ValueResearchItemDto(
            Key: "businessScope",
            Name: "业务范围",
            Group: "business",
            Value: profile?.BusinessScope is { Length: > 0 } scope ? Truncate(scope, 120) : null,
            Unit: null,
            AsOf: null,
            Status: string.IsNullOrWhiteSpace(profile?.BusinessScope) ? ResearchStatuses.NoData : ResearchStatuses.Ok,
            Source: "东财 F10 经营分析（原文）"));

        items.Add(new ValueResearchItemDto(
            Key: "businessReview",
            Name: "经营评述",
            Group: "business",
            Value: string.IsNullOrWhiteSpace(profile?.BusinessReview)
                ? null
                : $"原文 {profile.BusinessReview.Length} 字（展开查看）",
            Unit: null,
            AsOf: null,
            Status: string.IsNullOrWhiteSpace(profile?.BusinessReview) ? ResearchStatuses.NoData : ResearchStatuses.Ok,
            Source: "东财 F10 经营分析（只展示原文，不做解析）"));

        return Group("business", items, defaultExpanded: true);
    }

    /* ------------------------------------------------------------------
       2 周期位置
       ------------------------------------------------------------------ */

    /// <summary>
    /// 周期位置：营收 / 毛利率 / 净利 / ROE 的近 8–10 年序列。
    /// </summary>
    /// <remarks>
    /// 这是本轮<b>唯一允许的图表</b>（趋势线）：只有它需要「历史对照」才能回答
    /// 「这是周期高点还是起点」。其余各组的数字单点即可判断。
    /// </remarks>
    private static ValueResearchGroupDto BuildCycleGroup(IReadOnlyList<FundamentalMetric>? annualSeries)
    {
        var series = (annualSeries ?? []).OrderBy(metric => metric.ReportDate).ToList();
        var items = new List<ValueResearchItemDto>();

        if (series.Count == 0)
        {
            items.Add(NoData("revenueSeries", "营收序列", "cycle", "东财基本面（年报）"));
            return Group("cycle", items, defaultExpanded: true);
        }

        // 样本不足时如实说明，不插值也不猜（§10「新股 / 次新：指标样本不足时返回 null」）
        var span = $"{series.Count} 年（{SaTime.Format(series[0].ReportDate)} 起）";
        var latest = series[^1];
        var earliest = series[0];

        items.Add(new ValueResearchItemDto(
            "revenueSeries", "营收序列", "cycle",
            $"{span}：{Display.ToYi(earliest.Revenue ?? 0m):F2} → {Display.ToYi(latest.Revenue ?? 0m):F2} 亿",
            "亿元", SaTime.Format(latest.ReportDate), ResearchStatuses.Ok,
            "东财基本面（仅年报）",
            series.Select(metric => new ValueResearchHistoryPointDto(
                SaTime.Format(metric.ReportDate),
                metric.Revenue is { } value ? Display.ToYi(value) : null)).ToList()));

        items.Add(new ValueResearchItemDto(
            "grossMarginSeries", "毛利率序列", "cycle",
            $"{Percent(earliest.GrossMargin)} → {Percent(latest.GrossMargin)}",
            "%", SaTime.Format(latest.ReportDate), ResearchStatuses.Ok,
            "东财基本面（仅年报）",
            series.Select(metric => new ValueResearchHistoryPointDto(
                SaTime.Format(metric.ReportDate), metric.GrossMargin)).ToList()));

        items.Add(new ValueResearchItemDto(
            "netProfitSeries", "净利润序列", "cycle",
            $"{Display.ToYi(earliest.NetProfit ?? 0m):F2} → {Display.ToYi(latest.NetProfit ?? 0m):F2} 亿",
            "亿元", SaTime.Format(latest.ReportDate), ResearchStatuses.Ok,
            "东财基本面（仅年报）",
            series.Select(metric => new ValueResearchHistoryPointDto(
                SaTime.Format(metric.ReportDate),
                metric.NetProfit is { } value ? Display.ToYi(value) : null)).ToList()));

        items.Add(new ValueResearchItemDto(
            "roeSeries", "ROE 序列", "cycle",
            $"{Percent(earliest.RoeWeighted)} → {Percent(latest.RoeWeighted)}",
            "%", SaTime.Format(latest.ReportDate), ResearchStatuses.Ok,
            "东财基本面（仅年报）",
            series.Select(metric => new ValueResearchHistoryPointDto(
                SaTime.Format(metric.ReportDate), metric.RoeWeighted)).ToList()));

        return Group("cycle", items, defaultExpanded: true);
    }

    /* ------------------------------------------------------------------
       3 竞争力
       ------------------------------------------------------------------ */

    private static ValueResearchGroupDto BuildMoatGroup(FundamentalMetric? latest)
    {
        var items = new List<ValueResearchItemDto>
        {
            Number("rndExpense", "研发费用", "moat", latest?.RndExpense is { } rnd ? Display.ToYi(rnd) : null, "亿元", latest, "东财基本面"),
            Number("rndExpenseRatio", "研发费用率", "moat", latest?.RndExpenseRatio, "%", latest, "东财基本面"),
            Number("rndPersonnel", "研发人员", "moat", latest?.RndPersonnel, "人", latest, "东财基本面"),
            Number("grossMargin", "毛利率", "moat", latest?.GrossMargin, "%", latest, "东财基本面", notApplicableForFinancials: true),
            Number("staffNumber", "员工总数", "moat", latest?.StaffNumber, "人", latest, "东财基本面")
        };

        // 员工总数实测常为 null（东芯股份即如此）：按 §1.1 显示「暂无数据」，
        // 不得回退到估算值或行业均值
        return Group("moat", items, defaultExpanded: false);
    }

    /* ------------------------------------------------------------------
       4 财务质量
       ------------------------------------------------------------------ */

    private static ValueResearchGroupDto BuildQualityGroup(FundamentalMetric? latest)
    {
        var items = new List<ValueResearchItemDto>
        {
            Number("roe", "加权 ROE", "quality", latest?.RoeWeighted, "%", latest, "东财基本面"),
            Number("roeDeducted", "扣非 ROE", "quality", latest?.RoeDeducted, "%", latest, "东财基本面"),
            Number("grossMargin", "毛利率", "quality", latest?.GrossMargin, "%", latest, "东财基本面", notApplicableForFinancials: true),
            Number("netMargin", "净利率", "quality", latest?.NetMargin, "%", latest, "东财基本面"),
            Number("roic", "ROIC", "quality", latest?.Roic, "%", latest, "东财基本面", notApplicableForFinancials: true),
            Number("operatingCashFlowToNetProfit", "经营现金流÷净利润", "quality", latest?.OperatingCashFlowToNetProfit, "倍", latest, "东财基本面"),
            Number("operatingCashFlowToRevenue", "经营现金流÷营收", "quality", latest?.OperatingCashFlowToRevenue, "倍", latest, "东财基本面")
        };

        return Group("quality", items, defaultExpanded: false);
    }

    /* ------------------------------------------------------------------
       5 安全性
       ------------------------------------------------------------------ */

    private static ValueResearchGroupDto BuildSafetyGroup(FundamentalMetric? latest, bool isFinancial)
    {
        var items = new List<ValueResearchItemDto>
        {
            Number("debtRatio", "资产负债率", "safety", latest?.DebtRatio, "%", latest, "东财基本面"),
            Number("currentRatio", "流动比率", "safety", latest?.CurrentRatio, "倍", latest, "东财基本面", isFinancial),
            Number("quickRatio", "速动比率", "safety", latest?.QuickRatio, "倍", latest, "东财基本面", isFinancial),
            Number("interestDebtRatio", "有息负债率", "safety", latest?.InterestDebtRatio, "%", latest, "东财基本面"),
            Number("interestCoverageRatio", "利息保障倍数", "safety", latest?.InterestCoverageRatio, "倍", latest, "东财基本面"),
            Number("freeCashFlow", "自由现金流", "safety", latest?.FreeCashFlow is { } fcf ? Display.ToYi(fcf) : null, "亿元", latest, "东财基本面", isFinancial),
            Number("liquidationRatio", "清算价值比率", "safety", latest?.LiquidationRatio, "%", latest, "东财基本面")
        };

        return Group("safety", items, defaultExpanded: false);
    }

    /* ------------------------------------------------------------------
       6 治理与筹码
       ------------------------------------------------------------------ */

    private static ValueResearchGroupDto BuildGovernanceGroup(
        IReadOnlyList<ShareChange> shareChanges,
        IReadOnlyList<UpcomingUnlock> unlocks,
        IReadOnlyList<Domain.Entities.Equity.TopHolder> holders,
        IReadOnlyList<Domain.Entities.Equity.HolderCount> holderCounts,
        Domain.Entities.Equity.PledgeStat? pledge)
    {
        var items = new List<ValueResearchItemDto>();
        var latestChange = shareChanges.Count > 0 ? shareChanges[0] : null;

        items.Add(new ValueResearchItemDto(
            "totalShares", "总股本", "governance",
            latestChange?.TotalShares is { } total ? $"{Display.ToYi(total):F2} 亿股" : null,
            "亿股",
            latestChange is null ? null : SaTime.Format(latestChange.EndDate),
            latestChange?.TotalShares is null ? ResearchStatuses.NoData : ResearchStatuses.Ok,
            "东财 F10 股本结构"));

        // 有限售股：实测 null 与 0 都表示「没有限售股」，因此两者都显示 0 而不是「暂无数据」
        var limited = latestChange?.LimitedShares ?? 0m;
        items.Add(new ValueResearchItemDto(
            "limitedShares", "有限售股份", "governance",
            latestChange is null ? null : $"{Display.ToYi(limited):F2} 亿股",
            "亿股",
            latestChange is null ? null : SaTime.Format(latestChange.EndDate),
            latestChange is null ? ResearchStatuses.NoData : ResearchStatuses.Ok,
            "东财 F10 股本结构（null 与 0 均表示无限售股）"));

        // 限售解禁：空列表是「暂无待解禁」而不是「暂无数据」
        if (unlocks.Count == 0)
        {
            items.Add(new ValueResearchItemDto(
                "upcomingUnlock", "待解禁", "governance",
                "暂无待解禁",
                null, null, ResearchStatuses.NoUpcoming,
                "东财 F10 股本结构（该股已全流通）"));
        }
        else
        {
            var next = unlocks[0];
            items.Add(new ValueResearchItemDto(
                "upcomingUnlock", "待解禁", "governance",
                $"{SaTime.Format(next.LiftDate)} 解禁 {Display.ToYi(next.LiftShares ?? 0m):F2} 亿股"
                + (next.TotalSharesRatio is { } ratio ? $"（占总股本 {ratio:F2}%）" : string.Empty),
                null, SaTime.Format(next.LiftDate), ResearchStatuses.Ok,
                $"东财 F10 股本结构（共 {unlocks.Count} 条待解禁）"));
        }

        items.Add(new ValueResearchItemDto(
            "shareChangeReason", "最近股本变动", "governance",
            latestChange?.ChangeReason,
            null,
            latestChange is null ? null : SaTime.Format(latestChange.EndDate),
            string.IsNullOrWhiteSpace(latestChange?.ChangeReason) ? ResearchStatuses.NoData : ResearchStatuses.Ok,
            "东财 F10 股本结构（变动原因）"));

        var latestHolders = holders.Where(holder => !holder.IsFreeFloat).ToList();
        items.Add(new ValueResearchItemDto(
            "topHolders", "十大股东", "governance",
            latestHolders.Count == 0 ? null : $"{latestHolders.Count} 位（最新报告期）",
            null,
            latestHolders.Count == 0 ? null : SaTime.Format(latestHolders.Max(holder => holder.EndDate)),
            latestHolders.Count == 0 ? ResearchStatuses.NoData : ResearchStatuses.Ok,
            "东财 F10 十大股东"));

        var latestCount = holderCounts.Count > 0
            ? holderCounts.OrderByDescending(count => count.EndDate).First()
            : null;
        items.Add(new ValueResearchItemDto(
            "holderCount", "股东户数", "governance",
            latestCount is null ? null : $"{latestCount.HolderNum:N0} 户",
            "户",
            latestCount is null ? null : SaTime.Format(latestCount.EndDate),
            latestCount is null ? ResearchStatuses.NoData : ResearchStatuses.Ok,
            "东财 F10 股东研究"));

        items.Add(new ValueResearchItemDto(
            "pledge", "股权质押", "governance",
            pledge?.PledgeRatio is { } pledgeRatio ? $"{pledgeRatio:F2}%" : null,
            "%",
            pledge is null ? null : SaTime.Format(pledge.TradeDate),
            pledge?.PledgeRatio is null ? ResearchStatuses.NoData : ResearchStatuses.Ok,
            "东财股权质押"));

        return Group("governance", items, defaultExpanded: false);
    }

    /* ------------------------------------------------------------------
       7 估值与观点
       ------------------------------------------------------------------ */

    private static ValueResearchGroupDto BuildValuationGroup(
        Domain.Entities.Market.QuoteSnapshot? quote,
        IReadOnlyList<ResearchReport> researchReports,
        IReadOnlyList<Announcement> announcements,
        FundamentalMetric? latest)
    {
        var items = new List<ValueResearchItemDto>();

        items.Add(new ValueResearchItemDto(
            "peTtm", "PE(TTM)", "valuation",
            quote?.PeTtm is > 0 ? $"{quote.PeTtm:F2}" : null,
            "倍",
            quote is null ? null : SaTime.Format(quote.AsOf),
            quote?.PeTtm is > 0 ? ResearchStatuses.Ok : ResearchStatuses.NoData,
            "全市场快照（亏损股无 PE）"));

        items.Add(new ValueResearchItemDto(
            "pb", "PB", "valuation",
            quote?.Pb is > 0 ? $"{quote.Pb:F2}" : null,
            "倍",
            quote is null ? null : SaTime.Format(quote.AsOf),
            quote?.Pb is > 0 ? ResearchStatuses.Ok : ResearchStatuses.NoData,
            "全市场快照"));

        items.Add(new ValueResearchItemDto(
            "eps", "每股收益", "valuation",
            latest?.Eps is { } eps ? $"{eps:F2}" : null,
            "元",
            latest is null ? null : SaTime.Format(latest.ReportDate),
            latest?.Eps is null ? ResearchStatuses.NoData : ResearchStatuses.Ok,
            "东财基本面"));

        items.Add(new ValueResearchItemDto(
            "bps", "每股净资产", "valuation",
            latest?.Bps is { } bps ? $"{bps:F2}" : null,
            "元",
            latest is null ? null : SaTime.Format(latest.ReportDate),
            latest?.Bps is null ? ResearchStatuses.NoData : ResearchStatuses.Ok,
            "东财基本面"));

        if (researchReports.Count == 0)
        {
            items.Add(NoData("rating", "机构评级", "valuation", "东财研报列表（该股暂无研报覆盖）"));
        }
        else
        {
            var newest = researchReports[0];
            var ratings = researchReports
                .Where(report => !string.IsNullOrWhiteSpace(report.RatingName))
                .GroupBy(report => report.RatingName!)
                .Select(group => $"{group.Key} {group.Count()}")
                .ToList();

            items.Add(new ValueResearchItemDto(
                "rating", "机构评级", "valuation",
                $"{researchReports.Count} 篇；最新 {SaTime.Format(newest.PublishDate)} {newest.RatingName}"
                + (ratings.Count > 0 ? $"（{string.Join('，', ratings)}）" : string.Empty),
                null, SaTime.Format(newest.PublishDate), ResearchStatuses.Ok,
                "东财研报列表"));
        }

        // 三年盈利预测：原样展示、不做解读（高 PE 本身是有效信息）
        if (researchReports.Count > 0)
        {
            var newest = researchReports[0];
            items.Add(new ValueResearchItemDto(
                "predictPe", "三年 PE 预测", "valuation",
                Predictions(newest),
                "倍",
                SaTime.Format(newest.PublishDate),
                newest.PredictThisYearPe is null ? ResearchStatuses.NoData : ResearchStatuses.Ok,
                "东财研报列表（原样展示，不做解读）"));
        }

        items.Add(new ValueResearchItemDto(
            "recentAnnouncement", "近期公告", "valuation",
            announcements.Count == 0
                ? null
                : $"{SaTime.Format(announcements[0].NoticeDate)} {Truncate(announcements[0].Title, 40)}",
            null,
            announcements.Count == 0 ? null : SaTime.Format(announcements[0].NoticeDate),
            announcements.Count == 0 ? ResearchStatuses.NoData : ResearchStatuses.Ok,
            $"东财公告列表（已采集 {announcements.Count} 条）"));

        return Group("valuation", items, defaultExpanded: false);
    }
    /* ------------------------------------------------------------------
       构造辅助
       ------------------------------------------------------------------ */

    /// <summary>
    /// 构造一个数值型检查项，并判定「不适用 / 暂无数据 / 有数据」三态。
    /// </summary>
    /// <param name="key">字段键。</param>
    /// <param name="name">中文名。</param>
    /// <param name="group">组键。</param>
    /// <param name="value">数值；null 表示缺失。</param>
    /// <param name="unit">单位。</param>
    /// <param name="latest">最新一期基本面（提供报告期）。</param>
    /// <param name="source">来源说明。</param>
    /// <param name="notApplicableForFinancials">
    /// 该字段对金融业是否不适用。为 true 且该股是金融业且值为 null 时，
    /// 状态是 <c>notApplicable</c> 而不是 <c>noData</c>——这个区分是 §1.3 的关键落地。
    /// </param>
    private static ValueResearchItemDto Number(
        string key,
        string name,
        string group,
        decimal? value,
        string unit,
        FundamentalMetric? latest,
        string source,
        bool notApplicableForFinancials = false)
    {
        var asOf = latest is null ? null : SaTime.Format(latest.ReportDate);
        var isFinancial = FundamentalOrgTypes.IsFinancial(latest?.OrgType);

        if (value is null)
        {
            var status = notApplicableForFinancials && isFinancial
                ? ResearchStatuses.NotApplicable
                : ResearchStatuses.NoData;

            return new ValueResearchItemDto(
                key, name, group,
                status == ResearchStatuses.NotApplicable ? "不适用" : null,
                unit, asOf, status, source);
        }

        return new ValueResearchItemDto(
            key, name, group,
            value.Value.ToString("0.##"),
            unit, asOf, ResearchStatuses.Ok, source);
    }

    private static ValueResearchItemDto NoData(string key, string name, string group, string source) =>
        new(key, name, group, null, null, null, ResearchStatuses.NoData, source);

    private static ValueResearchGroupDto Group(
        string key,
        List<ValueResearchItemDto> items,
        bool defaultExpanded)
    {
        var definition = GroupDefinitions.First(entry => entry.Key == key);
        return new ValueResearchGroupDto(key, definition.Name, definition.Question, items, defaultExpanded);
    }

    /// <summary>占比字段是小数字段，展示时转百分数。</summary>
    private static string Percent(decimal? ratio) =>
        ratio is null ? "—" : $"{ratio.Value * 100m:F2}%";

    private static string Predictions(ResearchReport report)
    {
        var parts = new List<string>();
        if (report.PredictThisYearPe is { } thisYear)
        {
            parts.Add($"{thisYear:F1}");
        }

        if (report.PredictNextYearPe is { } nextYear)
        {
            parts.Add($"{nextYear:F1}");
        }

        if (report.PredictNextTwoYearPe is { } nextTwo)
        {
            parts.Add($"{nextTwo:F1}");
        }

        return parts.Count == 0 ? "—" : string.Join(" / ", parts);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    /// <summary>从最新年度往回数「连续有年报」的年数（与连续性条件同一口径）。</summary>
    private static int AnnualYears(IReadOnlyList<FundamentalMetric>? annualSeries)
    {
        if (annualSeries is null || annualSeries.Count == 0)
        {
            return 0;
        }

        var years = annualSeries.Select(metric => metric.ReportDate.Year).ToHashSet();
        var newest = years.Max();
        var count = 0;
        for (var year = newest; years.Contains(year); year--)
        {
            count++;
        }

        return count;
    }
}
