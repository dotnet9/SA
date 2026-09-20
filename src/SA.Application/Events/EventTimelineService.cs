using SA.Application.Abstractions;
using SA.Application.Common;
using SA.Contracts.Common;
using SA.Contracts.Events;
using SA.Domain.Common;
using SA.Domain.Entities.Capital;
using SA.Domain.Entities.Equity;
using SA.Domain.Entities.Events;
using SA.Domain.Entities.Finance;

namespace SA.Application.Events;

/// <summary>
/// 事件时间线与拓扑图。
/// </summary>
/// <remarks>
/// <para>
/// <b>不新增采集源</b>：事件由「已经落地的结构化数据」派生——业绩预告与定期报告（财务）、
/// 股东户数与质押（股权）、龙虎榜与大宗交易（资金面）。这样做的好处是事件与它引用的数据永远一致：
/// 点开一条事件能看到来源是同一张表，而不是另一套可能有延迟或口径差异的公告数据。
/// </para>
/// <para>
/// 每类事件的影响方向由<b>可复算的规则</b>给出，并允许人工覆盖（<see cref="EventAnnotation"/>）；
/// 覆盖只改判读，不改原始数据。
/// </para>
/// <para>
/// 四张拓扑图回答四个具体问题：谁控制这家公司（股东）、它在行业里的位置（行业）、
/// 哪些事件在影响它（事件）、谁在跟它做大宗交易（交易对手）。
/// 供应链关系需要专门的产业数据源，本轮没有，因此不画——不画比画一张假图好。
/// </para>
/// </remarks>
public sealed class EventTimelineService(
    IFinanceStore finance,
    IEquityStore equity,
    ICapitalStore capital,
    IInstrumentStore instruments,
    IQuoteSnapshotStore quotes,
    ISectorStore sectors,
    IEventAnnotationStore annotations,
    IOnDemandQueue onDemand)
{
    /// <summary>时间线最多返回多少条。</summary>
    public const int MaxEvents = 60;

    /// <summary>拓扑图节点上限（超过则图不可读）。</summary>
    private const int MaxTopologyNodes = 40;

    /// <summary>
    /// 组装事件与拓扑视图。
    /// </summary>
    public async Task<ServiceResult<EventTimelineDto>> GetAsync(
        string code,
        bool canAnnotate,
        CancellationToken cancellationToken = default)
    {
        var instrument = await instruments.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (instrument is null)
        {
            return ServiceResult<EventTimelineDto>.Fail(ErrorCode.NotFound, $"未找到证券代码 {code}");
        }

        var reports = await finance.GetReportsAsync(code, 12, cancellationToken).ConfigureAwait(false);
        var forecasts = await finance.GetForecastsAsync(code, 8, cancellationToken).ConfigureAwait(false);
        var topHolders = await equity.GetTopHoldersAsync(code, cancellationToken).ConfigureAwait(false);
        var holderCounts = await equity.GetHolderCountsAsync(code, cancellationToken).ConfigureAwait(false);
        var pledge = await equity.GetPledgeAsync(code, cancellationToken).ConfigureAwait(false);
        var billboards = await capital.GetBillboardsAsync(code, 20, cancellationToken).ConfigureAwait(false);
        var blockTrades = await capital.GetBlockTradesAsync(code, 20, cancellationToken).ConfigureAwait(false);
        var northbound = await capital.GetNorthboundAsync(code, 8, cancellationToken).ConfigureAwait(false);

        var events = BuildEvents(reports, forecasts, holderCounts, pledge, billboards, blockTrades, northbound);

        // 应用人工标注（覆盖派生判读）
        var annotationMap = await annotations.GetByCodeAsync(code, cancellationToken).ConfigureAwait(false);
        var annotated = events
            .Select(item => annotationMap.TryGetValue(item.Key, out var annotation)
                ? item with
                {
                    Tone = annotation.Tone,
                    Impact = annotation.Impact,
                    Annotated = true,
                    AnnotationNote = annotation.Note
                }
                : item)
            .ToList();

        var ordered = annotated
            .OrderByDescending(item => item.Date, StringComparer.Ordinal)
            .ThenBy(item => item.Type, StringComparer.Ordinal)
            .Take(MaxEvents)
            .ToList();

        var latestQuote = (await quotes.GetByCodesAsync([code], cancellationToken).ConfigureAwait(false))
            .TryGetValue(code, out var quote)
                ? quote
                : null;

        var peerQuotes = latestQuote is null
            ? []
            : await LoadIndustryPeersAsync(instrument, cancellationToken).ConfigureAwait(false);

        var topologies = new List<TopologyDto>
        {
            BuildShareholderTopology(code, instrument.Name, topHolders),
            BuildIndustryTopology(code, instrument.Name, instrument.Industry, peerQuotes, await sectors.GetAllAsync(cancellationToken).ConfigureAwait(false)),
            BuildEventTopology(code, instrument.Name, ordered),
            BuildCounterpartyTopology(code, instrument.Name, blockTrades, billboards)
        };

        // 只有在「既没有事件、也没有任何可画的拓扑图」时才算数据未就绪。
        // 有股东数据但暂时没有事件时应当正常出页（拓扑图本身就有信息），
        // 否则用户会看到一张空白页而不是「有结构、暂无事件」的真实状态。
        if (ordered.Count == 0 && topologies.TrueForAll(topology => !topology.Available))
        {
            onDemand.TryEnqueue(code);
            return ServiceResult<EventTimelineDto>.Fail(ErrorCode.DataNotReady, "事件与拓扑数据正在采集，请稍后重试");
        }

        // 事件派生于财务 / 股权 / 资金面三块数据；缺哪块就把该标的入队补齐，
        // 否则「首次打开事件页」会一直停在只有行业拓扑的状态
        var collecting = ordered.Count == 0 || topHolders.Count == 0;
        if (collecting)
        {
            onDemand.TryEnqueue(code);
        }

        return ServiceResult<EventTimelineDto>.Success(new EventTimelineDto(
            Code: code,
            Name: instrument.Name,
            AsOf: latestQuote is null ? null : SaTime.Format(latestQuote.AsOf),
            Events: ordered,
            Summary: Summarize(ordered),
            Topologies: topologies,
            Insights: BuildInsights(ordered),
            CanAnnotate: canAnnotate,
            Collecting: collecting,
            Notes:
            [
                "口径：事件由本地已落地的结构化数据派生（财务 / 股权 / 资金面），不是另一套公告数据源，因此与各模块页的数字始终一致。",
                "影响方向与强度默认由规则给出，可人工覆盖；覆盖只改判读，不改原始数据，并记录修改人与备注。",
                "事件键由「类型 + 日期 + 关键字段」拼成，稳定可复算，因此重复标注即更新、重复采集不会产生重复事件。",
                "四种拓扑图分别回答：谁在控制（股东）、它在行业里的位置（行业）、哪些事件在影响它（事件）、谁在跟它做大宗交易（交易对手）。",
                "供应链拓扑需要专门的产业数据源，本轮没有可用的公开源，因此不提供——不画比画一张推测出来的关系图更负责。",
                "数据来源：东方财富公开接口（业绩报表 / 业绩预告 / 股东与质押 / 龙虎榜 / 大宗交易 / 陆股通）。"
            ]));
    }

    /* ------------------------------------------------------------------
       事件派生
       ------------------------------------------------------------------ */

    private static List<EventItemDto> BuildEvents(
        IReadOnlyList<FinancialReport> reports,
        IReadOnlyList<EarningsForecast> forecasts,
        IReadOnlyList<HolderCount> holderCounts,
        PledgeStat? pledge,
        IReadOnlyList<BillboardRecord> billboards,
        IReadOnlyList<BlockTrade> blockTrades,
        IReadOnlyList<NorthboundHolding> northbound)
    {
        var events = new List<EventItemDto>();

        foreach (var forecast in forecasts)
        {
            var tone = forecast.ChangeMax switch
            {
                > 0 => "up",
                < 0 => "down",
                _ => "neutral"
            };

            var impact = Math.Abs(forecast.ChangeMax ?? 0) switch
            {
                >= 50 => 5,
                >= 20 => 4,
                >= 10 => 3,
                >= 3 => 2,
                _ => 1
            };

            events.Add(new EventItemDto(
                Key: $"forecast:{SaTime.Format(forecast.ReportDate)}",
                Date: SaTime.Format(forecast.NoticeDate ?? forecast.ReportDate),
                Type: EventTypes.Forecast,
                TypeName: "业绩预告",
                Title: $"{forecast.ForecastType ?? "业绩预告"}（{SaTime.Format(forecast.ReportDate)}）",
                Detail: forecast.Summary is null
                    ? null
                    : Truncate(forecast.Summary, 160),
                Tone: tone,
                Impact: impact,
                Source: "业绩预告",
                Annotated: false,
                AnnotationNote: null));
        }

        foreach (var report in reports)
        {
            // 只有给出公告日的报告才进时间线，否则无法定位「什么时候发生的」
            if (report.NoticeDate is null)
            {
                continue;
            }

            var tone = report.NetProfitYoy switch
            {
                > 0 => "up",
                < 0 => "down",
                _ => "neutral"
            };

            var impact = Math.Abs(report.NetProfitYoy ?? 0) switch
            {
                >= 50 => 5,
                >= 20 => 4,
                >= 10 => 3,
                >= 3 => 2,
                _ => 1
            };

            events.Add(new EventItemDto(
                Key: $"report:{SaTime.Format(report.ReportDate)}",
                Date: SaTime.Format(report.NoticeDate.Value),
                Type: EventTypes.Report,
                TypeName: "定期报告",
                Title: $"{report.ReportType ?? "定期报告"}披露",
                Detail: BuildReportDetail(report),
                Tone: tone,
                Impact: impact,
                Source: "业绩报表",
                Annotated: false,
                AnnotationNote: null));

            if (!string.IsNullOrWhiteSpace(report.DividendPlan))
            {
                events.Add(new EventItemDto(
                    Key: $"dividend:{SaTime.Format(report.ReportDate)}",
                    Date: SaTime.Format(report.NoticeDate.Value),
                    Type: EventTypes.Dividend,
                    TypeName: "分红方案",
                    Title: report.DividendPlan!,
                    Detail: report.DividendYield is null ? null : $"股息率 {report.DividendYield:F2}%",
                    // 分红本身是中性事件：高分红偏积极，但也可能是缺乏再投资机会
                    Tone: "neutral",
                    Impact: 2,
                    Source: "业绩报表",
                    Annotated: false,
                    AnnotationNote: null));
            }
        }

        // 股东户数：只在变动较大时进时间线，否则每期都产生一条噪音
        for (var i = 1; i < holderCounts.Count; i++)
        {
            var current = holderCounts[i];
            var ratio = current.HolderNumRatio;
            if (ratio is null || Math.Abs(ratio.Value) < 5m)
            {
                continue;
            }

            // 户数下降 = 筹码集中（偏积极）；上升 = 分散（偏消极）
            var tone = ratio < 0 ? "up" : "down";

            events.Add(new EventItemDto(
                Key: $"holderCount:{SaTime.Format(current.EndDate)}",
                Date: SaTime.Format(current.NoticeDate ?? current.EndDate),
                Type: EventTypes.HolderCount,
                TypeName: "股东户数",
                Title: $"股东户数{(ratio < 0 ? "减少" : "增加")} {Math.Abs(ratio.Value):F2}%",
                Detail: $"{current.HolderNum:N0} 户（上期 {current.PreviousHolderNum?.ToString("N0") ?? "—"}）",
                Tone: tone,
                Impact: Math.Abs(ratio.Value) >= 15m ? 3 : 2,
                Source: "股东户数",
                Annotated: false,
                AnnotationNote: null));
        }

        if (pledge is not null && pledge.PledgeRatio is not null)
        {
            events.Add(new EventItemDto(
                Key: $"pledge:{SaTime.Format(pledge.TradeDate)}",
                Date: SaTime.Format(pledge.TradeDate),
                Type: EventTypes.Pledge,
                TypeName: "股权质押",
                Title: $"质押比例 {pledge.PledgeRatio:F2}%",
                Detail: $"质押 {pledge.PledgeSharesWan?.ToString("N0") ?? "—"} 万股 · {pledge.PledgeDealNum ?? 0} 笔",
                // 质押比例越高风险越大
                Tone: pledge.PledgeRatio >= 30m ? "down" : "neutral",
                Impact: pledge.PledgeRatio >= 50m ? 4 : pledge.PledgeRatio >= 30m ? 3 : 1,
                Source: "股权质押",
                Annotated: false,
                AnnotationNote: null));
        }

        foreach (var billboard in billboards)
        {
            events.Add(new EventItemDto(
                Key: $"billboard:{SaTime.Format(billboard.TradeDate)}:{billboard.Reason}",
                Date: SaTime.Format(billboard.TradeDate),
                Type: EventTypes.Billboard,
                TypeName: "龙虎榜",
                Title: $"上榜：{Truncate(billboard.Reason ?? "未标注原因", 40)}",
                Detail: BuildBillboardDetail(billboard),
                Tone: (billboard.NetAmount ?? 0) >= 0 ? "up" : "down",
                Impact: Math.Abs(billboard.NetAmount ?? 0) >= 300_000_000m ? 3 : 2,
                Source: "龙虎榜",
                Annotated: false,
                AnnotationNote: null));
        }

        foreach (var block in blockTrades)
        {
            // 溢价成交偏积极、折价成交偏消极；平价视为中性
            var tone = block.PremiumRatio switch
            {
                > 1 => "up",
                < -1 => "down",
                _ => "neutral"
            };

            events.Add(new EventItemDto(
                Key: $"blocktrade:{SaTime.Format(block.TradeDate)}:{block.DealPrice}",
                Date: SaTime.Format(block.TradeDate),
                Type: EventTypes.BlockTrade,
                TypeName: "大宗交易",
                Title: $"大宗交易 {Display.ToYi(block.DealAmount ?? 0):F2} 亿元（{block.PremiumRatio:+0.00;-0.00}%）",
                Detail: $"{block.DealPrice:F2} 元 · 买卖方：{block.BuyerName ?? "—"} / {block.SellerName ?? "—"}",
                Tone: tone,
                Impact: Math.Abs(block.DealAmount ?? 0) >= 100_000_000m ? 3 : 2,
                Source: "大宗交易",
                Annotated: false,
                AnnotationNote: null));
        }

        foreach (var holding in northbound)
        {
            if (holding.AddShares is null)
            {
                continue;
            }

            events.Add(new EventItemDto(
                Key: $"northbound:{SaTime.Format(holding.HoldDate)}",
                Date: SaTime.Format(holding.HoldDate),
                Type: EventTypes.Northbound,
                TypeName: "陆股通",
                Title: $"陆股通{(holding.AddShares >= 0 ? "增持" : "减持")} {Math.Abs(holding.AddShares.Value / 10_000m):N2} 万股",
                Detail: $"{holding.DateType ?? SaTime.Format(holding.HoldDate)} · 持股 {(holding.HoldShares ?? 0) / 10_000m:N2} 万股",
                Tone: holding.AddShares >= 0 ? "up" : "down",
                // 季频数据，影响强度不高但方向明确
                Impact: 2,
                Source: "陆股通持股",
                Annotated: false,
                AnnotationNote: null));
        }

        return events;
    }

    private static string? BuildReportDetail(FinancialReport report)
    {
        var parts = new List<string>();
        if (report.Revenue is { } revenue)
        {
            parts.Add($"营收 {Display.ToYi(revenue):F2} 亿");
        }

        if (report.NetProfit is { } netProfit)
        {
            parts.Add($"净利 {Display.ToYi(netProfit):F2} 亿");
        }

        if (report.RevenueYoy is { } revenueYoy)
        {
            parts.Add($"营收同比 {revenueYoy:+0.00;-0.00}%");
        }

        if (report.NetProfitYoy is { } profitYoy)
        {
            parts.Add($"净利同比 {profitYoy:+0.00;-0.00}%");
        }

        if (report.Roe is { } roe)
        {
            parts.Add($"ROE {roe:F2}%");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static string? BuildBillboardDetail(BillboardRecord billboard)
    {
        var parts = new List<string>();
        if (billboard.NetAmount is { } net)
        {
            parts.Add($"净买入 {Display.ToYi(net):F2} 亿");
        }

        if (billboard.Explain is not null)
        {
            parts.Add(billboard.Explain);
        }

        if (billboard.Next1Change is { } next1)
        {
            parts.Add($"次日 {next1:+0.00;-0.00}%");
        }

        if (billboard.Next5Change is { } next5)
        {
            parts.Add($"后 5 日 {next5:+0.00;-0.00}%");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static IReadOnlyList<EventSummaryDto> Summarize(IReadOnlyList<EventItemDto> events)
    {
        return events
            .GroupBy(item => item.Type)
            .Select(group =>
            {
                var list = group.ToList();
                var up = list.Count(item => item.Tone == "up");
                var down = list.Count(item => item.Tone == "down");

                return new EventSummaryDto(
                    Type: group.Key,
                    TypeName: list[0].TypeName,
                    Count: list.Count,
                    LatestDate: list.Max(item => item.Date),
                    NetTone: up > down ? "up" : down > up ? "down" : "neutral");
            })
            .OrderByDescending(item => item.Count)
            .ToList();
    }

    private static IReadOnlyList<string> BuildInsights(IReadOnlyList<EventItemDto> events)
    {
        var insights = new List<string>();

        var up = events.Count(item => item.Tone == "up");
        var down = events.Count(item => item.Tone == "down");
        var neutral = events.Count - up - down;

        insights.Add($"共 {events.Count} 条事件：偏积极 {up}、偏消极 {down}、中性 {neutral}");

        // 近 90 天内的高影响事件最值得先看
        var recent = events
            .Where(item => item.Impact >= 4)
            .OrderByDescending(item => item.Date, StringComparer.Ordinal)
            .Take(3)
            .ToList();

        foreach (var item in recent)
        {
            insights.Add($"高影响事件：{item.Date} {item.Title}");
        }

        if (events.Count > 0)
        {
            insights.Add($"最近事件：{events[0].Date} {events[0].Title}");
        }

        return insights;
    }

    /* ------------------------------------------------------------------
       拓扑图
       ------------------------------------------------------------------ */

    /// <summary>股东拓扑：股东 → 公司。</summary>
    private static TopologyDto BuildShareholderTopology(
        string code,
        string name,
        IReadOnlyList<TopHolder> holders)
    {
        var latest = holders
            .Where(holder => !holder.IsFreeFloat)
            .GroupBy(holder => holder.EndDate)
            .OrderByDescending(group => group.Key)
            .FirstOrDefault();

        if (latest is null)
        {
            return Unavailable(
                "shareholder",
                "股东拓扑",
                "谁在控制这家公司：前十大股东与持股比例。",
                "尚未采集到十大股东数据。");
        }

        var nodes = new List<TopologyNodeDto>
        {
            new($"stock:{code}", name, 0, 100m, true, $"报告期 {SaTime.Format(latest.Key)}")
        };

        var edges = new List<TopologyEdgeDto>();
        foreach (var holder in latest.OrderByDescending(item => item.HoldNum).Take(MaxTopologyNodes))
        {
            var id = $"holder:{holder.Rank}";
            nodes.Add(new TopologyNodeDto(
                id,
                Truncate(holder.HolderName, 14),
                1,
                holder.HoldRatio ?? 0m,
                false,
                $"持股 {holder.HoldNum / 10_000m:N0} 万股 · 占总股本 {holder.HoldRatio:F2}%"));

            edges.Add(new TopologyEdgeDto(
                id,
                $"stock:{code}",
                holder.HoldRatio is null ? null : $"{holder.HoldRatio:F2}%",
                "neutral"));
        }

        return new TopologyDto(
            "shareholder",
            "股东拓扑",
            "谁在控制这家公司：前十大股东与持股比例（边上的数字是占总股本比例）。",
            ["本公司", "股东"],
            nodes,
            edges,
            true,
            null);
    }

    /// <summary>行业拓扑：行业 → 该股与同业。</summary>
    private TopologyDto BuildIndustryTopology(
        string code,
        string name,
        string? industry,
        IReadOnlyList<PeerInfo> peers,
        IReadOnlyList<Domain.Entities.Market.Sector> sectors)
    {
        if (industry is null)
        {
            return Unavailable(
                "industry",
                "行业拓扑",
                "它在行业里的位置：所属行业与主要同业。",
                "该标的缺少行业分类，无法画行业关系。");
        }

        var sector = sectors.FirstOrDefault(row => string.Equals(row.Name, industry, StringComparison.Ordinal));
        var nodes = new List<TopologyNodeDto>
        {
            new($"industry:{industry}", industry, 0, sector?.Pct ?? 0m, false,
                sector is null ? null : $"行业涨跌幅 {sector.Pct:+0.00;-0.00}%"),
            new($"stock:{code}", name, 1, 100m, true, null)
        };

        var edges = new List<TopologyEdgeDto>
        {
            new($"industry:{industry}", $"stock:{code}", "所属", "neutral")
        };

        foreach (var peer in peers.Where(peer => peer.Code != code).Take(MaxTopologyNodes - 2))
        {
            nodes.Add(new TopologyNodeDto(
                $"peer:{peer.Code}",
                Truncate(peer.Name, 10),
                2,
                peer.Pct,
                false,
                $"{peer.Code} · 涨跌 {peer.Pct:+0.00;-0.00}%"));
            edges.Add(new TopologyEdgeDto(
                $"industry:{industry}",
                $"peer:{peer.Code}",
                null,
                peer.Pct >= 0 ? "up" : "down"));
        }

        return new TopologyDto(
            "industry",
            "行业拓扑",
            "它在行业里的位置：所属行业、同业与各自涨跌（边的红绿表示同业当日涨跌方向）。",
            ["行业", "本公司", "同业"],
            nodes,
            edges,
            true,
            null);
    }

    /// <summary>事件拓扑：事件类型 → 公司。</summary>
    private static TopologyDto BuildEventTopology(
        string code,
        string name,
        IReadOnlyList<EventItemDto> events)
    {
        if (events.Count == 0)
        {
            return Unavailable("event", "事件拓扑", "哪些事件在影响它。", "暂无事件数据。");
        }

        var nodes = new List<TopologyNodeDto>
        {
            new($"stock:{code}", name, 0, 100m, true, null)
        };

        var edges = new List<TopologyEdgeDto>();
        var categories = new List<string> { "本公司" };
        var index = 1;

        foreach (var group in events.GroupBy(item => item.Type).OrderByDescending(group => group.Count()))
        {
            var list = group.ToList();
            var up = list.Count(item => item.Tone == "up");
            var down = list.Count(item => item.Tone == "down");
            var tone = up > down ? "up" : down > up ? "down" : "neutral";

            var id = $"event:{group.Key}";
            nodes.Add(new TopologyNodeDto(
                id,
                $"{list[0].TypeName}（{list.Count}）",
                1,
                list.Count * 10m,
                false,
                $"偏积极 {up} · 偏消极 {down}"));

            categories.Add(list[0].TypeName);
            edges.Add(new TopologyEdgeDto(id, $"stock:{code}", $"{list.Count} 条", tone));
            index++;
        }

        return new TopologyDto(
            "event",
            "事件拓扑",
            "哪些事件在影响它：事件类型聚合，边的颜色表示该类型的整体倾向（积极 / 消极 / 中性）。",
            ["本公司", "事件类型"],
            nodes,
            edges,
            true,
            null);
    }

    /// <summary>交易对手拓扑：大宗交易买卖方 → 公司。</summary>
    private static TopologyDto BuildCounterpartyTopology(
        string code,
        string name,
        IReadOnlyList<BlockTrade> blockTrades,
        IReadOnlyList<BillboardRecord> billboards)
    {
        if (blockTrades.Count == 0 && billboards.Count == 0)
        {
            return Unavailable(
                "counterparty",
                "交易对手拓扑",
                "谁在跟它做大宗交易：买卖营业部 / 机构与成交金额。",
                "近期没有大宗交易与龙虎榜记录，无法画交易对手关系。");
        }

        var nodes = new List<TopologyNodeDto>
        {
            new($"stock:{code}", name, 0, 100m, true, null)
        };

        var edges = new List<TopologyEdgeDto>();

        // 按对手方聚合，避免同一营业部出现几十个节点
        var counterparties = blockTrades
            .SelectMany(block => new[] { (Name: block.BuyerName, Amount: block.DealAmount ?? 0m, IsBuyer: true),
                                         (Name: block.SellerName, Amount: block.DealAmount ?? 0m, IsBuyer: false) })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Name!)
            .Select(group => (Name: group.Key, Amount: group.Sum(item => item.Amount)))
            .OrderByDescending(item => item.Amount)
            .Take(MaxTopologyNodes - 1)
            .ToList();

        foreach (var counterparty in counterparties)
        {
            var id = $"cp:{counterparty.Name}";
            nodes.Add(new TopologyNodeDto(
                id,
                Truncate(counterparty.Name, 14),
                1,
                counterparty.Amount / 100_000_000m,
                false,
                $"成交 {Display.ToYi(counterparty.Amount):F2} 亿元"));

            edges.Add(new TopologyEdgeDto(id, $"stock:{code}", $"{Display.ToYi(counterparty.Amount):F2} 亿", "neutral"));
        }

        return new TopologyDto(
            "counterparty",
            "交易对手拓扑",
            "谁在跟它做大宗交易：按对手方聚合成交金额（同一营业部只出现一个节点）。",
            ["本公司", "交易对手"],
            nodes,
            edges,
            true,
            null);
    }

    private static TopologyDto Unavailable(string key, string name, string description, string reason) =>
        new(key, name, description, [], [], [], false, reason);

    /* ------------------------------------------------------------------
       工具
       ------------------------------------------------------------------ */

    /// <summary>
    /// 同业（按市值倒序取前若干家）。
    /// </summary>
    /// <param name="Code">证券代码。</param>
    /// <param name="Name">证券名称。</param>
    /// <param name="Pct">当日涨跌幅（百分数）。</param>
    private readonly record struct PeerInfo(string Code, string Name, decimal Pct);

    /// <summary>加载该标的的同业（按市值倒序取前若干家）。</summary>
    /// <remarks>
    /// 节点标签用<b>证券名称</b>而不是代码：图上的可读性来自名字，代码只作为悬浮补充
    /// （实测按代码画图时 19 个节点全是 6 位数字，无法辨认）。
    /// </remarks>
    private async Task<IReadOnlyList<PeerInfo>> LoadIndustryPeersAsync(
        Domain.Entities.Market.Instrument instrument,
        CancellationToken cancellationToken)
    {
        if (instrument.Industry is null)
        {
            return [];
        }

        var allInstruments = await instruments.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var peers = allInstruments
            .Where(item => string.Equals(item.Industry, instrument.Industry, StringComparison.Ordinal))
            .ToList();

        if (peers.Count == 0)
        {
            return [];
        }

        var quotesByCode = await quotes.GetByCodesAsync(
            peers.Select(item => item.Code).ToList(),
            cancellationToken).ConfigureAwait(false);

        var nameByCode = peers.ToDictionary(item => item.Code, item => item.Name, StringComparer.Ordinal);

        return quotesByCode.Values
            .OrderByDescending(quote => quote.MarketCap)
            .Take(18)
            .Select(quote => new PeerInfo(
                quote.Code,
                nameByCode.TryGetValue(quote.Code, out var name) ? name : quote.Code,
                quote.Pct))
            .ToList();
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
