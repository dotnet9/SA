using SA.Application.Abstractions;
using SA.Application.Common;
using SA.Contracts.Common;
using SA.Contracts.Equity;
using SA.Domain.Common;
using SA.Domain.Entities.Equity;

namespace SA.Application.Equity;

/// <summary>
/// 投资与股权结构读模型。
/// </summary>
/// <remarks>
/// 四条口径约定：
/// <list type="number">
/// <item>十大股东与十大流通股东<b>分开返回</b>：两者的报告期与口径都不同，混在一起会让人误以为同一天的数据；</item>
/// <item>持股数量的变动用上游的原文（「不变」「新进」），不自行推断增减——上游已经按调整后口径判定过；</item>
/// <item>股东户数按报告期升序返回，页面直接画趋势（户数下降一般对应筹码集中）；</item>
/// <item>质押数据的单位已按实测核对：股数为万股、市值为万元，接口层统一换算成万股与亿元。</item>
/// </list>
/// <para>
/// 集中度只在「同一报告期的前十大齐全」时才计算，缺行时返回 null 而不是按现有行求和，
/// 避免把 7 个股东的比例当成前十大的比例。
/// </para>
/// </remarks>
public sealed class EquityService(
    IEquityStore store,
    IInstrumentStore instruments,
    IOnDemandQueue onDemand)
{
    /// <summary>股东户数趋势展示的期数上限。</summary>
    public const int HolderCountLimit = 16;

    /// <summary>
    /// 组装股权结构视图。
    /// </summary>
    public async Task<ServiceResult<EquityDto>> GetAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var instrument = await instruments.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (instrument is null)
        {
            return ServiceResult<EquityDto>.Fail(ErrorCode.NotFound, $"未找到证券代码 {code}");
        }

        var holders = await store.GetTopHoldersAsync(code, cancellationToken).ConfigureAwait(false);
        var counts = await store.GetHolderCountsAsync(code, cancellationToken).ConfigureAwait(false);
        var pledge = await store.GetPledgeAsync(code, cancellationToken).ConfigureAwait(false);

        if (holders.Count == 0 && counts.Count == 0)
        {
            onDemand.TryEnqueue(code);
            return ServiceResult<EquityDto>.Fail(ErrorCode.DataNotReady, "股权结构数据正在采集，请稍后重试");
        }

        var periods = holders
            .GroupBy(holder => (holder.EndDate, holder.IsFreeFloat))
            .Select(group => BuildPeriod(group.Key.EndDate, group.Key.IsFreeFloat, group.ToList()))
            .OrderByDescending(period => period.EndDate)
            .ToList();

        var latestEnd = periods.Count > 0 ? periods[0].EndDate : (DateOnly?)null;

        // 取 DTO（引用类型）而不是元组：元组的 FirstOrDefault 会返回「全零元组」而不是「不存在」，
        // 那样就无法区分「没有这一期」与「这一期存在但字段为空」
        var latest = periods
            .Where(period => period.EndDate == latestEnd && !period.IsFreeFloat)
            .Select(period => period.Dto)
            .FirstOrDefault();

        var latestFree = periods
            .Where(period => period.EndDate == latestEnd && period.IsFreeFloat)
            .Select(period => period.Dto)
            .FirstOrDefault();

        // 历史期里剔除最新一期的数据（最新一期单独展示），保留最多 4 期
        var history = periods
            .Where(period => period.EndDate != latestEnd)
            .Take(8)
            .Select(period => period.Dto)
            .ToList();

        var lastUpdated = await store.GetLastUpdatedAtAsync(code, cancellationToken).ConfigureAwait(false);

        return ServiceResult<EquityDto>.Success(new EquityDto(
            Code: code,
            Name: instrument.Name,
            AsOf: lastUpdated is null ? null : SaTime.Format(lastUpdated.Value),
            Latest: latest,
            LatestFreeFloat: latestFree,
            History: history,
            HolderCounts: counts
                .TakeLast(HolderCountLimit)
                .Select(count => new HolderCountDto(
                    EndDate: SaTime.Format(count.EndDate),
                    ReportName: count.ReportName,
                    HolderNum: count.HolderNum,
                    Change: count.HolderNumChange,
                    ChangeRatio: Display.Round(count.HolderNumRatio),
                    AvgHoldNum: Display.Round(count.AvgHoldNum),
                    // 户均市值：元 → 万元（页面按万元展示更直观）
                    AvgMarketCap: count.AvgMarketCap is null ? null : Display.Round(count.AvgMarketCap.Value / 10_000m),
                    // 总股本：股 → 亿股
                    TotalShares: count.TotalShares is null ? null : Display.Round(count.TotalShares.Value / 100_000_000m, 4)))
                .ToList(),
            Pledge: pledge is null
                ? null
                : new PledgeDto(
                    TradeDate: SaTime.Format(pledge.TradeDate),
                    PledgeRatio: Display.Round(pledge.PledgeRatio),
                    PledgeShares: Display.Round(pledge.PledgeSharesWan),
                    PledgeDealNum: pledge.PledgeDealNum,
                    // 质押市值：万元 → 亿元
                    PledgeMarketCap: pledge.PledgeMarketCapWan is null
                        ? null
                        : Display.Round(pledge.PledgeMarketCapWan.Value / 10_000m),
                    Industry: pledge.Industry,
                    Year1Change: Display.Round(pledge.Year1ChangePercent)),
            Insights: BuildInsights(latest, latestFree, counts, pledge),
            Notes:
            [
                "口径：十大股东为全部股份（含限售），十大流通股东只含流通股；两者的报告期可能不同，故分开展示。",
                "持股变动使用上游给出的原文（「不变」「新进」等），上游已按调整后口径判定，本地不另行推断。",
                "股东户数按报告期升序展示：户数下降通常对应筹码集中，但需结合股本变动原因一起看。",
                "质押口径：质押比例为占总股本比例；质押股数为万股，市值已换算为亿元（单位按实测响应核对）。",
                "数据来源：东方财富公开 F10 报表（十大股东 / 十大流通股东 / 股东户数 / 股权质押）。"
            ]));
    }

    /// <summary>一期股东名单（含集中度）。</summary>
    private static (DateOnly EndDate, bool IsFreeFloat, EquityPeriodDto Dto) BuildPeriod(
        DateOnly endDate,
        bool isFreeFloat,
        IReadOnlyList<TopHolder> rows)
    {
        var holders = rows
            .OrderBy(row => row.Rank)
            .Select(row => new EquityHolderDto(
                Rank: row.Rank,
                Name: row.HolderName,
                HoldNum: row.HoldNum,
                HoldRatio: Display.Round(row.HoldRatio),
                FreeHoldRatio: Display.Round(row.FreeHoldRatio),
                Change: row.HoldChange,
                HolderType: row.HolderType,
                SharesType: row.SharesType,
                // 持股市值：元 → 亿元
                MarketCap: row.MarketCap is null ? null : Display.ToYi(row.MarketCap.Value)))
            .ToList();

        // 只在「前十大齐全」时算集中度，缺行时返回 null（不按现有行求和冒充前十大数据）
        var ratios = holders.Select(holder => holder.HoldRatio).ToList();
        decimal? totalRatio = ratios.Count == 10 && ratios.All(ratio => ratio is not null)
            ? Display.Round(ratios.Sum(ratio => ratio!.Value))
            : null;

        var dto = new EquityPeriodDto(
            EndDate: SaTime.Format(endDate),
            NoticeDate: rows.Select(row => row.NoticeDate).FirstOrDefault(date => date is not null) is { } notice
                ? SaTime.Format(notice)
                : null,
            Holders: holders,
            TotalRatio: totalRatio,
            Concentration: totalRatio is null
                ? "前十大不齐全，未计算合计比例"
                : $"{(isFreeFloat ? "前十大流通股东" : "前十大股东")}合计 {totalRatio:F2}%");

        return (endDate, isFreeFloat, dto);
    }

    /// <summary>
    /// 股权结构结论。全部由可复算规则给出。
    /// </summary>
    private static List<string> BuildInsights(
        EquityPeriodDto? latest,
        EquityPeriodDto? latestFree,
        IReadOnlyList<HolderCount> counts,
        PledgeStat? pledge)
    {
        var insights = new List<string>();

        if (latest?.TotalRatio is { } total)
        {
            insights.Add($"前十大股东合计 {total:F2}%");
        }

        if (latestFree?.TotalRatio is { } freeTotal)
        {
            insights.Add($"前十大流通股东合计 {freeTotal:F2}%");
        }

        // 大股东新进 / 退出：只在文案里出现「新进」「退出」时提示，避免自行推断
        if (latest is not null)
        {
            var entered = latest.Holders.Count(holder => holder.Change is not null && holder.Change.Contains("新进", StringComparison.Ordinal));
            if (entered > 0)
            {
                insights.Add($"前十大股东中有 {entered} 家新进");
            }
        }

        // 股东户数：连续两期下降 → 筹码集中；上升 → 分散
        if (counts.Count >= 3)
        {
            var tail = counts.TakeLast(3).ToList();
            if (tail[1].HolderNum < tail[0].HolderNum && tail[2].HolderNum < tail[1].HolderNum)
            {
                insights.Add("股东户数连续两期下降（筹码趋于集中）");
            }
            else if (tail[1].HolderNum > tail[0].HolderNum && tail[2].HolderNum > tail[1].HolderNum)
            {
                insights.Add("股东户数连续两期上升（筹码趋于分散）");
            }
        }

        if (counts.Count >= 2)
        {
            var latestCount = counts[^1];
            if (latestCount.HolderNumRatio is { } ratio)
            {
                insights.Add(ratio >= 0
                    ? $"最新一期股东户数较上期 +{ratio:F2}%"
                    : $"最新一期股东户数较上期 {ratio:F2}%");
            }
        }

        if (pledge?.PledgeRatio is { } pledgeRatio && pledgeRatio > 0)
        {
            var level = pledgeRatio >= 30 ? "偏高" : pledgeRatio >= 10 ? "中等" : "较低";
            insights.Add($"质押比例 {pledgeRatio:F2}%（{level}）");
        }
        else if (pledge is not null)
        {
            insights.Add("无股权质押");
        }

        return insights;
    }
}
