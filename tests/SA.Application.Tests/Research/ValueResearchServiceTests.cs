using SA.Application.Abstractions;
using SA.Application.Research;
using SA.Contracts.Research;
using SA.Domain.Entities.Equity;
using SA.Domain.Entities.Finance;
using SA.Domain.Entities.Market;
using SA.Domain.Entities.Research;

namespace SA.Application.Tests.Research;

/// <summary>
/// 价值研究聚合的语义测试（实施计划 §6.1、§6.5）。
/// </summary>
/// <remarks>
/// 重点不是「有没有值」，而是<b>四态是否被正确区分</b>：
/// <list type="bullet">
/// <item>「不适用」（金融业的毛利率等）不能显示成「暂无数据」——那会误导用户去等一个永远不会来的数据；</item>
/// <item>「暂无待解禁」是「确实没有」而不是「没查到」——这个区别对判断筹码压力很关键。</item>
/// </list>
/// </remarks>
public class ValueResearchServiceTests
{
    private static readonly DateOnly Interim = new(2026, 6, 30);

    private static ValueResearchService Build(
        FundamentalMetric? latest = null,
        IReadOnlyList<BusinessComposition>? composition = null,
        IReadOnlyList<UpcomingUnlock>? unlocks = null,
        IReadOnlyList<ShareChange>? changes = null)
    {
        var fundamentals = new List<FundamentalMetric>();
        if (latest is not null)
        {
            fundamentals.Add(latest);
            // 补两条年报，让「周期位置」组有序列可画（单点画不出趋势线）
            fundamentals.Add(new FundamentalMetric
            {
                Code = latest.Code,
                ReportDate = new DateOnly(2025, 12, 31),
                ReportType = FundamentalReportTypes.Annual,
                OrgType = latest.OrgType,
                Revenue = 1_000_000_000m,
                RoeWeighted = 10m,
                GrossMargin = 20m,
                UpdatedAt = DateTimeOffset.Now
            });
            fundamentals.Add(new FundamentalMetric
            {
                Code = latest.Code,
                ReportDate = new DateOnly(2024, 12, 31),
                ReportType = FundamentalReportTypes.Annual,
                OrgType = latest.OrgType,
                Revenue = 900_000_000m,
                RoeWeighted = 8m,
                GrossMargin = 18m,
                UpdatedAt = DateTimeOffset.Now
            });
        }

        return new ValueResearchService(
            new FakeInstruments(),
            new FakeFundamentals(fundamentals),
            new FakeFinance(),
            new FakeResearch(composition ?? [], unlocks ?? [], changes ?? []),
            new FakeEquity(),
            new FakeQuotes());
    }

    private static FundamentalMetric Metric(
        string code = "688110",
        string orgType = "通用",
        decimal? grossMargin = 67.66m,
        decimal? currentRatio = 8.58m,
        decimal? roic = 17.04m,
        decimal? freeCashFlow = 453_291_462.6m,
        decimal? debtRatio = 9.76m,
        decimal? staffNumber = null) =>
        new()
        {
            Code = code,
            ReportDate = Interim,
            ReportType = FundamentalReportTypes.Interim,
            OrgType = orgType,
            RoeWeighted = 17.36m,
            GrossMargin = grossMargin,
            NetMargin = 45.8m,
            Roic = roic,
            DebtRatio = debtRatio,
            CurrentRatio = currentRatio,
            QuickRatio = 5.69m,
            OperatingCashFlowToNetProfit = 0.835m,
            FreeCashFlow = freeCashFlow,
            Revenue = 1_497_000_000m,
            StaffNumber = staffNumber,
            UpdatedAt = DateTimeOffset.Now
        };

    private static ValueResearchItemDto Item(ValueResearchDto dto, string group, string key) =>
        dto.Groups.Single(g => g.Key == group).Items.Single(i => i.Key == key);

    /* ------------------------------------------------------------------
       四态
       ------------------------------------------------------------------ */

