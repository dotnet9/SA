using SA.Application.Abstractions;
using SA.Application.Authorization;
using SA.Application.Market;
using SA.Application.Screener;
using SA.Contracts.Screener;
using SA.Domain.Entities.Finance;
using SA.Domain.Entities.Market;
using SA.Domain.Entities.Screener;
using SA.Domain.Entities.System;

namespace SA.Application.Tests.Screener;

/// <summary>
/// 选股器的基本面字段、金融业开关与连续性条件（实施计划 §5.2–§5.5）。
/// </summary>
/// <remarks>
/// 全部离线：行情快照、基本面与年报序列由替身提供，
/// 因此断言的是<b>筛选语义</b>而不是上游数据。
/// </remarks>
public class ScreenerFundamentalTests
{
    private static readonly DateOnly QuoteAsOf = new(2026, 9, 18);

    private static ScreenerService Build(
        IReadOnlyList<QuoteSnapshot> quotes,
        IReadOnlyList<Instrument> instruments,
        IReadOnlyList<FundamentalMetric> fundamentals,
        IReadOnlyList<FinancialReport>? reports = null)
    {
        var cache = new MarketSnapshotCache();
        cache.Replace(quotes, instruments.ToDictionary(i => i.Code, StringComparer.Ordinal));

        var latest = fundamentals
            .GroupBy(metric => metric.Code, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(metric => metric.ReportDate).First(),
                StringComparer.Ordinal);

        var annual = fundamentals
            .Where(metric => FundamentalReportTypes.IsAnnual(metric.ReportType))
            .GroupBy(metric => metric.Code, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<FundamentalMetric>)group.OrderBy(metric => metric.ReportDate).ToList(),
                StringComparer.Ordinal);

        var dividends = (reports ?? [])
            .GroupBy(report => report.Code, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(report => report.ReportDate).First().DividendYield,
                StringComparer.Ordinal);

        cache.ReplaceFundamentals(latest, annual, dividends);

