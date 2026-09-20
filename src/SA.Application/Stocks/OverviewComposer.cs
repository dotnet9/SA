using SA.Application.Abstractions;
using SA.Application.Analysis;
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
    TrendAnalyzer trend)
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
            cards.Add(key == "trend"
                ? await BuildTrendCardAsync(code, name, cancellationToken).ConfigureAwait(false)
                : PendingCard(key, name));
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
                "本批仅「趋势与价格结构」接真；其余模块按批次接入，卡片会明确标注采集中。"
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
