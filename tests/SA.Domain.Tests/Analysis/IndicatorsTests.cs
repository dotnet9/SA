using SA.Domain.Analysis;

namespace SA.Domain.Tests.Analysis;

/// <summary>
/// 指标算法。用手算/独立算法可复现的样本逐点比对——指标算错会一路传导到趋势结论与评分，
/// 而且不会自己暴露出来，因此这里刻意不放宽断言（详细设计 §13.1）。
/// </summary>
public class IndicatorsTests
{
    /* ------------------------------------------------------------------
       均线
       ------------------------------------------------------------------ */

    [Fact]
    public void 简单均线前若干个位置为null()
    {
        var closes = new decimal[] { 10, 20, 30, 40, 50 };

        var ma3 = Indicators.Sma(closes, 3);

        Assert.Null(ma3[0]);
        Assert.Null(ma3[1]);

        // (10+20+30)/3 = 20
        Assert.Equal(20m, ma3[2]);
        Assert.Equal(30m, ma3[3]);
        Assert.Equal(40m, ma3[4]);
    }

    [Fact]
    public void 样本不足时均线全为null而不是用可用样本凑()
    {
        var ma5 = Indicators.Sma([1m, 2m, 3m], 5);

        Assert.All(ma5, value => Assert.Null(value));
    }

    [Fact]
    public void 指数均线首值等于首个样本()
    {
        var closes = new decimal[] { 100, 110, 120 };

        var ema = Indicators.Ema(closes, 3);

        // k = 2/(3+1) = 0.5
        Assert.Equal(100m, ema[0]);
        Assert.Equal(105m, ema[1]);
        Assert.Equal(112.5m, ema[2]);
    }

    /* ------------------------------------------------------------------
       MACD
       ------------------------------------------------------------------ */

    [Fact]
    public void MACD三线满足定义式()
    {
        // 用一段确定性的锯齿序列，确保 EMA 递推被真实走到
        var closes = Enumerable.Range(0, 60)
            .Select(i => 100m + (i % 10) - (i % 3) * 0.5m)
            .ToArray();

        var macd = Indicators.Macd(closes);
        var ema12 = Indicators.Ema(closes, 12);
        var ema26 = Indicators.Ema(closes, 26);

        Assert.Equal(closes.Length, macd.Dif.Length);

        // DIF 必须等于 EMA12 − EMA26
        for (var i = 0; i < closes.Length; i++)
        {
            Assert.Equal(ema12[i] - ema26[i], macd.Dif[i]);
        }

        // MACD 柱必须等于 (DIF − DEA) × 2
        for (var i = 0; i < closes.Length; i++)
        {
            Assert.Equal((macd.Dif[i] - macd.Dea[i]) * 2, macd.Macd[i]);
        }
    }

    [Fact]
    public void 价格单调上涨时DIF为正()
    {
        var closes = Enumerable.Range(1, 40).Select(i => (decimal)i).ToArray();

        var macd = Indicators.Macd(closes);

        // 单调上涨：快线始终高于慢线
        Assert.True(macd.Dif[^1] > 0);
        Assert.True(macd.Macd[^1] > 0);
    }

    /* ------------------------------------------------------------------
       KDJ
       ------------------------------------------------------------------ */

    [Fact]
    public void KDJ前八位为null且初值递推正确()
    {
        // 振幅恒为 1 的平稳序列：RSV 可手算
        var lows = Enumerable.Repeat(9m, 12).ToArray();
        var highs = Enumerable.Repeat(11m, 12).ToArray();
        var closes = Enumerable.Repeat(10m, 12).ToArray();

        var kdj = Indicators.Kdj(highs, lows, closes);

        for (var i = 0; i < 8; i++)
        {
            Assert.Null(kdj.K[i]);
        }

        // close 居中 → RSV = (10-9)/(11-9)*100 = 50，K、D 稳定在 50，J = 50
        Assert.Equal(50m, kdj.K[8]!.Value);
        Assert.Equal(50m, kdj.D[8]!.Value);
        Assert.Equal(50m, kdj.J[8]!.Value);
    }

    [Fact]
    public void KDJ在连续涨停时K与D向上且J最快()
    {
        var lows = Enumerable.Range(1, 15).Select(i => (decimal)i).ToArray();
        var highs = lows.Select(v => v + 1).ToArray();
        var closes = highs.ToArray();

        var kdj = Indicators.Kdj(highs, lows, closes);

        // 收盘等于最高 → RSV = 100，K、D 向 100 收敛，J = 3K − 2D 跑在最前
        Assert.True(kdj.K[^1] > kdj.K[8]);
        Assert.True(kdj.D[^1] > kdj.D[8]!);
        Assert.True(kdj.J[^1] > kdj.K[^1]);
    }

    [Fact]
    public void KDJ振幅为零时取中值而不除零()
    {
        // 长期停牌：9 日内最高等于最低
        var flat = Enumerable.Repeat(10m, 12).ToArray();

        var kdj = Indicators.Kdj(flat, flat, flat);

        Assert.Equal(50m, kdj.K[8]!.Value);
        Assert.Equal(50m, kdj.J[8]!.Value);
    }

