using SA.Application.Abstractions;
using SA.Application.Common;
using SA.Contracts.Common;
using SA.Contracts.Risk;
using SA.Domain.Analysis;
using SA.Domain.Common;
using SA.Domain.Entities.Equity;
using SA.Domain.Entities.Finance;

namespace SA.Application.Risk;

/// <summary>
/// 风险与舆情监控读模型。
/// </summary>
/// <remarks>
/// <para>
/// <b>只做可复算的风险</b>：每条风险都由明确阈值与本地数据算出，并在返回体里同时给出
/// 实际值与阈值（<see cref="RiskItemDto.Metric"/> / <see cref="RiskItemDto.Threshold"/>），
/// 让人能自己核对为什么被标为风险，而不是接受一个黑箱结论。
/// </para>
/// <para>
/// 覆盖的维度：退市风险、财务恶化、股权质押、价格波动、最大回撤、流动性、估值位置。
/// </para>
/// <para>
/// <b>关于诉讼、监管问询与舆情（本处曾写成「本轮没有可用的公开源」，不准确）</b>：
/// 公告源<b>是可用的</b>（东财 <c>np-anotice-stock</c>，实测东芯股份 892 条，
/// 已由 <c>EastMoneyAnnouncementSource</c> 接入运行时采集）。不提供这几个维度的真实原因是
/// <b>本轮不做公告正文解析</b>（实施计划 §1.3：不做关键词抽取、不做情绪判断、不做事件自动归类），
/// 而「诉讼 / 监管问询」要从公告正文里抽取才能得到。
/// 因此这里不是「没有源」，而是「有源但本轮不做解析」——不编造舆情结论这一点不变。
/// </para>
/// </remarks>
public sealed class RiskService(
    IDailyHistoryStore daily,
    IFinanceStore finance,
    IEquityStore equity,
    IInstrumentStore instruments,
    IQuoteSnapshotStore quotes)
{
    /// <summary>风险计分的等级阈值。</summary>
    private const int HighGradeScore = 55;

    /// <summary>中等等级的阈值。</summary>
    private const int MediumGradeScore = 30;

    /// <summary>年化波动率阈值（百分数）。</summary>
    private const decimal VolatilityMedium = 45m;

    /// <summary>年化波动率高风险阈值。</summary>
    private const decimal VolatilityHigh = 70m;

    /// <summary>最大回撤阈值（百分数）。</summary>
    private const decimal DrawdownMedium = 30m;

    /// <summary>最大回撤高风险阈值。</summary>
    private const decimal DrawdownHigh = 50m;

    /// <summary>质押比例阈值（百分数）。</summary>
    private const decimal PledgeMedium = 30m;

    /// <summary>质押比例高风险阈值。</summary>
    private const decimal PledgeHigh = 50m;

    /// <summary>日均成交额下限（亿元）。</summary>
    private const decimal LiquidityFloorYi = 0.5m;

    /// <summary>价格分位的极值阈值（百分数）。</summary>
    private const decimal QuantileExtreme = 90m;

    /// <summary>
    /// 组装风险视图。
    /// </summary>
    public async Task<ServiceResult<RiskDto>> GetAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var instrument = await instruments.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (instrument is null)
        {
            return ServiceResult<RiskDto>.Fail(ErrorCode.NotFound, $"未找到证券代码 {code}");
        }

        var bars = await daily.GetLatestAsync(code, 250, cancellationToken).ConfigureAwait(false);
        var reports = await finance.GetReportsAsync(code, 4, cancellationToken).ConfigureAwait(false);
        var pledge = await equity.GetPledgeAsync(code, cancellationToken).ConfigureAwait(false);
        var quote = (await quotes.GetByCodesAsync([code], cancellationToken).ConfigureAwait(false))
            .TryGetValue(code, out var snapshot)
                ? snapshot
                : null;

        // 既没行情也没日线时说明数据还没到位，交给调用方重试
        if (bars.Count == 0 && quote is null)
        {
            return ServiceResult<RiskDto>.Fail(ErrorCode.DataNotReady, "风险数据正在采集，请稍后重试");
        }

        var items = new List<RiskItemDto>();
        var metrics = new List<RiskMetricDto>();

        EvaluateDelisting(instrument, items, metrics);
        EvaluateFinance(reports, items, metrics);
        EvaluatePledge(pledge, items, metrics);
        EvaluateVolatility(bars, items, metrics);
        EvaluateDrawdown(bars, items, metrics);
        EvaluateLiquidity(bars, quote, items, metrics);
        EvaluateValuation(bars, instruments, items, metrics);

        var score = ComputeScore(items);

        return ServiceResult<RiskDto>.Success(new RiskDto(
            Code: code,
            Name: instrument.Name,
            AsOf: quote is null ? null : SaTime.Format(quote.AsOf),
            Score: score,
            Grade: score >= HighGradeScore ? "高" : score >= MediumGradeScore ? "中" : "低",
            Items: items
                .OrderByDescending(item => item.Level == "high")
                .ThenByDescending(item => item.Level == "medium")
                .ToList(),
            Metrics: metrics,
            Insights: BuildInsights(instrument, items, score),
            Notes:
            [
                "口径：每条风险都给出实际值与阈值，可自行核对触发原因；风险分为各项权重的加权和（0–100），不是概率。",
                "波动率为最近 60 个交易日收益率的标准差按 250 个交易日年化；回撤为最近 250 个交易日内的最大回撤。",
                "财务风险看最近四期报告：亏损、净利连续两期下滑、经营现金流为负而净利为正。",
                "质押比例阈值为 30%（关注）/ 50%（较高）；退市与 ST 标记直接按名称判定（名称前缀 ST / *ST / 退）。",
                "流动性用最近 20 个交易日的日均成交额衡量，低于 0.5 亿元视为流动性偏弱。",
                "诉讼、监管问询与舆情这三个维度需要公告正文解析（关键词抽取 / 情绪判断），本轮不做，因此不提供——不编造舆情结论。公告列表本身已可用（个股“机构观点”页可见）。",
                "数据来源：本地日线（DuckDB + Parquet）、业绩报表、股权质押、全市场快照。"
            ]));
    }

    /// <summary>退市 / ST 风险。</summary>
    private static void EvaluateDelisting(
        Domain.Entities.Market.Instrument instrument,
        List<RiskItemDto> items,
        List<RiskMetricDto> metrics)
    {
        var isSt = instrument.IsSt;

        metrics.Add(new RiskMetricDto(
            "退市风险标记",
            isSt ? "是（ST / 退市风险）" : "否",
            "名称含 ST / *ST / 退",
            isSt));

        if (isSt)
        {
            items.Add(new RiskItemDto(
                Key: "delisting",
                Category: "退市",
                Level: "high",
                Title: "带 ST / 退市风险标记",
                Detail: "证券名称含 ST、*ST 或退市标记，涨跌幅限制与交易规则均与普通股票不同。",
                Metric: instrument.Name,
                Threshold: "名称含 ST / *ST / 退"));
        }
    }

    /// <summary>财务恶化风险。</summary>
    private static void EvaluateFinance(
        IReadOnlyList<FinancialReport> reports,
        List<RiskItemDto> items,
        List<RiskMetricDto> metrics)
    {
        if (reports.Count == 0)
        {
            metrics.Add(new RiskMetricDto("财务风险", "财报未采集", "最近四期报告", false));
            return;
        }

        var latest = reports[^1];

        // 亏损
        if (latest.NetProfit is < 0)
        {
            items.Add(new RiskItemDto(
                Key: "finance-loss",
                Category: "财务",
                Level: "high",
                Title: "最近一期亏损",
                Detail: $"最新报告期（{SaTime.Format(latest.ReportDate)}）归母净利润为负。",
                Metric: $"{Display.ToYi(latest.NetProfit ?? 0):F2} 亿元",
                Threshold: "< 0"));
        }

        metrics.Add(new RiskMetricDto(
            "最近一期净利同比",
            latest.NetProfitYoy is null ? "—" : $"{latest.NetProfitYoy:+0.00;-0.00}%",
            "连续两期为负为风险",
            latest.NetProfitYoy is < 0));

        // 连续两期净利同比下滑
        if (reports.Count >= 2)
        {
            var current = reports[^1].NetProfitYoy;
            var previous = reports[^2].NetProfitYoy;
            if (current is < 0 && previous is < 0)
            {
                items.Add(new RiskItemDto(
                    Key: "finance-decline",
                    Category: "财务",
                    Level: "medium",
                    Title: "净利润连续两期同比下滑",
                    Detail: $"{SaTime.Format(reports[^2].ReportDate)} {previous:+0.00;-0.00}%、{SaTime.Format(latest.ReportDate)} {current:+0.00;-0.00}%。",
                    Metric: $"{current:+0.00;-0.00}%",
                    Threshold: "两期均为负"));
            }
        }

        // 现金流为负而净利为正
        if (latest.OperatingCashFlowPerShare is < 0 && latest.Eps is > 0)
        {
            items.Add(new RiskItemDto(
                Key: "finance-cashflow",
                Category: "财务",
                Level: "medium",
                Title: "经营现金流为负而净利为正",
                Detail: "每股经营现金流为负、每股收益为正，盈利质量需关注（利润未转化为现金）。",
                Metric: $"{latest.OperatingCashFlowPerShare:F2} 元/股",
                Threshold: "< 0"));
        }
    }

    /// <summary>股权质押风险。</summary>
    private static void EvaluatePledge(
        PledgeStat? pledge,
        List<RiskItemDto> items,
        List<RiskMetricDto> metrics)
    {
        if (pledge?.PledgeRatio is not { } ratio)
        {
            metrics.Add(new RiskMetricDto("质押比例", "—", "< 30% 为正常", false));
            return;
        }

        metrics.Add(new RiskMetricDto(
            "质押比例（占总股本）",
            $"{ratio:F2}%",
            $"关注 ≥ {PledgeMedium}%，较高 ≥ {PledgeHigh}%",
            ratio >= PledgeMedium));

        if (ratio >= PledgeHigh)
        {
            items.Add(new RiskItemDto(
                Key: "pledge-high",
                Category: "质押",
                Level: "high",
                Title: "股权质押比例较高",
                Detail: "高比例质押在股价下跌时可能触发平仓压力，需关注质押方与到期安排。",
                Metric: $"{ratio:F2}%",
                Threshold: $"≥ {PledgeHigh}%"));
        }
        else if (ratio >= PledgeMedium)
        {
            items.Add(new RiskItemDto(
                Key: "pledge-medium",
                Category: "质押",
                Level: "medium",
                Title: "股权质押比例偏高",
                Detail: "质押比例处于需要关注的水平。",
                Metric: $"{ratio:F2}%",
                Threshold: $"≥ {PledgeMedium}%"));
        }
    }

    /// <summary>波动风险（60 日年化波动率）。</summary>
    private static void EvaluateVolatility(
        IReadOnlyList<Domain.History.DailyBar> bars,
        List<RiskItemDto> items,
        List<RiskMetricDto> metrics)
    {
        var volatility = Indicators.AnnualizedVolatility(bars.Select(bar => bar.Close).ToList(), 60);

        metrics.Add(new RiskMetricDto(
            "年化波动率（60 日）",
            volatility is null ? "样本不足" : $"{volatility:F2}%",
            $"关注 ≥ {VolatilityMedium}%，较高 ≥ {VolatilityHigh}%",
            volatility is >= VolatilityMedium));

        if (volatility is >= VolatilityHigh)
        {
            items.Add(new RiskItemDto(
                Key: "volatility-high",
                Category: "波动",
                Level: "high",
                Title: "波动显著高于常态",
                Detail: "近 60 个交易日的年化波动率处于较高水平，价格短期波动剧烈。",
                Metric: $"{volatility:F2}%",
                Threshold: $"≥ {VolatilityHigh}%"));
        }
        else if (volatility is >= VolatilityMedium)
        {
            items.Add(new RiskItemDto(
                Key: "volatility-medium",
                Category: "波动",
                Level: "medium",
                Title: "波动偏高",
                Detail: "近 60 个交易日的年化波动率高于多数标的。",
                Metric: $"{volatility:F2}%",
                Threshold: $"≥ {VolatilityMedium}%"));
        }
    }

    /// <summary>回撤风险（250 日内最大回撤）。</summary>
    private static void EvaluateDrawdown(
        IReadOnlyList<Domain.History.DailyBar> bars,
        List<RiskItemDto> items,
        List<RiskMetricDto> metrics)
    {
        var drawdown = Indicators.MaxDrawdown(bars.Select(bar => bar.Close).ToList());

        metrics.Add(new RiskMetricDto(
            "近 250 日最大回撤",
            drawdown is null ? "样本不足" : $"{drawdown:F2}%",
            $"关注 ≥ {DrawdownMedium}%，较高 ≥ {DrawdownHigh}%",
            drawdown is >= DrawdownMedium));

        if (drawdown is >= DrawdownHigh)
        {
            items.Add(new RiskItemDto(
                Key: "drawdown-high",
                Category: "回撤",
                Level: "high",
                Title: "区间回撤幅度较大",
                Detail: "近 250 个交易日内从最高点到最低点的跌幅较大，持有体验差。",
                Metric: $"{drawdown:F2}%",
                Threshold: $"≥ {DrawdownHigh}%"));
        }
        else if (drawdown is >= DrawdownMedium)
        {
            items.Add(new RiskItemDto(
                Key: "drawdown-medium",
                Category: "回撤",
                Level: "medium",
                Title: "区间回撤偏大",
                Detail: "近 250 个交易日内的最大回撤高于常态。",
                Metric: $"{drawdown:F2}%",
                Threshold: $"≥ {DrawdownMedium}%"));
        }
    }

    /// <summary>流动性风险（20 日均成交额）。</summary>
    private static void EvaluateLiquidity(
        IReadOnlyList<Domain.History.DailyBar> bars,
        Domain.Entities.Market.QuoteSnapshot? quote,
        List<RiskItemDto> items,
        List<RiskMetricDto> metrics)
    {
        // 优先用日线（有 20 日均值），无日线时退化为当日快照成交额
        decimal? averageAmount = null;
        if (bars.Count >= 5)
        {
            averageAmount = bars.TakeLast(20).Average(bar => bar.Amount);
        }
        else if (quote is not null)
        {
            averageAmount = quote.Amount;
        }

        if (averageAmount is null)
        {
            metrics.Add(new RiskMetricDto("日均成交额", "数据不足", $"> {LiquidityFloorYi} 亿元", false));
            return;
        }

        var yi = Display.ToYi(averageAmount.Value);
        metrics.Add(new RiskMetricDto(
            "日均成交额",
            $"{yi:F2} 亿元",
            $"> {LiquidityFloorYi} 亿元",
            yi < LiquidityFloorYi));

        if (yi < LiquidityFloorYi)
        {
            items.Add(new RiskItemDto(
                Key: "liquidity",
                Category: "流动性",
                Level: "medium",
                Title: "日均成交额偏低",
                Detail: "成交清淡会影响成交效率与冲击成本（对目标仓位较大的账户影响更明显）。",
                Metric: $"{yi:F2} 亿元",
                Threshold: $"< {LiquidityFloorYi} 亿元"));
        }
    }

    /// <summary>估值位置风险（价格分位）。</summary>
    private static void EvaluateValuation(
        IReadOnlyList<Domain.History.DailyBar> bars,
        IInstrumentStore instruments,
        List<RiskItemDto> items,
        List<RiskMetricDto> metrics)
    {
        _ = instruments;

        var closes = bars.Select(bar => bar.Close).ToList();
        if (closes.Count < 60)
        {
            metrics.Add(new RiskMetricDto("价格分位（250 日）", "样本不足", "> 90% 或 < 10% 为极值", false));
            return;
        }

        var quantile = Indicators.Quantile(closes, closes[^1], minSamples: 60);
        metrics.Add(new RiskMetricDto(
            "价格分位（250 日）",
            quantile is null ? "样本不足" : $"{quantile:F1}%",
            $"> {QuantileExtreme}% 或 < {100 - QuantileExtreme}% 为极值",
            quantile is > QuantileExtreme or < 100 - QuantileExtreme));

        if (quantile is > QuantileExtreme)
        {
            items.Add(new RiskItemDto(
                Key: "quantile-high",
                Category: "估值",
                Level: "medium",
                Title: "价格处于区间高位",
                Detail: "当前价接近近 250 个交易日的上沿，追高的回撤风险相对更大。",
                Metric: $"{quantile:F1}%",
                Threshold: $"> {QuantileExtreme}%"));
        }
        else if (quantile is < 100 - QuantileExtreme)
        {
            items.Add(new RiskItemDto(
                Key: "quantile-low",
                Category: "估值",
                Level: "medium",
                Title: "价格处于区间低位",
                Detail: "当前价接近近 250 个交易日的下沿，需区分「低估」与「基本面恶化」。",
                Metric: $"{quantile:F1}%",
                Threshold: $"< {100 - QuantileExtreme}%"));
        }
    }

    /// <summary>
    /// 风险计分：按等级加权（高 20 分、中 8 分），上限 100。
    /// </summary>
    private static int ComputeScore(IReadOnlyList<RiskItemDto> items)
    {
        var score = items.Sum(item => item.Level switch
        {
            "high" => 20,
            "medium" => 8,
            _ => 3
        });

        return Math.Clamp(score, 0, 100);
    }

    private static List<string> BuildInsights(
        Domain.Entities.Market.Instrument instrument,
        IReadOnlyList<RiskItemDto> items,
        int score)
    {
        var insights = new List<string>();

        var high = items.Count(item => item.Level == "high");
        var medium = items.Count(item => item.Level == "medium");

        insights.Add(high > 0
            ? $"存在 {high} 项高风险、{medium} 项中风险"
            : medium > 0
                ? $"存在 {medium} 项中风险，未发现高风险项"
                : "未触发任何风险阈值");

        insights.Add($"风险分 {score}/100");

        if (items.Count == 0)
        {
            insights.Add($"{instrument.Name} 在已覆盖的维度上（退市 / 财务 / 质押 / 波动 / 回撤 / 流动性 / 估值）均未触发阈值");
        }

        return insights;
    }

}
