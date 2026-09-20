namespace SA.Domain.Analysis;

/// <summary>
/// 技术指标算法。纯函数、无依赖，便于与服务端计算结果逐点比对（详细设计 §5.1–5.3）。
/// </summary>
/// <remarks>
/// <para>
/// 全部约定：样本不足的位置返回 <c>null</c>，<b>不做插值、不用可用样本凑</b>——
/// 新股与次新股的指标必须显示为「—」，而不是一个看起来合理的近似值（实施计划 §10）。
/// </para>
/// <para>
/// 返回值与输入等长，前端与 DTO 直接按下标对齐即可。
/// </para>
/// </remarks>
public static class Indicators
{
    /// <summary>
    /// 简单移动平均。<c>MA(n)[i] = mean(close[i-n+1 .. i])</c>，不足 n 个样本处为 null。
    /// </summary>
    public static decimal?[] Sma(IReadOnlyList<decimal> values, int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);

        var result = new decimal?[values.Count];
        if (values.Count < period)
        {
            return result;
        }

        decimal sum = 0;
        for (var i = 0; i < values.Count; i++)
        {
            sum += values[i];
            if (i >= period)
            {
                sum -= values[i - period];
            }

            if (i >= period - 1)
            {
                result[i] = sum / period;
            }
        }

