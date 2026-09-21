using SA.Application.Abstractions;
using SA.Application.Common;
using SA.Application.Events;
using SA.Contracts.Analysis;
using SA.Contracts.Common;
using SA.Domain.Common;
using SA.Domain.History;

namespace SA.Application.Analysis;

/// <summary>
/// 因果链与传导带宽。
/// </summary>
/// <remarks>
/// <para>
/// <b>规则引擎，不是模型推断</b>（实施计划 §2 决策 13）：链条的每一环都必须给出可复算的证据
/// （事件日期、之后 1/5 日涨跌、同期行业与基准的涨跌、相关系数与贝塔），
/// 以及一个由样本量决定的置信度。用户能自己核对，也能看出哪一环只是相关性。
/// </para>
/// <para>
/// <b>不做的事</b>：不声称因果、不预测未来。链条表达的是「事件发生之后，个股与行业分别怎么走」，
/// 以及「个股对行业的跟随程度有多强」，这两件事都能用数据回答；
/// 「谁导致了谁」需要产业数据（供货占比、成本占比），本轮没有，因此不写。
/// </para>
/// </remarks>
public sealed class CausalChainService(
    IDailyHistoryStore daily,
    IIndexStore indices,
    IInstrumentStore instruments,
    IQuoteSnapshotStore quotes,
    ISectorStore sectors,
    EventTimelineService events,
    IOnDemandQueue onDemand)
{
    /// <summary>相关性/贝塔的回看天数。</summary>
    private const int LookbackDays = 60;

    /// <summary>带宽所需的最少样本。</summary>
    private const int MinSamples = 40;

    /// <summary>链条里最多展开几个事件。</summary>
    private const int MaxEvents = 3;

    /// <summary>
    /// 组装因果链与传导带宽。
    /// </summary>
    public async Task<ServiceResult<CausalChainDto>> GetAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var instrument = await instruments.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (instrument is null)
        {
            return ServiceResult<CausalChainDto>.Fail(ErrorCode.NotFound, $"未找到证券代码 {code}");
        }

        var bars = await daily.GetLatestAsync(code, LookbackDays + 120, cancellationToken).ConfigureAwait(false);
        if (bars.Count < 10)
        {
            // 把标的入队，由按需采集在下一拍补齐日线（含其所属行业的指数日线）；
            // 前端按 1003 的既有约定自动重试，用户不必手动刷新
            onDemand.TryEnqueue(code);
            return ServiceResult<CausalChainDto>.Fail(ErrorCode.DataNotReady, "日线数据正在采集，请稍后重试");
        }

        var benchmark = await LoadSeriesAsync("000300", cancellationToken).ConfigureAwait(false);

        // 所属行业的板块码与行业指数日线
        string? industryCode = null;
        string? industryName = instrument.Industry;
        var sectorBars = new List<DailyBar>();

        if (industryName is not null)
        {
            var sectorsAll = await sectors.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var sector = sectorsAll.FirstOrDefault(row => string.Equals(row.Name, industryName, StringComparison.Ordinal));
            if (sector is not null)
            {
                industryCode = sector.Code;
                sectorBars = (await daily.GetLatestAsync(sector.Code, LookbackDays + 30, cancellationToken)
                    .ConfigureAwait(false)).ToList();
            }
        }

        var bandwidth = BuildBandwidth(
            bars.Select(bar => bar.Close).ToList(),
            sectorBars.Select(bar => bar.Close).ToList(),
            benchmark.Select(bar => bar.Close).ToList(),
            industryCode,
            industryName);

        // 行业指数日线缺失时把标的入队：按需采集会顺带补齐其所属行业（90.BKxxxx）的日线，
        // 否则带宽会一直是「样本不足」而用户不知道该等什么
        if (industryCode is not null && sectorBars.Count < MinSamples)
        {
            onDemand.TryEnqueue(code);
        }

        // 基准（沪深300）缺失同理：没有基准就算不出相对强弱与带宽的对照
        if (benchmark.Count < MinSamples)
        {
            onDemand.TryEnqueue("000300");
        }

        var links = await BuildLinksAsync(code, instrument.Name, bars, sectorBars, benchmark, cancellationToken)
            .ConfigureAwait(false);

        var quote = (await quotes.GetByCodesAsync([code], cancellationToken).ConfigureAwait(false))
            .TryGetValue(code, out var snapshot)
                ? snapshot
                : null;

        return ServiceResult<CausalChainDto>.Success(new CausalChainDto(
            Code: code,
            Name: instrument.Name,
            Industry: industryName,
            AsOf: quote is null ? null : SaTime.Format(quote.AsOf),
            Links: links,
            Bandwidth: bandwidth,
            Insights: BuildInsights(instrument.Name, links, bandwidth),
            Notes:
            [
                "口径：本页是规则引擎推算（非模型推断）。每一环都给出可复算的证据与置信度，可自行核对。",
                "相关性不等于因果：链条表达「事件发生之后个股与行业分别怎么走」，不声称谁导致了谁。",
                "传导带宽 = |个股与行业指数日收益的相关系数|，越大说明个股越跟随行业；贝塔表示行业每涨 1% 时个股平均的涨跌幅。",
                $"带宽需要至少 {MinSamples} 个重叠交易日（回看 {LookbackDays} 日），样本不足时显示为空而不用更短的样本硬算。",
                "产业链上下游数据（供货占比 / 成本占比）与「谁导致谁」需要专门的产业数据源，本轮没有，因此不提供。",
                "数据来源：本地日线（个股 / 行业指数 90.BKxxxx / 沪深300）+ 已落地的事件数据。"
            ]));
    }

    /// <summary>
    /// 组装链条：最近的高影响事件 → 事件后个股与行业的反应 → 当前状态。
    /// </summary>
    private async Task<List<CausalLinkDto>> BuildLinksAsync(
        string code,
        string name,
        IReadOnlyList<DailyBar> bars,
        IReadOnlyList<DailyBar> sectorBars,
        IReadOnlyList<DailyBar> benchmark,
        CancellationToken cancellationToken)
    {
        var links = new List<CausalLinkDto>();
        var order = 1;

        // 事件环节：取最近几条有明确日期的事件（事件由事件模块派生，这里复用同一套口径）
        var timeline = await events.GetAsync(code, canAnnotate: false, cancellationToken).ConfigureAwait(false);
        var recent = timeline.Ok && timeline.Value is not null
            ? timeline.Value.Events.Take(MaxEvents).ToList()
            : [];

        foreach (var item in recent)
        {
            var date = DateOnly.TryParse(item.Date, out var parsed) ? parsed : (DateOnly?)null;
            if (date is null)
            {
                continue;
            }

            var stockReturn = ForwardReturn(bars, date.Value, 5);
            var industryReturn = ForwardReturn(sectorBars, date.Value, 5);
            var benchmarkReturn = ForwardReturn(benchmark, date.Value, 5);

            var evidence = stockReturn is null
                ? "事件日之后尚无 5 个交易日的数据，暂无法度量影响"
                : $"事件后 5 日：个股 {stockReturn:+0.00;-0.00}%"
                  + (industryReturn is null ? string.Empty : $"，行业 {industryReturn:+0.00;-0.00}%")
                  + (benchmarkReturn is null ? string.Empty : $"，基准 {benchmarkReturn:+0.00;-0.00}%")
                  + (industryReturn is not null && benchmarkReturn is not null
                      ? $"（超额 {stockReturn - benchmarkReturn:+0.00;-0.00} 个百分点）"
                      : string.Empty);

            links.Add(new CausalLinkDto(
                Order: order++,
                Stage: "事件",
                Title: $"{item.Date} {item.Title}",
                Evidence: evidence,
                Tone: item.Tone,
                // 置信度取决于「有没有可度量的后续行情」：只有 1 天数据时只能说影响有限
                Confidence: stockReturn is null ? 35 : ForwardReturn(bars, date.Value, 5) is not null ? 75 : 55,
                ConfidenceNote: stockReturn is null
                    ? "缺少事件后的行情样本"
                    : "事件后有 5 个交易日样本，且事件日期明确"));
        }

        // 行业环节
        if (sectorBars.Count >= 20)
        {
            var industryReturn20 = ForwardReturn(sectorBars, sectorBars[^21].Date, 20);
            var stockReturn20 = ForwardReturn(bars, bars.Count > 21 ? bars[^21].Date : bars[0].Date, 20);

            links.Add(new CausalLinkDto(
                Order: order++,
                Stage: "行业反应",
                Title: $"所属行业近 20 日走势",
                Evidence: industryReturn20 is null
                    ? "行业指数样本不足"
                    : $"行业指数近 20 日 {industryReturn20:+0.00;-0.00}%"
                      + (stockReturn20 is null ? string.Empty : $"，个股同期 {stockReturn20:+0.00;-0.00}%"),
                Tone: industryReturn20 >= 0 ? "up" : "down",
                Confidence: 80,
                ConfidenceNote: "基于行业指数日线（90.BKxxxx）的真实序列"));
        }

        // 当前状态环节
        var last = bars[^1];
        var change = bars.Count >= 2 && bars[^2].Close > 0
            ? (last.Close / bars[^2].Close - 1) * 100m
            : 0m;

        links.Add(new CausalLinkDto(
            Order: order++,
            Stage: "当前状态",
            Title: $"{name} 最新收 {last.Close:F2}",
            Evidence: $"最近交易日 {SaTime.Format(last.Date)}，较前一交易日 {change:+0.00;-0.00}%"
                      + $"（当日高 {last.High:F2} / 低 {last.Low:F2}）",
            Tone: change >= 0 ? "up" : "down",
            Confidence: 95,
            ConfidenceNote: "直接来自最新日线，无需推断"));

        return links;
    }

    /// <summary>
    /// 传导带宽：与行业指数的相关性、贝塔，以及与基准的相关性。
    /// </summary>
    private static TransmissionBandwidthDto BuildBandwidth(
        IReadOnlyList<decimal> stockCloses,
        IReadOnlyList<decimal> industryCloses,
        IReadOnlyList<decimal> benchmarkCloses,
        string? industryCode,
        string? industryName)
    {
        var industryCorrelation = ProsperityService.ComputeCorrelation(stockCloses, industryCloses, LookbackDays);
        var benchmarkCorrelation = ProsperityService.ComputeCorrelation(stockCloses, benchmarkCloses, LookbackDays);
        var beta = ComputeBeta(stockCloses, industryCloses, LookbackDays);
        var samples = Math.Min(stockCloses.Count, industryCloses.Count);

        // 带宽以「与行业的相关性」为准；行业数据缺失时退回与基准的相关性并注明
        var bandwidth = industryCorrelation ?? benchmarkCorrelation;
        var note = industryCorrelation is not null
            ? $"与行业指数的重叠样本 {samples} 天（回看 {LookbackDays} 日）"
            : benchmarkCorrelation is not null
                ? "行业指数样本不足，带宽已退回「与基准的相关性」；建议稍后重试（行业日线正在回补）"
                : $"样本不足（需要至少 {MinSamples} 个重叠交易日）";

        return new TransmissionBandwidthDto(
            IndustryCode: industryCode,
            IndustryName: industryName,
            IndustryCorrelation: industryCorrelation,
            BenchmarkCorrelation: benchmarkCorrelation,
            Beta: beta,
            Samples: samples,
            Bandwidth: bandwidth,
            Note: note);
    }

    /// <summary>
    /// 贝塔：个股日收益对行业指数日收益的回归斜率。
    /// </summary>
    internal static decimal? ComputeBeta(
        IReadOnlyList<decimal> stockCloses,
        IReadOnlyList<decimal> industryCloses,
        int window)
    {
        var count = Math.Min(stockCloses.Count, industryCloses.Count);
        if (count < MinSamples)
        {
            return null;
        }

        var take = Math.Min(count, window);
        var stock = stockCloses.Skip(stockCloses.Count - take).ToList();
        var industry = industryCloses.Skip(industryCloses.Count - take).ToList();

        var x = new List<double>(take - 1);
        var y = new List<double>(take - 1);

        for (var i = 1; i < take; i++)
        {
            if (stock[i - 1] <= 0 || industry[i - 1] <= 0)
            {
                continue;
            }

            x.Add((double)(industry[i] / industry[i - 1] - 1));
            y.Add((double)(stock[i] / stock[i - 1] - 1));
        }

        if (x.Count < MinSamples - 1)
        {
            return null;
        }

        var meanX = x.Average();
        var meanY = y.Average();

        double covariance = 0, varianceX = 0;
        for (var i = 0; i < x.Count; i++)
        {
            covariance += (x[i] - meanX) * (y[i] - meanY);
            varianceX += (x[i] - meanX) * (x[i] - meanX);
        }

        // 方差为 0（行业指数完全没有波动）时贝塔没有定义
        return varianceX <= 0 ? null : Math.Round((decimal)(covariance / varianceX), 3);
    }

    /// <summary>
    /// 从某个日期起、经过 <paramref name="days"/> 个交易日的区间收益（百分数）。
    /// </summary>
    /// <remarks>
    /// 以「事件日或之后第一个交易日」为基准价，避免事件发生在非交易日时取不到基准。
    /// 之后不足 <paramref name="days"/> 根 K 线时返回 null，而不是用更短的区间冒充 5 日。
    /// </remarks>
    internal static decimal? ForwardReturn(IReadOnlyList<DailyBar> bars, DateOnly from, int days)
    {
        var startIndex = -1;
        for (var i = 0; i < bars.Count; i++)
        {
            if (bars[i].Date >= from)
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex < 0 || startIndex + days >= bars.Count + 1)
        {
            return null;
        }

        var endIndex = startIndex + days;
        if (endIndex >= bars.Count)
        {
            return null;
        }

        var startClose = bars[startIndex].Close;
        if (startClose <= 0)
        {
            return null;
        }

        return Math.Round((bars[endIndex].Close / startClose - 1) * 100m, 2);
    }

    private static List<string> BuildInsights(
        string name,
        IReadOnlyList<CausalLinkDto> links,
        TransmissionBandwidthDto bandwidth)
    {
        var insights = new List<string>();

        if (bandwidth.Bandwidth is { } value)
        {
            var level = value >= 0.7m ? "强" : value >= 0.4m ? "中等" : "弱";
            insights.Add($"对行业的跟随度{level}（相关性 {value:F2}）");
        }
        else
        {
            insights.Add("传导带宽暂不可用（样本不足或行业日线未回补）");
        }

        if (bandwidth.Beta is { } beta)
        {
            insights.Add(beta >= 1m
                ? $"行业每涨 1%，{name} 平均涨 {beta:F2}%（弹性高于行业）"
                : $"行业每涨 1%，{name} 平均涨 {beta:F2}%（弹性低于行业）");
        }

        var eventLinks = links.Where(link => link.Stage == "事件").ToList();
        if (eventLinks.Count > 0)
        {
            insights.Add($"链上包含 {eventLinks.Count} 条事件环节，其中最高置信度 {eventLinks.Max(link => link.Confidence)}");
        }

        return insights;
    }

    /// <summary>取某标的的日线序列（用于基准）——指数与个股共用同一个日线存储。</summary>
    private async Task<IReadOnlyList<DailyBar>> LoadSeriesAsync(string code, CancellationToken cancellationToken)
    {
        var configured = await indices.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var resolved = configured.FirstOrDefault(index => index.Code == code)?.Code ?? code;
        return await daily.GetLatestAsync(resolved, LookbackDays + 30, cancellationToken).ConfigureAwait(false);
    }
}