    /* ------------------------------------------------------------------
       RSI
       ------------------------------------------------------------------ */

    [Fact]
    public void RSI在连续上涨时为100()
    {
        var closes = Enumerable.Range(1, 20).Select(i => (decimal)i).ToArray();

        var rsi = Indicators.Rsi(closes, 6);

        Assert.Null(rsi[5]);
        Assert.Equal(100m, rsi[6]!.Value);
        Assert.Equal(100m, rsi[^1]!.Value);
    }

    [Fact]
    public void RSI在连续下跌时为0()
    {
        var closes = Enumerable.Range(1, 20).Select(i => 100m - i).ToArray();

        var rsi = Indicators.Rsi(closes, 6);

        Assert.Equal(0m, rsi[6]!.Value);
    }

    [Fact]
    public void RSI在涨多跌少时大于50跌多涨少时小于50()
    {
        // 交替涨跌在 Wilder 平滑下会在 50 附近摆动，因此这里用「净方向明确」的两组样本
        var up = new List<decimal> { 100m };
        for (var i = 0; i < 20; i++)
        {
            up.Add(i % 3 == 2 ? up[^1] - 1 : up[^1] + 2);
        }

        var down = new List<decimal> { 100m };
        for (var i = 0; i < 20; i++)
        {
            down.Add(i % 3 == 2 ? down[^1] + 1 : down[^1] - 2);
        }

        Assert.True(Indicators.Rsi(up, 6)[^1] > 50m);
        Assert.True(Indicators.Rsi(down, 6)[^1] < 50m);
    }

    /* ------------------------------------------------------------------
       布林带
       ------------------------------------------------------------------ */

    [Fact]
    public void 布林带中轨等于均线且上下轨对称()
    {
        var closes = Enumerable.Range(1, 30).Select(i => (decimal)i).ToArray();

        var boll = Indicators.Boll(closes, 20);
        var ma20 = Indicators.Sma(closes, 20);

        Assert.Equal(ma20[19], boll.Middle[19]);
        Assert.NotNull(boll.Upper[19]);
        Assert.NotNull(boll.Lower[19]);

        // 上下轨关于中轨对称
        var middle = boll.Middle[19]!.Value;
        Assert.Equal(middle - boll.Lower[19]!.Value, boll.Upper[19]!.Value - middle);

        // 不足窗口处为 null
        Assert.Null(boll.Upper[18]);
    }

    /* ------------------------------------------------------------------
       分位与相对强弱
       ------------------------------------------------------------------ */

    [Fact]
    public void 分位在样本不足时返回null()
    {
        var samples = Enumerable.Range(1, 30).Select(i => (decimal)i).ToArray();

        Assert.Null(Indicators.Quantile(samples, 15m, minSamples: 60));
    }

    [Fact]
    public void 分位等于小于等于当前价的占比()
    {
        var samples = Enumerable.Range(1, 100).Select(i => (decimal)i).ToArray();

        // 50 及以下共 50 个 → 50%
        Assert.Equal(50m, Indicators.Quantile(samples, 50m));
        Assert.Equal(100m, Indicators.Quantile(samples, 200m));
        Assert.Equal(1m, Indicators.Quantile(samples, 1m));
    }

    [Fact]
    public void 相对强弱为个股收益减基准收益()
    {
        // 个股 +50%，基准 +20% → 相对强弱 +30 个百分点
        decimal[] stock = [10m, 15m];
        decimal[] benchmark = [100m, 120m];

        Assert.Equal(30m, Indicators.RelativeStrength(stock, benchmark, 0));
    }

    [Fact]
    public void 相对强弱在样本不足时返回null()
    {
        Assert.Null(Indicators.RelativeStrength([10m], [100m, 120m], 0));
        Assert.Null(Indicators.RelativeStrength([0m, 10m], [100m, 120m], 0));
    }

    [Fact]
    public void 相对强弱曲线按尾部等长区间对齐()
    {
        // 基准比个股多 3 个样本：只比较最后 3 个点（尾部对齐到最新交易日）
        decimal[] stock = [10m, 11m, 12m];
        decimal[] benchmark = [1m, 2m, 3m, 100m, 110m, 120m];

        var line = Indicators.RelativeStrengthLine(stock, benchmark);

        Assert.Equal(3, line.Length);

        // 个股 10→12 = +20%，基准 100→120 = +20% → 相对强弱 0
        Assert.Equal(0m, line[^1]);
        Assert.Equal(0m, line[0]);
    }

    [Fact]
    public void 累计收益以首点为基准()
    {
        decimal[] closes = [100m, 110m, 90m];

        var returns = Indicators.CumulativeReturn(closes);

        Assert.Equal(0m, returns[0]);
        Assert.Equal(10m, returns[1]);
        Assert.Equal(-10m, returns[2]);
    }
}