        return result;
    }

    /// <summary>
    /// 指数移动平均。<c>EMA[0] = values[0]</c>，<c>EMA[i] = values[i]*k + EMA[i-1]*(1-k)</c>，
    /// <c>k = 2/(period+1)</c>。
    /// </summary>
    public static decimal[] Ema(IReadOnlyList<decimal> values, int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);

        var result = new decimal[values.Count];
        if (values.Count == 0)
        {
            return result;
        }

        var k = 2m / (period + 1);
        result[0] = values[0];
        for (var i = 1; i < values.Count; i++)
        {
            result[i] = values[i] * k + result[i - 1] * (1 - k);
        }

        return result;
    }

    /// <summary>
    /// MACD(12,26,9)：<c>DIF = EMA12 − EMA26</c>，<c>DEA = EMA(DIF, 9)</c>，<c>MACD = (DIF − DEA) × 2</c>。
    /// </summary>
    /// <param name="closes">收盘价序列。</param>
    /// <param name="fast">快线周期，默认 12。</param>
    /// <param name="slow">慢线周期，默认 26。</param>
    /// <param name="signal">信号线周期，默认 9。</param>
    public static MacdSeries Macd(
        IReadOnlyList<decimal> closes,
        int fast = 12,
        int slow = 26,
        int signal = 9)
    {
        var fastEma = Ema(closes, fast);
        var slowEma = Ema(closes, slow);

        var dif = new decimal[closes.Count];
        for (var i = 0; i < closes.Count; i++)
        {
            dif[i] = fastEma[i] - slowEma[i];
        }

        var dea = Ema(dif, signal);
        var macd = new decimal[closes.Count];
        for (var i = 0; i < closes.Count; i++)
        {
            macd[i] = (dif[i] - dea[i]) * 2;
        }

        return new MacdSeries(dif, dea, macd);
    }

    /// <summary>
    /// KDJ(9,3,3)。
    /// </summary>
    /// <remarks>
    /// <c>RSV = (close − LLV(low,9)) / (HHV(high,9) − LLV(low,9)) × 100</c>，
    /// 分母为 0（连续 9 日振幅为 0，常见于长期停牌）时取 50；
    /// <c>K = 2/3·K(前) + 1/3·RSV</c>，K、D 初值均为 50，<c>J = 3K − 2D</c>。
    /// 前 8 个样本因缺少 9 日窗口而不输出。
    /// </remarks>
    public static KdjSeries Kdj(
        IReadOnlyList<decimal> highs,
        IReadOnlyList<decimal> lows,
        IReadOnlyList<decimal> closes,
        int period = 9)
    {
        var count = Math.Min(closes.Count, Math.Min(highs.Count, lows.Count));
        var k = new decimal?[count];
        var d = new decimal?[count];
        var j = new decimal?[count];

        decimal previousK = 50;
        decimal previousD = 50;

        for (var i = 0; i < count; i++)
        {
            if (i < period - 1)
            {
                continue;
            }

            var highest = decimal.MinValue;
            var lowest = decimal.MaxValue;
            for (var w = i - period + 1; w <= i; w++)
            {
                if (highs[w] > highest)
                {
                    highest = highs[w];
                }

                if (lows[w] < lowest)
                {
                    lowest = lows[w];
                }
            }

            var range = highest - lowest;
            var rsv = range == 0 ? 50m : (closes[i] - lowest) / range * 100m;

            previousK = (2m / 3m) * previousK + (1m / 3m) * rsv;
            previousD = (2m / 3m) * previousD + (1m / 3m) * previousK;

            k[i] = previousK;
            d[i] = previousD;
            j[i] = 3 * previousK - 2 * previousD;
        }

        return new KdjSeries(k, d, j);
    }

    /// <summary>
    /// RSI（Wilder 平滑）：<c>RSI = 100 − 100 / (1 + 平均涨幅 / 平均跌幅)</c>。
    /// </summary>
    /// <remarks>
    /// 初值用前 <paramref name="period"/> 个涨跌幅的简单平均，之后按
    /// <c>avg = (avg × (period−1) + 当期) / period</c> 递推（业界通行的 Wilder 口径）。
    /// 平均跌幅为 0 时返回 100（连续上涨）。
    /// </remarks>
    public static decimal?[] Rsi(IReadOnlyList<decimal> closes, int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1);

        var result = new decimal?[closes.Count];
        if (closes.Count <= period)
        {
            return result;
        }

        decimal gainSum = 0;
        decimal lossSum = 0;
        for (var i = 1; i <= period; i++)
        {
            var change = closes[i] - closes[i - 1];
            if (change >= 0)
            {
                gainSum += change;
            }
            else
            {
                lossSum -= change;
            }
        }

        var averageGain = gainSum / period;
        var averageLoss = lossSum / period;
        result[period] = ToRsi(averageGain, averageLoss);

        for (var i = period + 1; i < closes.Count; i++)
        {
            var change = closes[i] - closes[i - 1];
            var gain = change > 0 ? change : 0m;
            var loss = change < 0 ? -change : 0m;

            averageGain = (averageGain * (period - 1) + gain) / period;
            averageLoss = (averageLoss * (period - 1) + loss) / period;
            result[i] = ToRsi(averageGain, averageLoss);
        }

        return result;
    }

    /// <summary>
    /// 布林带 BOLL(20,2)：中轨 = MA(20)，上下轨 = 中轨 ± 2 × 总体标准差。
    /// </summary>
    public static BollSeries Boll(IReadOnlyList<decimal> closes, int period = 20, decimal multiplier = 2m)
    {
        var middle = Sma(closes, period);
        var upper = new decimal?[closes.Count];
        var lower = new decimal?[closes.Count];

        for (var i = period - 1; i < closes.Count; i++)
        {
            if (middle[i] is not { } mid)
            {
                continue;
            }

            decimal sumSquares = 0;
            for (var w = i - period + 1; w <= i; w++)
            {
                var diff = closes[w] - mid;
                sumSquares += diff * diff;
            }

            var deviation = (decimal)Math.Sqrt((double)(sumSquares / period));
            upper[i] = mid + multiplier * deviation;
            lower[i] = mid - multiplier * deviation;
        }

        return new BollSeries(middle, upper, lower);
    }

    /// <summary>
    /// 价格分位：当前价在样本分布中「小于等于」它的占比（百分数，0–100）。
    /// </summary>
    /// <remarks>
    /// 样本不足（少于 <paramref name="minSamples"/>）时返回 null，界面显示「—」，
    /// 不用更短的样本硬算一个分位（实施计划 §10「新股 / 次新」）。
    /// </remarks>
    public static decimal? Quantile(IReadOnlyList<decimal> samples, decimal current, int minSamples = 60)
    {
        if (samples.Count < minSamples)
        {
            return null;
        }

        var belowOrEqual = samples.Count(v => v <= current);
        return Math.Round((decimal)belowOrEqual / samples.Count * 100m, 1);
    }

    /// <summary>
    /// 相对强弱：个股累计收益 − 基准累计收益（同区间），返回百分数。
    /// </summary>
    /// <remarks>
    /// 与详细设计 §5.3 一致：两条序列都按各自首个值归一，再相减。
    /// 任一条长度不足 2 或首值为 0 时返回 null。
    /// </remarks>
    public static decimal? RelativeStrength(
        IReadOnlyList<decimal> stockCloses,
        IReadOnlyList<decimal> benchmarkCloses,
        int offset)
    {
        var index = offset < 0 ? 0 : offset;
        if (stockCloses.Count <= index + 1 || benchmarkCloses.Count <= index + 1)
        {
            return null;
        }

        var stockBase = stockCloses[index];
        var benchmarkBase = benchmarkCloses[index];
        if (stockBase == 0 || benchmarkBase == 0)
        {
            return null;
        }

        var stockReturn = stockCloses[^1] / stockBase - 1;
        var benchmarkReturn = benchmarkCloses[^1] / benchmarkBase - 1;
        return Math.Round((stockReturn - benchmarkReturn) * 100m, 2);
    }

    /// <summary>
    /// 累计收益率序列（百分数，首点为 0）：用于相对强弱曲线的两条腿。
    /// </summary>
    /// <remarks>
    /// 以首个值为基准归一：<c>ret[i] = closes[i] / closes[0] − 1</c>。
    /// 首值为 0 或样本为空时返回全 0（调用方应以 null 表示「不可用」，而不是画一条直线）。
    /// </remarks>
    public static decimal[] CumulativeReturn(IReadOnlyList<decimal> closes)
    {
        var result = new decimal[closes.Count];
        if (closes.Count == 0 || closes[0] == 0)
        {
            return result;
        }

        var baseValue = closes[0];
        for (var i = 0; i < closes.Count; i++)
        {
            result[i] = Math.Round((closes[i] / baseValue - 1) * 100m, 2);
        }

        return result;
    }

    /// <summary>
    /// 相对强弱曲线：两条等长累计收益序列逐点相减。
    /// </summary>
    public static decimal[] RelativeStrengthLine(
        IReadOnlyList<decimal> stockCloses,
        IReadOnlyList<decimal> benchmarkCloses)
    {
        var count = Math.Min(stockCloses.Count, benchmarkCloses.Count);
        if (count == 0)
        {
            return [];
        }

        // 取尾部等长区间对齐：两条序列的最后一个交易日都是最新交易日
        var stock = stockCloses.Skip(stockCloses.Count - count).ToArray();
        var benchmark = benchmarkCloses.Skip(benchmarkCloses.Count - count).ToArray();

        var stockReturn = CumulativeReturn(stock);
        var benchmarkReturn = CumulativeReturn(benchmark);

        var result = new decimal[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = Math.Round(stockReturn[i] - benchmarkReturn[i], 2);
        }

        return result;
    }

    private static decimal ToRsi(decimal averageGain, decimal averageLoss)
    {
        if (averageLoss == 0)
        {
            return averageGain == 0 ? 50m : 100m;
        }

        var rs = averageGain / averageLoss;
        return Math.Round(100m - 100m / (1 + rs), 2);
    }
}

/// <summary>MACD 三线。</summary>
/// <param name="Dif">快慢线之差。</param>
/// <param name="Dea">DIF 的信号线。</param>
/// <param name="Macd">柱状值（DIF − DEA 的 2 倍）。</param>
public readonly record struct MacdSeries(decimal[] Dif, decimal[] Dea, decimal[] Macd);

/// <summary>KDJ 三线（不足窗口处为 null）。</summary>
/// <param name="K">K 值。</param>
/// <param name="D">D 值。</param>
/// <param name="J">J 值。</param>
public readonly record struct KdjSeries(decimal?[] K, decimal?[] D, decimal?[] J);

/// <summary>布林带三轨（不足窗口处为 null）。</summary>
/// <param name="Middle">中轨。</param>
/// <param name="Upper">上轨。</param>
/// <param name="Lower">下轨。</param>
public readonly record struct BollSeries(decimal?[] Middle, decimal?[] Upper, decimal?[] Lower);
