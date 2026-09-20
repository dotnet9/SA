using SA.Application.Analysis;
using SA.Application.Screener;
using SA.Contracts.Screener;
using SA.Domain.History;

namespace SA.Application.Tests.Analysis;

/// <summary>
/// 分布统计与规则引擎推算的数学部分。
/// </summary>
/// <remarks>
/// 这些函数的特点是「算错不会报错」：分位数插值写错只会让数字略偏，
/// 相关系数取了未取绝对值只会让带宽方向反了。因此逐个用可手算的样本钉住。
/// </remarks>
public class DistributionAndCausalMathTests
{
    /* ------------------------------------------------------------------
       分布统计
       ------------------------------------------------------------------ */

    [Fact]
    public void 分位数用线性插值()
    {
        // 1..5：中位数 3；P25 位置 = 0.25*4 = 1 → 2；P75 位置 = 3 → 4
        var values = new List<decimal> { 1m, 2m, 3m, 4m, 5m };

        Assert.Equal(3m, DistributionStats.Quantile(values, 0.5m));
        Assert.Equal(2m, DistributionStats.Quantile(values, 0.25m));
        Assert.Equal(4m, DistributionStats.Quantile(values, 0.75m));
    }

    [Fact]
    public void 分位数在两点之间插值()
    {
        // 1..4：P50 位置 = 0.5*3 = 1.5 → (2+3)/2 = 2.5
        var values = new List<decimal> { 1m, 2m, 3m, 4m };

        Assert.Equal(2.5m, DistributionStats.Quantile(values, 0.5m));
    }

    [Fact]
    public void 单样本的分位数就是它自己()
    {
        Assert.Equal(7m, DistributionStats.Quantile([7m], 0.25m));
    }

    [Fact]
    public void 直方图最大值落在最后一箱()
    {
        // 0..10 分成 5 箱、每箱宽 2：若无「最大值归最后一箱」的处理，10 会掉到区间外
        var values = new List<decimal> { 0m, 2m, 4m, 6m, 8m, 10m };

        var bins = DistributionStats.BuildBins(values, 0m, 10m, 5);

        Assert.Equal(5, bins.Count);
        Assert.Equal(2, bins[^1].Count);
        Assert.Equal(10m, bins[^1].To);
        Assert.Equal(values.Count, bins.Sum(bin => bin.Count));
    }

    [Fact]
    public void 所有值相同时退化为单箱而不是除零()
    {
        var bins = DistributionStats.BuildBins([5m, 5m, 5m], 5m, 5m, 12);

        Assert.Single(bins);
        Assert.Equal(3, bins[0].Count);
    }

    [Fact]
    public void 缺失值不参与分布统计()
    {
        var rows = new List<ScreenerRowDto>
        {
            Row("A", pe: 10m),
            Row("B", pe: null),   // 亏损股：PE 缺失
            Row("C", pe: 20m)
        };

        var stats = DistributionStats.Compute(rows, ScreenerFields.PeTtm);

        Assert.NotNull(stats);
        Assert.Equal(2, stats!.Count);
        Assert.Equal(10m, stats.Min);
        Assert.Equal(20m, stats.Max);
    }

    [Fact]
    public void 全部缺失时返回空统计而不是零值()
    {
        var rows = new List<ScreenerRowDto> { Row("A", pe: null), Row("B", pe: null) };

        var stats = DistributionStats.Compute(rows, ScreenerFields.PeTtm);

        Assert.NotNull(stats);
        Assert.Equal(0, stats!.Count);
        Assert.Null(stats.Median);
        Assert.Empty(stats.Bins);
    }

    [Fact]
    public void 不支持的字段返回null()
    {
        Assert.Null(DistributionStats.Compute([Row("A", pe: 1m)], "board"));
    }

    /* ------------------------------------------------------------------
       传导带宽
       ------------------------------------------------------------------ */

    [Fact]
    public void 完全同向的两条序列相关性为一()
    {
        // 两条相同的锯齿序列：相关系数应为 1
        var a = Enumerable.Range(0, 60).Select(i => 100m + (i % 7) * 2m).ToList();
        var b = new List<decimal>(a);

        var correlation = ProsperityService.ComputeCorrelation(a, b, 60);

        Assert.NotNull(correlation);
        Assert.Equal(1m, correlation!.Value);
    }

    [Fact]
    public void 相关性取绝对值因此反向也计入带宽()
    {
        var a = Enumerable.Range(0, 60).Select(i => 100m + (i % 7) * 2m).ToList();
        // 反向序列：每一对相邻变化的符号相反
        var b = new List<decimal> { a[0] };
        for (var i = 1; i < a.Count; i++)
        {
            b.Add(b[^1] + (a[i - 1] - a[i]));
        }

        var correlation = ProsperityService.ComputeCorrelation(a, b, 60);

        Assert.NotNull(correlation);
        // 带宽衡量「跟随程度」，方向由相对强弱表达，因此取绝对值
        Assert.True(correlation!.Value > 0.9m);
    }

    [Fact]
    public void 样本不足时带宽返回null()
    {
        var shortSeries = Enumerable.Range(0, 20).Select(i => 100m + i).ToList();

        Assert.Null(ProsperityService.ComputeCorrelation(shortSeries, shortSeries, 60));
    }