    [Fact]
    public async Task 金融业的空字段显示不适用而不是暂无数据()
    {
        // 平安银行实测：毛利率/流动比率/ROIC/自由现金流为 null，而负债率有值
        var service = Build(Metric(
            code: "000001",
            orgType: FundamentalOrgTypes.Bank,
            grossMargin: null,
            currentRatio: null,
            roic: null,
            freeCashFlow: null,
            debtRatio: 90.9m));

        var result = await service.GetAsync("000001");

        var grossMargin = Item(result.Value!, "quality", "grossMargin");
        Assert.Equal(ResearchStatuses.NotApplicable, grossMargin.Status);
        Assert.Equal("不适用", grossMargin.Value);

        Assert.Equal(ResearchStatuses.NotApplicable, Item(result.Value!, "safety", "currentRatio").Status);
        Assert.Equal(ResearchStatuses.NotApplicable, Item(result.Value!, "safety", "freeCashFlow").Status);

        // 负债率有值，正常显示（金融业天然偏高，但不是缺失）
        var debt = Item(result.Value!, "safety", "debtRatio");
        Assert.Equal(ResearchStatuses.Ok, debt.Status);
        Assert.Equal("90.9", debt.Value);
    }

    [Fact]
    public async Task 非金融业的空字段是暂无数据()
    {
        // 同一个字段，对通用行业就是真的缺失
        var service = Build(Metric(grossMargin: null, currentRatio: null));

        var result = await service.GetAsync("688110");

        Assert.Equal(ResearchStatuses.NoData, Item(result.Value!, "quality", "grossMargin").Status);
        Assert.Equal(ResearchStatuses.NoData, Item(result.Value!, "safety", "currentRatio").Status);
    }

    [Fact]
    public async Task 员工总数为空时显示暂无数据不回退到估算值()
    {
        // 实测东芯股份 STAFF_NUM 为 null；§1.1 要求显示「暂无数据」，
        // 不得回退到估算值或行业均值
        var service = Build(Metric(staffNumber: null));

        var result = await service.GetAsync("688110");

        var staff = Item(result.Value!, "moat", "staffNumber");
        Assert.Equal(ResearchStatuses.NoData, staff.Status);
        Assert.Null(staff.Value);
    }

    [Fact]
    public async Task xsjj为空时显示暂无待解禁而不是暂无数据()
    {
        var service = Build(
            Metric(),
            changes:
            [
                new ShareChange
                {
                    Code = "688110",
                    EndDate = new DateOnly(2026, 6, 5),
                    TotalShares = 442_377_391m,
                    UnlimitedShares = 442_377_391m,
                    // 已全流通：有限售股为 null
                    LimitedShares = null,
                    ChangeReason = "限制性股票",
                    UpdatedAt = DateTimeOffset.Now
                }
            ]);

        var result = await service.GetAsync("688110");

        var unlock = Item(result.Value!, "governance", "upcomingUnlock");
        Assert.Equal(ResearchStatuses.NoUpcoming, unlock.Status);
        Assert.Equal("暂无待解禁", unlock.Value);

        // 有限售股 null 表示「没有限售股」，显示 0 而不是「暂无数据」
        var limited = Item(result.Value!, "governance", "limitedShares");
        Assert.Equal(ResearchStatuses.Ok, limited.Status);
        Assert.Contains("0.00", limited.Value!);
    }

    [Fact]
    public async Task 有待解禁时显示解禁日与股数()
    {
        var service = Build(
            Metric(),
            unlocks:
            [
                new UpcomingUnlock
                {
                    Code = "688981",
                    LiftDate = new DateOnly(2027, 6, 23),
                    LiftType = "定向增发机构配售股份",
                    LiftShares = 547_182_073m,
                    TotalSharesRatio = 6.39m,
                    UpdatedAt = DateTimeOffset.Now
                }
            ]);

        var result = await service.GetAsync("688981");

        var unlock = Item(result.Value!, "governance", "upcomingUnlock");
        Assert.Equal(ResearchStatuses.Ok, unlock.Status);
        Assert.Contains("2027-06-23", unlock.Value!);
        Assert.Contains("6.39%", unlock.Value!);
    }

    /* ------------------------------------------------------------------
       结论区只讲事实
       ------------------------------------------------------------------ */

