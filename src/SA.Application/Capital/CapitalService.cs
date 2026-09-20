using SA.Application.Abstractions;
using SA.Application.Common;
using SA.Contracts.Capital;
using SA.Contracts.Common;
using SA.Domain.Common;
using SA.Domain.Entities.Capital;

namespace SA.Application.Capital;

/// <summary>
/// 资金面与筹码读模型。
/// </summary>
/// <remarks>
/// <para>
/// 口径要点：
/// </para>
/// <list type="number">
/// <item>资金流金额统一换算为亿元，比率保持百分数；主力 = 大单 + 超大单（上游已算好，本地不复算）；</item>
/// <item>龙虎榜的「次日 / 后 5 日 / 后 10 日涨跌幅」由上游回填，因此最近一次上榜可能还没有后续数据，
/// 缺失时返回 null 而不是 0；</item>
/// <item>陆股通持股是<b>季频</b>披露，页面必须标注频率，避免被读成逐日北向资金——本项目的北向净流入
/// 仍是空态（公开接口已不再提供逐日净买入）；</item>
/// <item>筹码分布需要分价位持仓数据，本轮没有可用的公开源，因此不提供该区块（页面显式说明）。</item>
/// </list>
/// </remarks>
public sealed class CapitalService(
    ICapitalStore store,
    IInstrumentStore instruments,
    IOnDemandQueue onDemand)
{
    /// <summary>资金流折线展示的交易日数。</summary>
    public const int FundFlowDays = 20;

    /// <summary>两融趋势展示的交易日数。</summary>
    public const int MarginDays = 30;

    /// <summary>
    /// 组装资金面视图。
    /// </summary>
    public async Task<ServiceResult<CapitalDto>> GetAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var instrument = await instruments.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (instrument is null)
        {
            return ServiceResult<CapitalDto>.Fail(ErrorCode.NotFound, $"未找到证券代码 {code}");
        }

        var flows = await store.GetFundFlowAsync(code, FundFlowDays, cancellationToken).ConfigureAwait(false);
        var billboards = await store.GetBillboardsAsync(code, 20, cancellationToken).ConfigureAwait(false);
        var blocks = await store.GetBlockTradesAsync(code, 20, cancellationToken).ConfigureAwait(false);
        var margins = await store.GetMarginDetailsAsync(code, MarginDays, cancellationToken).ConfigureAwait(false);
        var northbound = await store.GetNorthboundAsync(code, 8, cancellationToken).ConfigureAwait(false);

        if (flows.Count == 0 && margins.Count == 0 && billboards.Count == 0)
        {
            onDemand.TryEnqueue(code);
            return ServiceResult<CapitalDto>.Fail(ErrorCode.DataNotReady, "资金面数据正在采集，请稍后重试");
        }

        var lastUpdated = await store.GetLastUpdatedAtAsync(code, cancellationToken).ConfigureAwait(false);

        return ServiceResult<CapitalDto>.Success(new CapitalDto(
            Code: code,
            Name: instrument.Name,
            AsOf: lastUpdated is null ? null : SaTime.Format(lastUpdated.Value),
            FundFlow: flows.Select(ToFlowPoint).ToList(),
            Summary: Summarize(flows.TakeLast(5).ToList()),
            Summary20: Summarize(flows.TakeLast(20).ToList()),
            Billboards: billboards.Select(ToBillboard).ToList(),
            BlockTrades: blocks.Select(ToBlockTrade).ToList(),
            Margins: margins.Select(ToMargin).ToList(),
            Northbound: northbound.Select(ToNorthbound).ToList(),
            Insights: BuildInsights(flows, margins, northbound, billboards),
            Notes:
            [
                "口径：主力资金 = 大单 + 超大单（上游已按此计算，本地不复算）；金额统一为亿元。",
                "龙虎榜的「次日 / 后 5 日 / 后 10 日」涨跌幅由上游回填，最近一次上榜可能尚无后续数据，此时显示「—」。",
                "陆股通持股为季度披露（如「2026二季末」），只能反映该季度的整体增减，不能当作逐日北向净买入。",
                "北向资金逐日净买入自 2024 年起不再公开披露，本项目不提供该数值，也不以估算替代。",
                "筹码分布（分价位持仓）需要分价数据，本轮没有可用的公开源，因此不提供该区块。",
                "数据来源：东方财富公开接口（个股资金流 / 龙虎榜 / 大宗交易 / 两融明细 / 陆股通持股）。"
            ]));
    }

    /// <summary>
    /// 汇总一段区间的资金流。
    /// </summary>
    private static FundFlowSummaryDto? Summarize(IReadOnlyList<FundFlowDaily> rows)
    {
        if (rows.Count == 0)
        {
            return null;
        }

        var latest = rows[^1];
        return new FundFlowSummaryDto(
            // 实际样本数而不是请求的窗口长度：只有一天数据时说「近 20 日」会误导
            Days: rows.Count,
            MainNet: Display.ToYi(rows.Sum(row => row.MainNet)),
            InflowDays: rows.Count(row => row.MainNet > 0),
            OutflowDays: rows.Count(row => row.MainNet < 0),
            LatestMainNet: Display.ToYi(latest.MainNet),
            LatestMainRatio: Display.Round(latest.MainRatio));
    }

    private static FundFlowPointDto ToFlowPoint(FundFlowDaily row) =>
        new(
            Date: SaTime.Format(row.Date),
            MainNet: Display.ToYi(row.MainNet),
            SuperLargeNet: Display.ToYi(row.SuperLargeNet),
            LargeNet: Display.ToYi(row.LargeNet),
            MediumNet: Display.ToYi(row.MediumNet),
            SmallNet: Display.ToYi(row.SmallNet),
            MainRatio: Display.Round(row.MainRatio),
            Close: Display.Round(row.Close),
            ChangePercent: Display.Round(row.ChangePercent));

    private static BillboardDto ToBillboard(BillboardRecord row) =>
        new(
            TradeDate: SaTime.Format(row.TradeDate),
            Reason: row.Reason,
            Explain: row.Explain,
            Close: Display.Round(row.Close),
            ChangePercent: Display.Round(row.ChangePercent),
            TurnoverRate: Display.Round(row.TurnoverRate),
            NetAmount: ToYi(row.NetAmount),
            BuyAmount: ToYi(row.BuyAmount),
            SellAmount: ToYi(row.SellAmount),
            Next1Change: Display.Round(row.Next1Change),
            Next5Change: Display.Round(row.Next5Change),
            Next10Change: Display.Round(row.Next10Change));

    private static BlockTradeDto ToBlockTrade(BlockTrade row) =>
        new(
            TradeDate: SaTime.Format(row.TradeDate),
            DealPrice: Display.Round(row.DealPrice),
            PremiumRatio: Display.Round(row.PremiumRatio),
            // 股 → 万股
            DealVolume: row.DealVolume is null ? null : Display.Round(row.DealVolume.Value / 10_000m, 2),
            DealAmount: ToYi(row.DealAmount),
            BuyerName: row.BuyerName,
            SellerName: row.SellerName,
            Close: Display.Round(row.Close));

    private static MarginDetailDto ToMargin(MarginDetail row) =>
        new(
            Date: SaTime.Format(row.Date),
            FinanceBalance: ToYi(row.FinanceBalance),
            FinanceBuy: ToYi(row.FinanceBuy),
            FinanceNetBuy: ToYi(row.FinanceNetBuy),
            LoanBalance: ToYi(row.LoanBalance),
            TotalBalance: ToYi(row.TotalBalance),
            FinanceBalanceRatio: Display.Round(row.FinanceBalanceRatio),
            Close: Display.Round(row.Close));

    private static NorthboundDto ToNorthbound(NorthboundHolding row) =>
        new(
            HoldDate: SaTime.Format(row.HoldDate),
            DateType: row.DateType,
            // 股 → 万股
            HoldShares: row.HoldShares is null ? null : Display.Round(row.HoldShares.Value / 10_000m, 2),
            AddShares: row.AddShares is null ? null : Display.Round(row.AddShares.Value / 10_000m, 2),
            AddSharesAmp: Display.Round(row.AddSharesAmp),
            HoldMarketCap: ToYi(row.HoldMarketCap),
            FreeSharesRatio: Display.Round(row.FreeSharesRatio),
            TotalSharesRatio: Display.Round(row.TotalSharesRatio),
            OrgQuantity: row.OrgQuantity);

    private static decimal? ToYi(decimal? yuan) => yuan is null ? null : Display.ToYi(yuan.Value);

    /// <summary>
    /// 资金面结论。全部由可复算规则给出。
    /// </summary>
    private static List<string> BuildInsights(
        IReadOnlyList<FundFlowDaily> flows,
        IReadOnlyList<MarginDetail> margins,
        IReadOnlyList<NorthboundHolding> northbound,
        IReadOnlyList<BillboardRecord> billboards)
    {
        var insights = new List<string>();

        if (flows.Count >= 5)
        {
            var recent = flows.TakeLast(5).ToList();
            var total = recent.Sum(row => row.MainNet);
            var days = recent.Count(row => row.MainNet > 0);
            insights.Add(total >= 0
                ? $"近 5 日主力净流入 {Display.ToYi(total)} 亿（{days}/5 天流入）"
                : $"近 5 日主力净流出 {Math.Abs(Display.ToYi(total))} 亿（{days}/5 天流入）");
        }

        // 连续同向：只在三天以上时提示，两天不足以称「连续」
        if (flows.Count >= 3)
        {
            var last3 = flows.TakeLast(3).ToList();
            if (last3.All(row => row.MainNet < 0))
            {
                insights.Add("主力连续 3 日净流出");
            }
            else if (last3.All(row => row.MainNet > 0))
            {
                insights.Add("主力连续 3 日净流入");
            }
        }

        if (margins.Count >= 2)
        {
            var latest = margins[^1];
            var previous = margins[^2];
            if (latest.FinanceBalance is { } current && previous.FinanceBalance is { } prior && prior > 0)
            {
                var change = (current / prior - 1) * 100m;
                insights.Add($"融资余额较上日 {(change >= 0 ? "+" : string.Empty)}{change:F2}%");
            }
        }

        if (northbound.Count > 0 && northbound[0].AddShares is { } addShares)
        {
            var amp = northbound[0].AddSharesAmp;
            var period = northbound[0].DateType ?? SaTime.Format(northbound[0].HoldDate);
            insights.Add(addShares >= 0
                ? $"陆股通 {period} 增持 {Display.Round(addShares / 10_000m, 2)} 万股{(amp is null ? string.Empty : $"（+{amp:F2}%）")}"
                : $"陆股通 {period} 减持 {Display.Round(Math.Abs(addShares) / 10_000m, 2)} 万股{(amp is null ? string.Empty : $"（{amp:F2}%）")}");
        }

        if (billboards.Count > 0)
        {
            var latest = billboards[0];
            insights.Add($"最近上榜 {SaTime.Format(latest.TradeDate)}（{latest.Reason ?? "原因未标注"}）");
        }

        return insights;
    }
}
