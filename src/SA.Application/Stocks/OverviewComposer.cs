using SA.Application.Abstractions;
using SA.Application.Analysis;
using SA.Application.Capital;
using SA.Application.Equity;
using SA.Application.Finance;
using SA.Application.Common;
using SA.Application.Market;
using SA.Contracts.Common;
using SA.Contracts.Stock;
using SA.Domain.Common;

namespace SA.Application.Stocks;

/// <summary>
/// 个股总览组装器：把 8 个域的数据拼成 8 张摘要卡（概要设计 §3.1 的一次请求聚合）。
/// </summary>
/// <remarks>
/// <para>
/// 本批（第 3 批）只有趋势卡有真实数据，其余 7 张卡返回 <c>Status = collecting</c>，
/// 由界面渲染成「采集中」的空态。<b>不返回假数据、不返回空卡</b>——实施计划 §5.1 明确
/// <c>1003</c> 语义是本轮的关键，摘要卡必须能自我说明为什么还没有数字。
/// </para>
/// <para>
/// 各域批次落地时，只需在这里把对应模块从「占位」换成真实实现，卡片结构不变。
/// </para>
/// </remarks>
public sealed class OverviewComposer(
    IInstrumentStore instruments,
    IQuoteSnapshotStore quotes,
    TrendAnalyzer trend,
    FinanceService finance,
    EquityService equity,
    CapitalService capital)
{
    /// <summary>8 个模块的定义与顺序，与导航和原型矩阵页一致。</summary>
    private static readonly (string Key, string Name)[] Modules =
    [
        ("trend", "趋势与价格结构"),
        ("finance", "盈利与财务表现"),
        ("equity", "投资与股权结构"),
        ("capital", "资金面与筹码"),
        ("industry", "行业与同业对比"),
        ("events", "事件时间线与影响"),
        ("risk", "风险与舆情监控"),
        ("rating", "机构评级与预测")
    ];

    /// <summary>
    /// 组装总览。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<ServiceResult<StockOverviewDto>> ComposeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var instrument = await instruments.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (instrument is null)
        {
            return ServiceResult<StockOverviewDto>.Fail(ErrorCode.NotFound, $"未找到证券代码 {code}");
        }

        var quote = (await quotes.GetByCodesAsync([code], cancellationToken).ConfigureAwait(false))
            .TryGetValue(code, out var snapshot)
                ? snapshot
                : null;

        var cards = new List<ModuleCardDto>(Modules.Length);
        foreach (var (key, name) in Modules)
        {
            cards.Add(key switch
            {
                "trend" => await BuildTrendCardAsync(code, name, cancellationToken).ConfigureAwait(false),
                "finance" => await BuildFinanceCardAsync(code, name, cancellationToken).ConfigureAwait(false),
                "equity" => await BuildEquityCardAsync(code, name, cancellationToken).ConfigureAwait(false),
                "capital" => await BuildCapitalCardAsync(code, name, cancellationToken).ConfigureAwait(false),
                _ => PendingCard(key, name)
            });
        }

        var trendCard = cards[0];
        var summary = trendCard.Status == "ready"
            ? trendCard.Tags.Concat(new[] { new ModuleTagDto(trendCard.Summary ?? string.Empty, "neutral") })
                .Where(tag => tag.Text.Length > 0)
                .ToList()
            : [new ModuleTagDto("趋势数据采集中", "warn")];

        return ServiceResult<StockOverviewDto>.Success(new StockOverviewDto(
            Profile: ToProfile(instrument, quote),
            Modules: cards,
            Summary: summary,
            Notes:
            [
                "口径：日线为前复权；行业为东财行业。",
                "总览卡片的数字与各模块页共用同一数据源，模块页请求更细的序列数据。",
                "已接入：趋势与价格结构、盈利与财务表现、投资与股权结构；其余模块按批次接入，卡片会明确标注采集中。"
            ]));
    }

    /// <summary>行情条。</summary>
    public async Task<ServiceResult<StockProfileDto>> GetProfileAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var instrument = await instruments.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (instrument is null)
        {
            return ServiceResult<StockProfileDto>.Fail(ErrorCode.NotFound, $"未找到证券代码 {code}");
        }

        var quote = (await quotes.GetByCodesAsync([code], cancellationToken).ConfigureAwait(false))
            .TryGetValue(code, out var snapshot)
                ? snapshot
                : null;

        return ServiceResult<StockProfileDto>.Success(ToProfile(instrument, quote));
    }

    private async Task<ModuleCardDto> BuildTrendCardAsync(
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        var indicatorRows = await trend.GetThumbAsync(code, 60, cancellationToken).ConfigureAwait(false);
        var result = await trend.GetTrendAsync(code, 120, cancellationToken).ConfigureAwait(false);

        if (!result.Ok || result.Value is null)
        {
            // 日线尚未回补：卡片本身也要显示「采集中」，而不是一张空卡
            return new ModuleCardDto(
                Key: "trend",
                Name: name,
                Status: result.Error == ErrorCode.DataNotReady ? "collecting" : "failed",
                Tags: [],
                Kpis: [],
                Thumb: [],
                Summary: result.Message,
                Link: $"/stock/{code}/trend");
        }

        var dto = result.Value;
        var last = dto.Candles.Count > 0 ? dto.Candles[^1] : null;

        var kpis = new List<ModuleKpiDto>();
        if (dto.Levels.Quantile250 is { } quantile)
        {
            kpis.Add(new ModuleKpiDto("250 日分位", $"{quantile:F1}%", quantile >= 70 ? "up" : quantile <= 30 ? "down" : "neutral"));
        }
        else
        {
            // 样本不足：显示「—」并说明原因，不猜一个分位
            kpis.Add(new ModuleKpiDto("250 日分位", "—", "neutral"));
        }

        if (dto.RelativeStrength.Value is { } rs)
        {
            kpis.Add(new ModuleKpiDto("相对沪深300", $"{rs:+0.00;-0.00}%", rs >= 0 ? "up" : "down"));
        }

        if (dto.Levels.AboveMa20Pct is { } aboveMa20)
        {
            kpis.Add(new ModuleKpiDto("距 20 日线", $"{aboveMa20:+0.00;-0.00}%", aboveMa20 >= 0 ? "up" : "down"));
        }

        return new ModuleCardDto(
            Key: "trend",
            Name: name,
            Status: "ready",
            Tags: dto.Insights.Select(i => new ModuleTagDto(i.Text, i.Tone)).ToList(),
            Kpis: kpis,
            Thumb: indicatorRows,
            Summary: BuildSummary(dto.Insights, last?.C),
            Link: $"/stock/{code}/trend");
    }

    private static string BuildSummary(IReadOnlyList<Contracts.Trend.TrendInsightDto> insights, decimal? close)
    {
        var text = string.Join(" · ", insights.Take(3).Select(i => i.Text));
        return close is null ? text : $"{text}（收 {close:F2}）";
    }

    /// <summary>
    /// 财务卡：最新一期营收/净利/ROE + 缩略图（近 20 期营收）。
    /// </summary>
    private async Task<ModuleCardDto> BuildFinanceCardAsync(
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        var result = await finance.GetAsync(code, cancellationToken).ConfigureAwait(false);
        if (!result.Ok || result.Value is null)
        {
            return new ModuleCardDto(
                Key: "finance",
                Name: name,
                Status: result.Error == ErrorCode.DataNotReady ? "collecting" : "failed",
                Tags: [],
                Kpis: [],
                Thumb: [],
                Summary: result.Message,
                Link: $"/stock/{code}/finance");
        }

        var dto = result.Value;
        var kpis = new List<ModuleKpiDto>();

        if (dto.Latest?.Revenue is { } revenue)
        {
            kpis.Add(new ModuleKpiDto("营业收入", $"{revenue:F2} 亿", "neutral"));
        }

        if (dto.Latest?.RevenueYoy is { } revenueYoy)
        {
            kpis.Add(new ModuleKpiDto("营收同比", $"{revenueYoy:+0.00;-0.00}%", revenueYoy >= 0 ? "up" : "down"));
        }

        if (dto.Latest?.NetProfit is { } netProfit)
        {
            kpis.Add(new ModuleKpiDto("归母净利", $"{netProfit:F2} 亿", "neutral"));
        }

        if (dto.Latest?.NetProfitYoy is { } profitYoy)
        {
            kpis.Add(new ModuleKpiDto("净利同比", $"{profitYoy:+0.00;-0.00}%", profitYoy >= 0 ? "up" : "down"));
        }

        if (dto.Latest?.Roe is { } roe)
        {
            kpis.Add(new ModuleKpiDto("加权 ROE", $"{roe:F2}%", roe >= 8 ? "up" : "neutral"));
        }

        if (dto.Latest?.GrossMargin is { } margin)
        {
            kpis.Add(new ModuleKpiDto("毛利率", $"{margin:F2}%", "neutral"));
        }

        // 缩略图用营收序列：财务是季频，20 期足够看出多年趋势
        var thumb = dto.Trend
            .Select(point => point.Revenue)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToList();

        return new ModuleCardDto(
            Key: "finance",
            Name: name,
            Status: "ready",
            Tags: dto.Insights.Take(3).Select(text => new ModuleTagDto(text, ToneOf(text))).ToList(),
            Kpis: kpis,
            Thumb: thumb,
            Summary: dto.Latest is null
                ? null
                : $"{dto.Latest.ReportType ?? dto.Latest.ReportDate}（{dto.Latest.ReportDate}）",
            Link: $"/stock/{code}/finance");
    }

    /// <summary>
    /// 股权卡：股东集中度 / 股东户数 / 质押比例。
    /// </summary>
    private async Task<ModuleCardDto> BuildEquityCardAsync(
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        var result = await equity.GetAsync(code, cancellationToken).ConfigureAwait(false);
        if (!result.Ok || result.Value is null)
        {
            return new ModuleCardDto(
                Key: "equity",
                Name: name,
                Status: result.Error == ErrorCode.DataNotReady ? "collecting" : "failed",
                Tags: [],
                Kpis: [],
                Thumb: [],
                Summary: result.Message,
                Link: $"/stock/{code}/equity");
        }

        var dto = result.Value;
        var kpis = new List<ModuleKpiDto>();

        if (dto.Latest?.TotalRatio is { } total)
        {
            kpis.Add(new ModuleKpiDto("前十大合计", $"{total:F2}%", total >= 50 ? "up" : "neutral"));
        }

        if (dto.HolderCounts.Count > 0)
        {
            var latestCount = dto.HolderCounts[^1];
            kpis.Add(new ModuleKpiDto("股东户数", latestCount.HolderNum.ToString("N0"), "neutral"));

            // 户数下降 = 筹码集中，用与涨跌无关的中性色调，仅在标签里说明方向
            if (latestCount.Change is { } change)
            {
                kpis.Add(new ModuleKpiDto(
                    "户数变化",
                    $"{(change >= 0 ? "+" : string.Empty)}{change:N0}",
                    change < 0 ? "up" : "down"));
            }
        }

        if (dto.Pledge?.PledgeRatio is { } pledgeRatio)
        {
            kpis.Add(new ModuleKpiDto("质押比例", $"{pledgeRatio:F2}%", pledgeRatio >= 30 ? "down" : "neutral"));
        }

        // 缩略图用股东户数序列：户数趋势是这一页最直观的一条线
        var thumb = dto.HolderCounts.Select(count => (decimal)count.HolderNum).ToList();

        return new ModuleCardDto(
            Key: "equity",
            Name: name,
            Status: "ready",
            Tags: dto.Insights.Take(3).Select(text => new ModuleTagDto(text, ToneOf(text))).ToList(),
            Kpis: kpis,
            Thumb: thumb,
            Summary: dto.Latest is null ? null : $"报告期 {dto.Latest.EndDate}",
            Link: $"/stock/{code}/equity");
    }

    /// <summary>
    /// 资金面卡：近 5 日主力净额、两融余额、陆股通增减。
    /// </summary>
    private async Task<ModuleCardDto> BuildCapitalCardAsync(
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        var result = await capital.GetAsync(code, cancellationToken).ConfigureAwait(false);
        if (!result.Ok || result.Value is null)
        {
            return new ModuleCardDto(
                Key: "capital",
                Name: name,
                Status: result.Error == ErrorCode.DataNotReady ? "collecting" : "failed",
                Tags: [],
                Kpis: [],
                Thumb: [],
                Summary: result.Message,
                Link: $"/stock/{code}/capital");
        }

        var dto = result.Value;
        var kpis = new List<ModuleKpiDto>();

        if (dto.Summary is { } summary)
        {
            kpis.Add(new ModuleKpiDto(
                "近 5 日主力",
                $"{(summary.MainNet >= 0 ? "+" : string.Empty)}{summary.MainNet:F2} 亿",
                summary.MainNet >= 0 ? "up" : "down"));
            kpis.Add(new ModuleKpiDto("流入天数", $"{summary.InflowDays}/5", summary.InflowDays >= 3 ? "up" : "down"));
        }

        if (dto.Margins.Count > 0 && dto.Margins[^1].FinanceBalance is { } finance)
        {
            kpis.Add(new ModuleKpiDto("融资余额", $"{finance:F2} 亿", "neutral"));
        }

        if (dto.Northbound.Count > 0 && dto.Northbound[0].AddShares is { } addShares)
        {
            kpis.Add(new ModuleKpiDto(
                "陆股通增减",
                $"{(addShares >= 0 ? "+" : string.Empty)}{addShares:F2} 万股",
                addShares >= 0 ? "up" : "down"));
        }

        // 缩略图用主力净额序列：这一页最直观的一条线
        var thumb = dto.FundFlow.Select(point => point.MainNet).ToList();

        return new ModuleCardDto(
            Key: "capital",
            Name: name,
            Status: "ready",
            Tags: dto.Insights.Take(3).Select(text => new ModuleTagDto(text, ToneOf(text))).ToList(),
            Kpis: kpis,
            Thumb: thumb,
            Summary: dto.FundFlow.Count == 0 ? null : $"资金流至 {dto.FundFlow[^1].Date}",
            Link: $"/stock/{code}/capital");
    }

    /// <summary>把结论文案映射到色调：含「下滑/负/不增利」为跌，含「增长/上升」为涨。</summary>
    private static string ToneOf(string text) =>
        text.Contains("下滑", StringComparison.Ordinal) || text.Contains("不增利", StringComparison.Ordinal)
            ? "down"
            : text.Contains("增长", StringComparison.Ordinal)
                ? "up"
                : "neutral";

    /// <summary>尚未接入的模块：状态为 collecting，界面据此显示采集中空态。</summary>
    private static ModuleCardDto PendingCard(string key, string name) =>
        new(
            Key: key,
            Name: name,
            Status: "collecting",
            Tags: [new ModuleTagDto("本批未接入", "warn")],
            Kpis: [],
            Thumb: [],
            Summary: "该模块的数据接口尚未接入，界面不展示任何数值。",
            Link: string.Empty);

    private static StockProfileDto ToProfile(Domain.Entities.Market.Instrument instrument, Domain.Entities.Market.QuoteSnapshot? quote) =>
        new(
            Code: instrument.Code,
            Name: instrument.Name,
            Py: instrument.Pinyin,
            Board: instrument.Board,
            Industry: instrument.Industry,
            Price: quote is null ? null : MarketService.Trim(quote.Price),
            Chg: quote is null ? null : MarketService.Trim(quote.Change),
            Pct: quote is null ? null : MarketService.Trim(quote.Pct),
            Open: quote is null ? null : MarketService.Trim(quote.Open),
            High: quote is null ? null : MarketService.Trim(quote.High),
            Low: quote is null ? null : MarketService.Trim(quote.Low),
            PrevClose: quote is null ? null : MarketService.Trim(quote.PrevClose),
            Volume: quote is null ? null : quote.Volume,
            Amount: quote is null ? null : MarketService.ToYi(quote.Amount),
            Turnover: quote is null ? null : MarketService.Trim(quote.Turnover),
            VolRatio: quote is null ? null : MarketService.Trim(quote.VolRatio),
            Cap: quote is null ? null : MarketService.ToYi(quote.MarketCap),
            FloatCap: quote is null ? null : MarketService.ToYi(quote.FloatCap),
            Pe: quote is null || quote.Pe <= 0 ? null : MarketService.Trim(quote.Pe),
            PeTtm: quote is null || quote.PeTtm <= 0 ? null : MarketService.Trim(quote.PeTtm),
            Pb: quote is null || quote.Pb <= 0 ? null : MarketService.Trim(quote.Pb),
            IsSt: instrument.IsSt,
            AsOf: quote is null ? null : SaTime.Format(quote.AsOf));
}