    [Fact]
    public async Task 结论区只有客观数值不含判断性措辞()
    {
        var service = Build(Metric());

        var result = await service.GetAsync("688110");

        var conclusion = result.Value!.Conclusion;
        Assert.Contains("营收", conclusion.Summary);
        Assert.Contains("毛利率", conclusion.Summary);

        // §1.3：不出现「买入」「看好」「估值合理」等判断性措辞，也不出现综合评分
        foreach (var banned in new[] { "买入", "卖出", "持有", "看好", "推荐", "建议", "评分", "合理" })
        {
            Assert.DoesNotContain(banned, conclusion.Summary, StringComparison.Ordinal);
        }

        // 关键数字里也不出现评分类指标
        Assert.DoesNotContain(conclusion.Metrics, metric => metric.Name.Contains("评分", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 无财报数据时结论区如实说明()
    {
        var service = Build();

        var result = await service.GetAsync("688110");

        Assert.Equal("暂无财报数据", result.Value!.Conclusion.Summary);
        Assert.Empty(result.Value.Conclusion.Metrics);
    }

    /* ------------------------------------------------------------------
       七组结构
       ------------------------------------------------------------------ */

    [Fact]
    public async Task 七组齐全且默认只展开前两组()
    {
        var service = Build(Metric());

        var result = await service.GetAsync("688110");

        var groups = result.Value!.Groups;
        Assert.Equal(7, groups.Count);
        Assert.Equal(
            ["business", "cycle", "moat", "quality", "safety", "governance", "valuation"],
            groups.Select(group => group.Key));

        // 简洁优先：默认只展开「生意」与「周期位置」，其余折叠
        Assert.True(groups[0].DefaultExpanded);
        Assert.True(groups[1].DefaultExpanded);
        Assert.All(groups.Skip(2), group => Assert.False(group.DefaultExpanded));

        // 每组都要回答一个具体问题（界面直接展示，用户不必猜）
        Assert.All(groups, group => Assert.False(string.IsNullOrWhiteSpace(group.Question)));
    }

    [Fact]
    public async Task 周期位置组带历史序列供趋势线使用()
    {
        var service = Build(Metric());

        var result = await service.GetAsync("688110");

        var series = Item(result.Value!, "cycle", "grossMarginSeries");
        Assert.Equal(ResearchStatuses.Ok, series.Status);
        Assert.NotNull(series.History);
        Assert.Equal(2, series.History!.Count);
    }

    /* ------------------------------------------------------------------
       替身
       ------------------------------------------------------------------ */

    private sealed class FakeInstruments : IInstrumentStore
    {
        public Task<IReadOnlyList<Instrument>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Instrument>>([]);

        public Task<IReadOnlyDictionary<string, Instrument>> GetByCodesAsync(
            IReadOnlyCollection<string> codes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, Instrument>>(
                new Dictionary<string, Instrument>(StringComparer.Ordinal));

        public Task<Instrument?> FindAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<Instrument?>(new Instrument
            {
                Code = code,
                Name = "东芯股份",
                Market = 1,
                Board = "科创板",
                Industry = "半导体",
                IsSt = false,
                UpdatedOn = Interim
            });

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> UpsertAsync(IReadOnlyList<Instrument> instruments, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> UpdatePinyinAsync(
            IReadOnlyDictionary<string, string> pinyinByCode,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeFundamentals(IReadOnlyList<FundamentalMetric> rows) : IFundamentalStore
    {
        public Task<int> UpsertAsync(IReadOnlyList<FundamentalMetric> metrics, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<FundamentalMetric>> GetHistoryAsync(
            string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FundamentalMetric>>(
                rows.Where(row => row.Code == code).OrderBy(row => row.ReportDate).ToList());

        public Task<IReadOnlyDictionary<string, FundamentalMetric>> GetLatestPerCodeAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, FundamentalMetric>>(
                rows.GroupBy(row => row.Code, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.OrderByDescending(r => r.ReportDate).First(),
                        StringComparer.Ordinal));

        public Task<IReadOnlyDictionary<string, IReadOnlyList<FundamentalMetric>>> GetAnnualByCodeAsync(
            IReadOnlyCollection<string>? codes = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<FundamentalMetric>>>(
                rows.Where(row => ContinuousConditionRules.IsAnnualMetric(row))
                    .GroupBy(row => row.Code, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyList<FundamentalMetric>)group.OrderBy(r => r.ReportDate).ToList(),
                        StringComparer.Ordinal));

        public Task<bool> HasReportDateAsync(DateOnly reportDate, CancellationToken cancellationToken = default) =>
            Task.FromResult(rows.Any(row => row.ReportDate == reportDate));

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(rows.Count);
    }

    private sealed class FakeFinance : IFinanceStore
    {
        public Task<IReadOnlyList<FinancialReport>> GetReportsAsync(
            string code, int limit = 24, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FinancialReport>>([]);

        public Task<IReadOnlyList<EarningsForecast>> GetForecastsAsync(
            string code, int limit = 8, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EarningsForecast>>([]);

        public Task<int> UpsertReportsAsync(IReadOnlyList<FinancialReport> reports, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> UpsertForecastsAsync(IReadOnlyList<EarningsForecast> forecasts, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DateTimeOffset?> GetLastUpdatedAtAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<DateTimeOffset?>(null);

        public Task<IReadOnlyDictionary<string, decimal?>> GetLatestDividendYieldsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, decimal?>>(
                new Dictionary<string, decimal?>(StringComparer.Ordinal));
    }

    private sealed class FakeResearch(
        IReadOnlyList<BusinessComposition> composition,
        IReadOnlyList<UpcomingUnlock> unlocks,
        IReadOnlyList<ShareChange> changes) : IResearchStore
    {
        public Task<int> UpsertProfileAsync(BusinessProfile profile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BusinessProfile?> GetProfileAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<BusinessProfile?>(new BusinessProfile
            {
                Code = code,
                BusinessScope = "集成电路设计",
                BusinessReview = "报告期内…",
                UpdatedAt = DateTimeOffset.Now
            });

        public Task<int> UpsertCompositionsAsync(
            IReadOnlyList<BusinessComposition> items, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BusinessComposition>> GetCompositionsAsync(
            string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(composition);

        public Task<int> UpsertShareChangesAsync(
            IReadOnlyList<ShareChange> rows, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ShareChange>> GetShareChangesAsync(
            string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(changes);

        public Task<int> ReplaceUpcomingUnlocksAsync(
            string code, IReadOnlyList<UpcomingUnlock> rows, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<UpcomingUnlock>> GetUpcomingUnlocksAsync(
            string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(unlocks);

        public Task<int> UpsertAnnouncementsAsync(
            IReadOnlyList<Announcement> rows, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Announcement>> GetAnnouncementsAsync(
            string code, string? columnName = null, int limit = 200, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Announcement>>([]);

        public Task<IReadOnlyDictionary<string, int>> GetAnnouncementTypesAsync(
            string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>(StringComparer.Ordinal));

        public Task<int> UpsertReportsAsync(
            IReadOnlyList<ResearchReport> rows, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ResearchReport>> GetReportsAsync(
            string code, int limit = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResearchReport>>([]);

        public Task<DateTimeOffset?> GetLastUpdatedAtAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<DateTimeOffset?>(null);
    }

    private sealed class FakeEquity : IEquityStore
    {
        public Task<IReadOnlyList<TopHolder>> GetTopHoldersAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TopHolder>>([]);

        public Task<IReadOnlyList<HolderCount>> GetHolderCountsAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<HolderCount>>([]);

        public Task<PledgeStat?> GetPledgeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<PledgeStat?>(null);

        public Task<int> UpsertTopHoldersAsync(IReadOnlyList<TopHolder> holders, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> UpsertHolderCountsAsync(IReadOnlyList<HolderCount> counts, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> UpsertPledgeAsync(PledgeStat pledge, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DateTimeOffset?> GetLastUpdatedAtAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<DateTimeOffset?>(null);
    }

    private sealed class FakeQuotes : IQuoteSnapshotStore
    {
        public Task<IReadOnlyList<QuoteSnapshot>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<QuoteSnapshot>>([]);

        public Task<IReadOnlyDictionary<string, QuoteSnapshot>> GetByCodesAsync(
            IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, QuoteSnapshot>>(
                new Dictionary<string, QuoteSnapshot>(StringComparer.Ordinal));

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<DateTimeOffset?> GetLastUpdatedAtAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DateTimeOffset?>(null);

        public Task<int> ReplaceAllAsync(IReadOnlyList<QuoteSnapshot> snapshots, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