        return new ScreenerService(
            cache,
            new FakeQuoteStore(quotes),
            new FakeInstrumentStore(instruments),
            new FakeFundamentalStore(fundamentals),
            new FakeFinanceStore(reports ?? []),
            new DataScopeFilter(new AnonymousUser(), new FakeScopeService()),
            new FakeRunStore(),
            new FakeExportLogStore());
    }

    /* ------------------------------------------------------------------
       字段映射与缺失语义
       ------------------------------------------------------------------ */

    [Fact]
    public async Task 基本面字段出现在结果行上()
    {
        var service = Build(
            [Quote("688110", price: 45m)],
            [Instrument("688110", "东芯股份", "科创板")],
            [Metric("688110", new DateOnly(2026, 6, 30), "中报", orgType: "通用", roe: 17.36m, grossMargin: 67.66m)]);

        var result = await service.RunAsync(Request());

        var row = Assert.Single(result.Value!.Rows);
        Assert.True(row.HasFundamental);
        Assert.Equal(17.36m, row.Roe);
        Assert.Equal(67.66m, row.GrossMargin);
        Assert.Equal("2026-06-30", row.FundamentalAsOf);
        Assert.False(row.IsFinancial);
    }

    [Fact]
    public async Task 无财报数据的标的字段为null且标记为无财报()
    {
        var service = Build(
            [Quote("688110", price: 45m)],
            [Instrument("688110", "东芯股份", "科创板")],
            []);

        var result = await service.RunAsync(Request());

        var row = Assert.Single(result.Value!.Rows);
        Assert.False(row.HasFundamental);
        Assert.Null(row.Roe);
        Assert.Null(row.GrossMargin);
        Assert.Equal(0, row.FundamentalYears);
    }

    [Fact]
    public async Task 金融业被标记且其空字段保持null()
    {
        var service = Build(
            [Quote("000001", price: 11.7m)],
            [Instrument("000001", "平安银行", "深市主板")],
            [Metric("000001", new DateOnly(2026, 6, 30), "中报", orgType: "银行", debtRatio: 90.9m)]);

        var result = await service.RunAsync(Request());

        var row = Assert.Single(result.Value!.Rows);
        Assert.True(row.IsFinancial);
        Assert.Null(row.GrossMargin);
        Assert.Equal(90.9m, row.DebtRatio);
    }

    /* ------------------------------------------------------------------
       金融业显式开关
       ------------------------------------------------------------------ */

    [Fact]
    public async Task 按毛利率筛选会排除金融业且开关可显式包含()
    {
        var service = Build(
            [Quote("688110", price: 45m), Quote("000001", price: 11.7m)],
            [Instrument("688110", "东芯股份", "科创板"), Instrument("000001", "平安银行", "深市主板")],
            [
                Metric("688110", new DateOnly(2026, 6, 30), "中报", orgType: "通用", grossMargin: 67.66m),
                Metric("000001", new DateOnly(2026, 6, 30), "中报", orgType: "银行", debtRatio: 90.9m)
            ]);

        // 按毛利率筛：银行的毛利率为空 → 不命中（这是「不适用」不是「低毛利率」）
        var withoutFinancials = await service.RunAsync(Request(
            ranges: [new ScreenerRange(ScreenerFields.GrossMargin, 30m, null)],
            flags: [new ScreenerFlag(ScreenerFields.IncludeFinancials, false)]));

        var only = Assert.Single(withoutFinancials.Value!.Rows);
        Assert.Equal("688110", only.Code);
        Assert.Contains(withoutFinancials.Value.Applied, note => note.Contains("排除金融业"));

        // 显式包含：银行仍然进不来（它没有毛利率），但回显说明了原因
        var withFinancials = await service.RunAsync(Request(
            ranges: [new ScreenerRange(ScreenerFields.GrossMargin, 30m, null)],
            flags: [new ScreenerFlag(ScreenerFields.IncludeFinancials, true)]));

        Assert.Contains(withFinancials.Value!.Applied, note => note.Contains("包含金融业"));
    }

    [Fact]
    public async Task 不勾选金融业开关时默认包含金融业()
    {
        // 开关缺省必须包含：隐式排除会让用户以为「全市场都筛过了」
        var service = Build(
            [Quote("688110", price: 45m), Quote("000001", price: 11.7m)],
            [Instrument("688110", "东芯股份", "科创板"), Instrument("000001", "平安银行", "深市主板")],
            [
                Metric("688110", new DateOnly(2026, 6, 30), "中报", orgType: "通用", roe: 17m),
                Metric("000001", new DateOnly(2026, 6, 30), "中报", orgType: "银行", roe: 8m)
            ]);

        var result = await service.RunAsync(Request());

        Assert.Equal(2, result.Value!.Total);
    }

    /* ------------------------------------------------------------------
       连续性条件
       ------------------------------------------------------------------ */

    [Fact]
    public async Task 连续五年ROE达标才命中()
    {
        var service = Build(
            [Quote("688110", price: 45m), Quote("300750", price: 300m)],
            [Instrument("688110", "东芯股份", "科创板"), Instrument("300750", "宁德时代", "创业板")],
            [
                // 东芯：连续 5 年年报 ROE ≥ 15%
                Annual("688110", 2021, 18m), Annual("688110", 2022, 17m), Annual("688110", 2023, 16m),
                Annual("688110", 2024, 20m), Annual("688110", 2025, 19m),
                // 宁德：2023 年掉到 8%
                Annual("300750", 2021, 22m), Annual("300750", 2022, 20m), Annual("300750", 2023, 8m),
                Annual("300750", 2024, 21m), Annual("300750", 2025, 23m)
            ]);

        var result = await service.RunAsync(Request(
            continuous: [new ScreenerContinuous(ScreenerFields.Roe, 15m, null, 5)]));

        var row = Assert.Single(result.Value!.Rows);
        Assert.Equal("688110", row.Code);
        Assert.Equal(5, row.FundamentalYears);
    }

    [Fact]
    public async Task 历史数据不足时回显里给出只数()
    {
        var service = Build(
            [Quote("688110", price: 45m)],
            [Instrument("688110", "东芯股份", "科创板")],
            [Annual("688110", 2024, 20m), Annual("688110", 2025, 20m)]);

        var result = await service.RunAsync(Request(
            continuous: [new ScreenerContinuous(ScreenerFields.Roe, 15m, null, 5)]));

        Assert.Empty(result.Value!.Rows);
        // 不静默排除：回显里写清有多少只是「历史数据不足」
        Assert.Contains(result.Value.Applied, note => note.Contains("历史数据不足"));
    }

    [Fact]
    public async Task 跳年不被判为连续()
    {
        var service = Build(
            [Quote("688110", price: 45m)],
            [Instrument("688110", "东芯股份", "科创板")],
            [Annual("688110", 2019, 20m), Annual("688110", 2025, 20m)]);

        var result = await service.RunAsync(Request(
            continuous: [new ScreenerContinuous(ScreenerFields.Roe, 15m, null, 2)]));

        Assert.Empty(result.Value!.Rows);
    }

    [Fact]
    public async Task 连续条件的字段不支持时回显忽略()
    {
        var service = Build(
            [Quote("688110", price: 45m)],
            [Instrument("688110", "东芯股份", "科创板")],
            [Annual("688110", 2025, 20m)]);

        var result = await service.RunAsync(Request(
            continuous: [new ScreenerContinuous(ScreenerFields.Pct, 0m, null, 3)]));

        Assert.Contains(result.Value!.Applied, note => note.Contains("忽略不支持连续性的字段"));
    }

    /* ------------------------------------------------------------------
       预设
       ------------------------------------------------------------------ */

    [Fact]
    public async Task 高ROE低负债预设要求连续性且排除金融业()
    {
        var interim = new DateOnly(2026, 6, 30);

        var service = Build(
            [Quote("688110", price: 45m), Quote("000001", price: 11.7m), Quote("300750", price: 300m)],
            [
                Instrument("688110", "东芯股份", "科创板"),
                Instrument("000001", "平安银行", "深市主板"),
                Instrument("300750", "宁德时代", "创业板")
            ],
            [
                // 最新一期：负债率是预设区间条件用的字段
                Metric("688110", interim, "中报", orgType: "通用", roe: 17.36m, debtRatio: 9.76m),
                Metric("000001", interim, "中报", orgType: "银行", roe: 8.5m, debtRatio: 90.91m),
                Metric("300750", interim, "中报", orgType: "通用", roe: 12m, debtRatio: 63.65m),

                // 年报序列：连续性条件用
                Annual("688110", 2021, 18m), Annual("688110", 2022, 17m), Annual("688110", 2023, 16m),
                Annual("688110", 2024, 20m), Annual("688110", 2025, 19m),
                // 银行 ROE 连续达标，但负债率 90.9% 超过 50%，且是金融业
                Annual("000001", 2021, 18m), Annual("000001", 2022, 17m), Annual("000001", 2023, 16m),
                Annual("000001", 2024, 20m), Annual("000001", 2025, 19m),
                // 宁德连续 5 年但 ROE 只有 5%，不达 15% 门槛
                Annual("300750", 2021, 5m), Annual("300750", 2022, 5m), Annual("300750", 2023, 5m),
                Annual("300750", 2024, 5m), Annual("300750", 2025, 5m)
            ]);

        var result = await service.RunAsync(Request(preset: "high-roe-low-debt"));

        var row = Assert.Single(result.Value!.Rows);
        Assert.Equal("688110", row.Code);

        // 回显里能看到预设的三类条件都生效了
        Assert.Contains(result.Value.Applied, note => note.Contains("预设"));
        Assert.Contains(result.Value.Applied, note => note.Contains("排除金融业"));
        Assert.Contains(result.Value.Applied, note => note.Contains("连续 5 年"));
    }

    /* ------------------------------------------------------------------
       元数据
       ------------------------------------------------------------------ */

    [Fact]
    public async Task 元数据下发分组口径提示与连续性选项()
    {
        var service = Build(
            [Quote("688110", price: 45m)],
            [Instrument("688110", "东芯股份", "科创板")],
            [Metric("688110", new DateOnly(2026, 6, 30), "中报", orgType: "通用", roe: 17m)]);

        var meta = await service.GetMetaAsync(exportQuota: 3, exportRowLimit: 5000, strategyQuota: 10);

        Assert.Equal(3, meta.Value!.ExportQuota);
        Assert.Contains(meta.Value.FieldGroups, group => group.Key == "quality" && group.DefaultExpanded);
        // 其余组默认折叠：一屏 30 个输入框会让用户无从下手
        Assert.All(
            meta.Value.FieldGroups.Where(group => group.Key != "quality"),
            group => Assert.False(group.DefaultExpanded));
        Assert.NotEmpty(meta.Value.CaliberNotes);
        Assert.Contains(ScreenerFields.Roe, meta.Value.ContinuousFields);
        Assert.Equal([3, 5, 8], meta.Value.ContinuousYears);
        Assert.Equal("2026-06-30", meta.Value.FundamentalAsOf);
    }

    /* ------------------------------------------------------------------
       辅助
       ------------------------------------------------------------------ */

    private static ScreenerRequest Request(
        IReadOnlyList<ScreenerRange>? ranges = null,
        IReadOnlyList<ScreenerFlag>? flags = null,
        IReadOnlyList<ScreenerContinuous>? continuous = null,
        string? preset = null) =>
        new(ranges, null, flags, ScreenerFields.Amount, true, 1, 50, preset, continuous);

    private static QuoteSnapshot Quote(string code, decimal price) =>
        new()
        {
            Code = code,
            Price = price,
            Change = 0m,
            Pct = 0m,
            Volume = 1000m,
            Amount = 1_000_000_000m,
            Turnover = 1m,
            VolRatio = 1m,
            Open = price,
            High = price,
            Low = price,
            PrevClose = price,
            MarketCap = 10_000_000_000m,
            FloatCap = 8_000_000_000m,
            Pe = 10m,
            PeTtm = 10m,
            Pb = 1m,
            AsOf = QuoteAsOf,
            UpdatedAt = DateTimeOffset.Now
        };

    private static Instrument Instrument(string code, string name, string board) =>
        new()
        {
            Code = code,
            Name = name,
            Market = 0,
            Board = board,
            IsSt = false,
            UpdatedOn = QuoteAsOf
        };

    private static FundamentalMetric Metric(
        string code,
        DateOnly reportDate,
        string reportType,
        string? orgType,
        decimal? roe = null,
        decimal? grossMargin = null,
        decimal? debtRatio = null) =>
        new()
        {
            Code = code,
            ReportDate = reportDate,
            ReportType = reportType,
            OrgType = orgType,
            RoeWeighted = roe,
            GrossMargin = grossMargin,
            DebtRatio = debtRatio,
            UpdatedAt = DateTimeOffset.Now
        };

    /// <summary>造一条年报（用于连续性条件）。</summary>
    private static FundamentalMetric Annual(string code, int year, decimal? roe) =>
        new()
        {
            Code = code,
            ReportDate = new DateOnly(year, 12, 31),
            ReportType = FundamentalReportTypes.Annual,
            OrgType = FundamentalOrgTypes.General,
            RoeWeighted = roe,
            UpdatedAt = DateTimeOffset.Now
        };

    private sealed class AnonymousUser : IUserContext
    {
        public string? UserId => null;
    }

    private sealed class FakeScopeService : IDataScopeService
    {
        public Task<IReadOnlySet<string>?> AllowedCodesAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<string>?>(null);

        public Task<bool> IsAllowedAsync(string userId, string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeQuoteStore(IReadOnlyList<QuoteSnapshot> rows) : IQuoteSnapshotStore
    {
        public Task<IReadOnlyList<QuoteSnapshot>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(rows);

        public Task<IReadOnlyDictionary<string, QuoteSnapshot>> GetByCodesAsync(
            IReadOnlyCollection<string> codes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, QuoteSnapshot>>(
                rows.Where(row => codes.Contains(row.Code)).ToDictionary(row => row.Code, StringComparer.Ordinal));

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(rows.Count);

        public Task<DateTimeOffset?> GetLastUpdatedAtAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DateTimeOffset?>(DateTimeOffset.Now);

        public Task<int> ReplaceAllAsync(IReadOnlyList<QuoteSnapshot> snapshots, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeInstrumentStore(IReadOnlyList<Instrument> rows) : IInstrumentStore
    {
        public Task<IReadOnlyList<Instrument>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(rows);

        public Task<IReadOnlyDictionary<string, Instrument>> GetByCodesAsync(
            IReadOnlyCollection<string> codes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, Instrument>>(
                rows.Where(row => codes.Contains(row.Code)).ToDictionary(row => row.Code, StringComparer.Ordinal));

        public Task<Instrument?> FindAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(rows.FirstOrDefault(row => row.Code == code));

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(rows.Count);

        public Task<int> UpsertAsync(IReadOnlyList<Instrument> instruments, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> UpdatePinyinAsync(
            IReadOnlyDictionary<string, string> pinyinByCode,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeFundamentalStore(IReadOnlyList<FundamentalMetric> rows) : IFundamentalStore
    {
        public Task<int> UpsertAsync(IReadOnlyList<FundamentalMetric> metrics, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<FundamentalMetric>> GetHistoryAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FundamentalMetric>>(
                rows.Where(row => row.Code == code).OrderBy(row => row.ReportDate).ToList());

        public Task<IReadOnlyDictionary<string, FundamentalMetric>> GetLatestPerCodeAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, FundamentalMetric>>(
                rows.GroupBy(row => row.Code, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => group.OrderByDescending(row => row.ReportDate).First(),
                        StringComparer.Ordinal));

        public Task<IReadOnlyDictionary<string, IReadOnlyList<FundamentalMetric>>> GetAnnualByCodeAsync(
            IReadOnlyCollection<string>? codes = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<FundamentalMetric>>>(
                rows.Where(row => FundamentalReportTypes.IsAnnual(row.ReportType))
                    .Where(row => codes is null || codes.Contains(row.Code))
                    .GroupBy(row => row.Code, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyList<FundamentalMetric>)group.OrderBy(row => row.ReportDate).ToList(),
                        StringComparer.Ordinal));

        public Task<bool> HasReportDateAsync(DateOnly reportDate, CancellationToken cancellationToken = default) =>
            Task.FromResult(rows.Any(row => row.ReportDate == reportDate));

        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(rows.Count);
    }

    private sealed class FakeFinanceStore(IReadOnlyList<FinancialReport> reports) : IFinanceStore
    {
        public Task<IReadOnlyList<FinancialReport>> GetReportsAsync(
            string code, int limit = 24, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FinancialReport>>(reports.Where(row => row.Code == code).ToList());

        public Task<IReadOnlyList<EarningsForecast>> GetForecastsAsync(
            string code, int limit = 8, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<EarningsForecast>>([]);

        public Task<int> UpsertReportsAsync(IReadOnlyList<FinancialReport> rows, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> UpsertForecastsAsync(IReadOnlyList<EarningsForecast> rows, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DateTimeOffset?> GetLastUpdatedAtAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<DateTimeOffset?>(DateTimeOffset.Now);

        public Task<IReadOnlyDictionary<string, decimal?>> GetLatestDividendYieldsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, decimal?>>(
                reports.GroupBy(row => row.Code, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => group.OrderByDescending(row => row.ReportDate).First().DividendYield,
                        StringComparer.Ordinal));
    }

    private sealed class FakeRunStore : IScreenerRunStore
    {
        public Task<long> AddAsync(ScreenerRun run, CancellationToken cancellationToken = default) =>
            Task.FromResult(1L);

        public Task<ScreenerRun?> FindAsync(string userId, long id, CancellationToken cancellationToken = default) =>
            Task.FromResult<ScreenerRun?>(null);

        public Task<ScreenerRun?> FindByNameAsync(string userId, string name, CancellationToken cancellationToken = default) =>
            Task.FromResult<ScreenerRun?>(null);

        public Task<IReadOnlyList<ScreenerRun>> GetRecentAsync(
            string userId, bool strategiesOnly, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScreenerRun>>([]);

        public Task<int> CountStrategiesAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<bool> RenameAsync(string userId, long id, string? name, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<int> DeleteAsync(string userId, long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<int> TrimAsync(string userId, int keep, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed class FakeExportLogStore : IExportLogStore
    {
        public Task AddAsync(ExportLog log, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ExportLog>> GetRecentAsync(
            string userId, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ExportLog>>([]);

        public Task<int> CountTodayAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}