    [Fact]
    public void 序列完全不动时无法计算相关性()
    {
        var flat = Enumerable.Repeat(100m, 60).ToList();

        // 方差为 0，相关系数没有定义
        Assert.Null(ProsperityService.ComputeCorrelation(flat, flat, 60));
    }

    [Fact]
    public void 相对强弱为行业收益减基准收益()
    {
        var sector = Enumerable.Range(0, 30).Select(i => 100m + i).ToList();      // +约 29%
        var benchmark = Enumerable.Range(0, 30).Select(i => 100m + i * 0.5m).ToList(); // +约 14.5%

        var bars = ToBars(sector);
        var bench = ToBars(benchmark);

        var rs = ProsperityService.ComputeRelativeStrength(bars, bench, 20);

        Assert.NotNull(rs);
        Assert.True(rs!.Value > 0m);
    }

    [Fact]
    public void 相对强弱样本不足时返回null()
    {
        Assert.Null(ProsperityService.ComputeRelativeStrength(ToBars([1m, 2m]), ToBars([1m, 2m]), 20));
    }

    /* ------------------------------------------------------------------
       贝塔与前向收益
       ------------------------------------------------------------------ */

    [Fact]
    public void 个股与行业同步波动时贝塔接近一()
    {
        // 行业与个股的日收益完全一致 → 贝塔 = 1
        var industry = Enumerable.Range(0, 60).Select(i => 1000m + (i % 5) * 3m).ToList();
        var stock = industry.Select(value => value / 10m).ToList();

        var beta = CausalChainService.ComputeBeta(stock, industry, 60);

        Assert.NotNull(beta);
        Assert.Equal(1m, beta!.Value);
    }

    [Fact]
    public void 个股波动是行业两倍时贝塔接近二()
    {
        // 个股收益 = 行业收益 × 2
        var industry = new List<decimal> { 1000m };
        var stock = new List<decimal> { 100m };
        for (var i = 1; i < 60; i++)
        {
            var change = (i % 4 - 1.5m) / 100m;   // 有正有负的固定序列
            industry.Add(industry[^1] * (1 + change));
            stock.Add(stock[^1] * (1 + change * 2));
        }

        var beta = CausalChainService.ComputeBeta(stock, industry, 60);

        Assert.NotNull(beta);
        Assert.InRange(beta!.Value, 1.9m, 2.1m);
    }

    [Fact]
    public void 样本不足时贝塔返回null()
    {
        var shortSeries = Enumerable.Range(0, 10).Select(i => 100m + i).ToList();

        Assert.Null(CausalChainService.ComputeBeta(shortSeries, shortSeries, 60));
    }

    [Fact]
    public void 前向收益从事件日起算固定交易日数()
    {
        var bars = Enumerable.Range(0, 30)
            .Select(i => Bar(new DateOnly(2026, 1, 1).AddDays(i), 100m + i))
            .ToList();

        // 从第 5 天（100+5）起算 5 个交易日 → 第 10 天（110）→ +5/105 ≈ 4.76%
        var result = CausalChainService.ForwardReturn(bars, new DateOnly(2026, 1, 6), 5);

        Assert.NotNull(result);
        Assert.Equal(Math.Round((110m / 105m - 1) * 100m, 2), result!.Value);
    }

    [Fact]
    public void 后续样本不足时不冒充固定区间()
    {
        var bars = Enumerable.Range(0, 10)
            .Select(i => Bar(new DateOnly(2026, 1, 1).AddDays(i), 100m + i))
            .ToList();

        // 距末尾只剩 3 根却要求 5 日：必须返回 null，而不是用 3 日的结果冒充
        Assert.Null(CausalChainService.ForwardReturn(bars, new DateOnly(2026, 1, 7), 5));
    }

    [Fact]
    public void 事件日早于全部样本时从第一根起算()
    {
        var bars = Enumerable.Range(0, 30)
            .Select(i => Bar(new DateOnly(2026, 1, 1).AddDays(i), 100m + i))
            .ToList();

        var result = CausalChainService.ForwardReturn(bars, new DateOnly(2025, 12, 1), 5);

        Assert.NotNull(result);
        Assert.Equal(Math.Round((105m / 100m - 1) * 100m, 2), result!.Value);
    }

    /* ------------------------------------------------------------------
       构造工具
       ------------------------------------------------------------------ */

    private static List<DailyBar> ToBars(IReadOnlyList<decimal> closes) =>
        closes.Select((close, index) => Bar(new DateOnly(2026, 1, 1).AddDays(index), close)).ToList();

    private static DailyBar Bar(DateOnly date, decimal close) =>
        new(
            Date: date,
            Open: close,
            High: close,
            Low: close,
            Close: close,
            Volume: 1_000_000m,
            Amount: close * 1_000_000m,
            Turnover: 1m,
            VolRatio: 1m,
            AdjFactor: 1m);

    private static ScreenerRowDto Row(string code, decimal? pe) =>
        new(
            Code: code,
            Name: code,
            Board: "沪市主板",
            Industry: "电池",
            Price: 10m,
            Pct: 1m,
            Turnover: 1m,
            VolRatio: 1m,
            Amount: 1m,
            PeTtm: pe,
            Pb: 1m,
            Cap: 100m,
            FloatCap: 100m,
            IsSt: false);
}
