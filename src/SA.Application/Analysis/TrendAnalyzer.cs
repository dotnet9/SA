using SA.Application.Abstractions;
using SA.Application.Common;
using SA.Contracts.Common;
using SA.Contracts.Trend;
using SA.Domain.Analysis;
using SA.Domain.Common;
using SA.Domain.History;

namespace SA.Application.Analysis;

/// <summary>
/// 趋势与价格结构的分析器：把日线 + 指标 + 基准序列组装成一张趋势视图。
/// </summary>
/// <remarks>
/// <para>
/// 指标不在本类计算，而是读 <see cref="IIndicatorStore"/>：指标由采集侧批量算好并落库，
/// 保证「列表看到的 MA20」与「详情页看到的 MA20」是同一份数（实施计划 §5.5）。
/// </para>
/// <para>
/// 所有位置类指标（分位、250 日高低、站上均线幅度）在样本不足时返回 null，
/// 由界面显示「—」，不做任何近似（实施计划 §10：新股与次新不做插值）。
/// </para>
/// </remarks>
public sealed class TrendAnalyzer(
    IDailyHistoryStore daily,
    IIndicatorStore indicators,
    IIndexStore indexStore,
    IInstrumentStore instruments,
    IQuoteSnapshotStore quotes)
{
    /// <summary>K 线上限（详细设计 §9.2：默认展示 120 根，最多 240 根）。</summary>
    public const int MaxCandles = 240;

    /// <summary>近 250 日窗口，用于年内高低与中短期位置。</summary>
    private const int ShortWindow = 250;

    /// <summary>长周期均线窗口（年线）。</summary>
    private const int LongWindow = 250;

    /// <summary>
    /// 组装趋势视图。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="limit">K 线条数（上限 <see cref="MaxCandles"/>）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<ServiceResult<TrendDto>> GetTrendAsync(
        string code,
        int limit = 120,
        CancellationToken cancellationToken = default)
    {
        var instrument = await instruments.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (instrument is null)
        {
            return ServiceResult<TrendDto>.Fail(ErrorCode.NotFound, $"未找到证券代码 {code}");
        }

        var take = Math.Clamp(limit <= 0 ? 120 : limit, 20, MaxCandles);

        // 多取一段：分位与年线需要更长样本，K 线只展示尾部 take 根
        var bars = await daily.GetLatestAsync(code, Math.Max(take, ShortWindow + 60), cancellationToken)
            .ConfigureAwait(false);

        if (bars.Count == 0)
        {
            // 按需采集在后续批次接入；当前语义是「已入队采集中」，前端据此重试
            return ServiceResult<TrendDto>.Fail(ErrorCode.DataNotReady, "日线数据正在采集，请稍后重试");
        }

        var indicatorRows = await indicators.GetLatestAsync(code, Math.Max(take, ShortWindow + 60), cancellationToken)
            .ConfigureAwait(false);

        var benchmark = await LoadBenchmarkAsync(cancellationToken).ConfigureAwait(false);
        var quote = (await quotes.GetByCodesAsync([code], cancellationToken).ConfigureAwait(false))
            .TryGetValue(code, out var snapshot)
                ? snapshot
                : null;

        var candles = bars.TakeLast(take).ToList();
        var aligned = AlignIndicators(indicatorRows, candles);

        // 口径说明：把「本次为什么没有某个数字」直接写进返回体，界面原样展示
        var notes = new List<string>
        {
            "口径：日线为前复权（指标与图形同一口径）；停牌日不产生 K 线。",
            "指标（MA / MACD / KDJ / BOLL / RSI）由服务端批量计算并落库，前端只负责渲染。",
            "价格分位为当前价在样本分布中小于等于它的占比；样本不足时返回空、界面显示「—」，不做插值。"
        };

        if (indicatorRows.Count == 0)
        {
            notes.Add("该标的指标尚未计算完成（已完成日线 " + bars.Count + " 根），均线与 MACD 暂不可用；页面会自动重试。");
        }

        if (benchmark.Count == 0)
        {
            notes.Add("基准指数（沪深300）日线尚未回补，相对强弱暂不可用。");
        }

        var dto = new TrendDto(
            Code: code,
            Name: instrument.Name,
            Period: "daily",
            Adjust: "front",
            AsOf: SaTime.Format(bars[^1].Date),
            Candles: candles.Select(bar => new CandleDto(
                SaTime.Format(bar.Date), bar.Open, bar.High, bar.Low, bar.Close, bar.Volume)).ToList(),
            Ma: aligned.Ma,
            Macd: aligned.Macd,
            Kdj: aligned.Kdj,
            Boll: aligned.Boll,
            RelativeStrength: BuildRelativeStrength(candles, benchmark),
            Levels: BuildLevels(bars, quote?.Price),
            Insights: BuildInsights(bars, aligned, quote?.Price),
            Notes: notes);

        return ServiceResult<TrendDto>.Success(dto);
    }

    /// <summary>只取最新收盘价序列，供总览页缩略图使用。</summary>
    public async Task<IReadOnlyList<decimal>> GetThumbAsync(
        string code,
        int take = 60,
        CancellationToken cancellationToken = default)
    {
        var bars = await daily.GetLatestAsync(code, take, cancellationToken).ConfigureAwait(false);
        return bars.Select(bar => bar.Close).ToList();
    }

    /// <summary>
    /// 基准指数序列（沪深 300）。相对强弱的基准固定为此指数，与详细设计 §5.3 一致。
    /// </summary>
    private async Task<IReadOnlyList<DailyBar>> LoadBenchmarkAsync(CancellationToken cancellationToken)
    {
        var configured = await indexStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var code = configured.FirstOrDefault(index => index.Code == "000300")?.Code ?? "000300";
        return await daily.GetLatestAsync(code, ShortWindow + 60, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 把指标行对齐到 K 线日期。指标是按日期存的，缺行（例如指标任务尚未跑）时补 null，
    /// 保证前端按下标取值不会错位。
    /// </summary>
    private static AlignedIndicators AlignIndicators(
        IReadOnlyList<IndicatorRow> rows,
        IReadOnlyList<DailyBar> candles)
    {
        var byDate = rows.ToDictionary(row => row.Date);

        var ma5 = new List<decimal?>(candles.Count);
        var ma10 = new List<decimal?>(candles.Count);
        var ma20 = new List<decimal?>(candles.Count);
        var ma60 = new List<decimal?>(candles.Count);
        var dif = new List<decimal?>(candles.Count);
        var dea = new List<decimal?>(candles.Count);
        var macd = new List<decimal?>(candles.Count);
        var k = new List<decimal?>(candles.Count);
        var d = new List<decimal?>(candles.Count);
        var j = new List<decimal?>(candles.Count);
        var bollUp = new List<decimal?>(candles.Count);
        var bollMid = new List<decimal?>(candles.Count);
        var bollLow = new List<decimal?>(candles.Count);

        foreach (var candle in candles)
        {
            if (!byDate.TryGetValue(candle.Date, out var row))
            {
                ma5.Add(null);
                ma10.Add(null);
                ma20.Add(null);
                ma60.Add(null);
                dif.Add(null);
                dea.Add(null);
                macd.Add(null);
                k.Add(null);
                d.Add(null);
                j.Add(null);
                bollUp.Add(null);
                bollMid.Add(null);
                bollLow.Add(null);
                continue;
            }

            ma5.Add(row.Ma5);
            ma10.Add(row.Ma10);
            ma20.Add(row.Ma20);
            ma60.Add(row.Ma60);
            dif.Add(row.Dif);
            dea.Add(row.Dea);
            macd.Add(row.Macd);
            k.Add(row.K);
            d.Add(row.D);
            j.Add(row.J);
            bollUp.Add(row.BollUp);
            bollMid.Add(row.BollMid);
            bollLow.Add(row.BollLow);
        }

        return new AlignedIndicators(
            new MaSeriesDto(ma5, ma10, ma20, ma60),
            new MacdSeriesDto(dif, dea, macd),
            new KdjSeriesDto(k, d, j),
            new BollSeriesDto(bollUp, bollMid, bollLow));
    }

    /// <summary>
    /// 相对强弱。输入必须是<b>与 K 线同一段</b>的个股序列：曲线与 K 线按日期一一对齐，
    /// 前端才能把两条序列画在同一个横轴上。
    /// </summary>
    private static RelativeStrengthDto BuildRelativeStrength(
        IReadOnlyList<DailyBar> bars,
        IReadOnlyList<DailyBar> benchmark)
    {
        if (benchmark.Count < 2)
        {
            return new RelativeStrengthDto("沪深300", "000300", null, []);
        }

        var stockCloses = bars.Select(bar => bar.Close).ToList();
        var benchmarkCloses = benchmark.Select(bar => bar.Close).ToList();

        var offset = Math.Max(0, stockCloses.Count - benchmarkCloses.Count);
        var value = Indicators.RelativeStrength(stockCloses, benchmarkCloses, offset);
        var line = Indicators.RelativeStrengthLine(stockCloses, benchmarkCloses);

        return new RelativeStrengthDto("沪深300", "000300", value, line);
    }

    private static TrendLevelsDto BuildLevels(IReadOnlyList<DailyBar> bars, decimal? current)
    {
        var window = bars.TakeLast(ShortWindow).ToList();
        var closes = window.Select(bar => bar.Close).ToList();

        var high250 = window.Count > 0 ? window.Max(bar => bar.High) : (decimal?)null;
        var low250 = window.Count > 0 ? window.Min(bar => bar.Low) : (decimal?)null;

        var price = current ?? closes[^1];

        var ma20 = Indicators.Sma(closes, 20)[^1];
        var ma250 = Indicators.Sma(closes, LongWindow)[^1];

        return new TrendLevelsDto(
            High250: high250,
            Low250: low250,
            AboveMa20Pct: ma20 is null or 0 ? null : Math.Round((price / ma20.Value - 1) * 100m, 2),
            AboveMa250Pct: ma250 is null or 0 ? null : Math.Round((price / ma250.Value - 1) * 100m, 2),
            // 3 年分位：当前入库样本不足 3 年时按「样本不足」处理，而不是用更短的样本硬算
            Quantile3y: Indicators.Quantile(bars.Select(bar => bar.Close).ToList(), price, minSamples: 500),
            Quantile250: Indicators.Quantile(closes, price, minSamples: 120),
            Samples: bars.Count);
    }

    /// <summary>
    /// 趋势结论。全部由可复算的规则给出，不含主观判断。
    /// </summary>
    private static List<TrendInsightDto> BuildInsights(
        IReadOnlyList<DailyBar> bars,
        AlignedIndicators aligned,
        decimal? current)
    {
        var insights = new List<TrendInsightDto>();
        var price = current ?? bars[^1].Close;

        var ma5 = aligned.Ma.Ma5[^1];
        var ma10 = aligned.Ma.Ma10[^1];
        var ma20 = aligned.Ma.Ma20[^1];
        var ma60 = aligned.Ma.Ma60[^1];

        // 均线排列
        if (ma5 is not null && ma10 is not null && ma20 is not null && ma60 is not null)
        {
            if (ma5 > ma10 && ma10 > ma20 && ma20 > ma60)
            {
                insights.Add(new TrendInsightDto("均线", "多头排列", "up"));
            }
            else if (ma5 < ma10 && ma10 < ma20 && ma20 < ma60)
            {
                insights.Add(new TrendInsightDto("均线", "空头排列", "down"));
            }
            else
            {
                insights.Add(new TrendInsightDto("均线", "均线纠缠", "neutral"));
            }
        }
        else
        {
            insights.Add(new TrendInsightDto("均线", "样本不足", "neutral"));
        }

        // 位置：相对 20 日线
        if (ma20 is not null && ma20.Value != 0)
        {
            var above = (price / ma20.Value - 1) * 100m;
            insights.Add(new TrendInsightDto(
                "位置",
                Math.Abs(above) < 1m ? "贴近 20 日线" : above > 0 ? $"站上 20 日线 {above:F1}%" : $"跌破 20 日线 {Math.Abs(above):F1}%",
                above >= 0 ? "up" : "down"));
        }

        // 量能：最近 5 日均量对比前 20 日均量
        if (bars.Count >= 25)
        {
            var recent = bars.TakeLast(5).Average(bar => bar.Volume);
            var baseline = bars.TakeLast(25).Take(20).Average(bar => bar.Volume);
            if (baseline > 0)
            {
                var ratio = recent / baseline;
                var tone = ratio >= 1.2m ? "up" : ratio <= 0.8m ? "down" : "neutral";
                var text = ratio >= 1.2m ? $"放量 {ratio:F2} 倍" : ratio <= 0.8m ? $"缩量至 {ratio:F2} 倍" : "量能平稳";
                insights.Add(new TrendInsightDto("量能", text, tone));
            }
        }

        // MACD 柱方向
        var macd = aligned.Macd.Macd[^1];
        if (macd is not null)
        {
            insights.Add(new TrendInsightDto("动能", macd >= 0 ? "MACD 红柱" : "MACD 绿柱", macd >= 0 ? "up" : "down"));
        }

        return insights;
    }

    /// <summary>与 K 线对齐后的指标序列。</summary>
    private sealed record AlignedIndicators(
        MaSeriesDto Ma,
        MacdSeriesDto Macd,
        KdjSeriesDto Kdj,
        BollSeriesDto Boll);
}
