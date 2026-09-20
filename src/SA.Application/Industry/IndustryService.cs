using SA.Application.Abstractions;
using SA.Application.Common;
using SA.Application.Market;
using SA.Contracts.Common;
using SA.Contracts.Industry;
using SA.Domain.Analysis;
using SA.Domain.Common;
using SA.Domain.Entities.Market;

namespace SA.Application.Industry;

/// <summary>
/// 行业与同业对比读模型。
/// </summary>
/// <remarks>
/// <para>
/// <b>本模块不引入新的采集源</b>：全部基于已有的全市场个股快照与行业板块快照计算。
/// 这样做的理由是所需口径（同业横截面、行业排名、行业内分位）本来就只能从「全市场同一时点」
/// 的数据算出来，而这两张表恰好就是全市场同一时点的快照。
/// </para>
/// <para>
/// 三个统计口径的选择：
/// </para>
/// <list type="number">
/// <item>用<b>中位数</b>而不是均值：行业内常有极端值（次新股、ST），均值会被拉偏；</item>
/// <item>PE 为负或为 0 的标的<b>不参与</b>估值统计（亏损股的 PE 没有意义，混进去会把中位数拉低）；</item>
/// <item>行业分类为<b>东财行业</b>（需求规格里的申万一级无公开直取源，属已登记的口径偏差）。</item>
/// </list>
/// </remarks>
public sealed class IndustryService(
    MarketSnapshotCache cache,
    ISectorStore sectors,
    IInstrumentStore instruments,
    IQuoteSnapshotStore quotes)
{
    /// <summary>同业对比上限（过多会变成全市场列表，失去「同业」的意义）。</summary>
    public const int PeerLimit = 30;

    /// <summary>行业排行展示条数。</summary>
    private const int TopIndustryLimit = 10;

    /// <summary>
    /// 组装行业对比视图。
    /// </summary>
    public async Task<ServiceResult<IndustryComparisonDto>> GetAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var snapshot = cache.Current;
        if (!snapshot.Instruments.TryGetValue(code, out var instrument))
        {
            return ServiceResult<IndustryComparisonDto>.Fail(ErrorCode.NotFound, $"未找到证券代码 {code}");
        }

        var industry = instrument.Industry;
        var sectorsAll = await sectors.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // 行业快照按涨跌幅排序的结果，直接用于「全市场行业排行」
        var sectorRanked = sectorsAll
            .OrderByDescending(sector => sector.Pct)
            .ToList();

        var sector = industry is null
            ? null
            : sectorsAll.FirstOrDefault(row => string.Equals(row.Name, industry, StringComparison.Ordinal));

        var sectorDto = sector is null
            ? null
            : ToOverview(
                sector,
                memberCount: 0,
                medianPct: null,
                medianPe: null,
                rank: sectorRanked.FindIndex(row => row.Code == sector.Code) + 1,
                total: sectorRanked.Count);

        // 行业成分股：以内存快照为准（同一时点、同一口径）
        var members = industry is null
            ? []
            : snapshot.Rows
                .Where(row => snapshot.Instruments.TryGetValue(row.Code, out var item)
                    && string.Equals(item.Industry, industry, StringComparison.Ordinal))
                .Select(row => (Row: row, Instrument: snapshot.Instruments[row.Code]))
                .ToList();

        var memberQuotes = members.Select(member => member.Row).ToList();

        var pcts = memberQuotes.Select(row => row.Pct).OrderBy(value => value).ToList();
        var pes = memberQuotes
            .Where(row => row.PeTtm > 0)
            .Select(row => row.PeTtm)
            .OrderBy(value => value)
            .ToList();

        var medianPct = Indicators.Median(pcts);
        var medianPe = Indicators.Median(pes);

        if (sectorDto is not null)
        {
            sectorDto = sectorDto with
            {
                MemberCount = members.Count,
                MedianPct = medianPct,
                MedianPe = medianPe,
                UpCount = memberQuotes.Count(row => row.Pct > 0),
                DownCount = memberQuotes.Count(row => row.Pct < 0)
            };
        }

        var self = memberQuotes.FirstOrDefault(row => row.Code == code);
        var position = BuildPosition(code, memberQuotes, self);

        var peers = members
            .OrderByDescending(member => member.Row.MarketCap)
            .ThenBy(member => member.Row.Code, StringComparer.Ordinal)
            .Take(PeerLimit)
            .Select(member => ToPeerRow(member.Row, member.Instrument, member.Row.Code == code))
            .ToList();

        return ServiceResult<IndustryComparisonDto>.Success(new IndustryComparisonDto(
            Code: code,
            Name: instrument.Name,
            Industry: industry,
            AsOf: snapshot.AsOf == DateOnly.MinValue ? null : SaTime.Format(snapshot.AsOf),
            Overview: sectorDto,
            Position: position,
            Peers: peers,
            TopIndustries: sectorRanked
                .Take(TopIndustryLimit)
                .Select((row, index) => ToOverview(row, 0, null, null, index + 1, sectorRanked.Count))
                .ToList(),
            Insights: BuildInsights(instrument, sectorDto, position, medianPct, medianPe, self),
            Notes:
            [
                $"口径：行业分类为东财行业；同业对比取有行情的成分股（停牌与退市不进快照），按总市值倒序取前 {PeerLimit} 家。",
                "行业涨跌幅来自行业板块端点（上游直接给出），成分股中位数由本地按同一时点的全市场快照计算。",
                "PE(TTM) 为负或为 0 的标的（亏损股）不参与估值统计，否则会把行业中位数拉低。",
                "分位为「小于等于该值的标的占比」，样本为行业内全部有行情的成分股。",
                "数据来源：东方财富公开接口（全市场快照 + 行业板块），模块本身不新增采集源。"
            ]));
    }

    /// <summary>行业内相对位置。</summary>
    private static IndustryPositionDto BuildPosition(
        string code,
        IReadOnlyList<QuoteSnapshot> members,
        QuoteSnapshot? self)
    {
        var total = members.Count;
        if (self is null || total == 0)
        {
            return new IndustryPositionDto(null, total, null, null, null, null, null, null);
        }

        var byPct = members.OrderByDescending(row => row.Pct).ToList();
        var pctRank = byPct.FindIndex(row => row.Code == code) + 1;

        var byCap = members.OrderByDescending(row => row.MarketCap).ToList();
        var capRank = byCap.FindIndex(row => row.Code == code) + 1;
        var capPercentile = Display.Round((decimal)(total - capRank + 1) / total * 100m, 1);

        // PE 分位只在「该股有正 PE」且「样本里至少有一家正 PE」时给出
        var withPe = members.Where(row => row.PeTtm > 0).OrderBy(row => row.PeTtm).ToList();
        int? peRank = null;
        decimal? pePercentile = null;
        decimal? peVsMedian = null;

        if (self.PeTtm > 0 && withPe.Count > 0)
        {
            peRank = withPe.FindIndex(row => row.Code == code) + 1;
            pePercentile = Display.Round((decimal)peRank / withPe.Count * 100m, 1);

            var median = Indicators.Median(withPe.Select(row => row.PeTtm).OrderBy(value => value).ToList());
            if (median is { } mid && mid > 0)
            {
                peVsMedian = Display.Round((self.PeTtm / mid - 1) * 100m);
            }
        }

        decimal? pctVsMedian = null;
        var medianPct = Indicators.Median(members.Select(row => row.Pct).OrderBy(value => value).ToList());
        if (medianPct is { } medianValue)
        {
            pctVsMedian = Display.Round(self.Pct - medianValue);
        }

        return new IndustryPositionDto(
            PctRank: pctRank > 0 ? pctRank : null,
            PctTotal: total,
            CapRank: capRank > 0 ? capRank : null,
            CapPercentile: capPercentile,
            PeRank: peRank,
            PePercentile: pePercentile,
            PeVsMedian: peVsMedian,
            PctVsMedian: pctVsMedian);
    }

    /// <summary>
    /// 结论。全部由可复算规则给出。
    /// </summary>
    private static List<string> BuildInsights(
        Instrument instrument,
        IndustryOverviewDto? sector,
        IndustryPositionDto position,
        decimal? medianPct,
        decimal? medianPe,
        QuoteSnapshot? self)
    {
        var insights = new List<string>();

        if (sector is not null)
        {
            insights.Add($"行业「{sector.Name}」{sector.Pct:+0.00;-0.00}%");
            if (sector.Rank is { } rank && sector.TotalIndustries > 0)
            {
                insights.Add($"行业涨跌幅排名 {rank}/{sector.TotalIndustries}");
            }
        }

        if (position.PctRank is { } pctRank && position.PctTotal > 0)
        {
            insights.Add($"行业内涨幅排名 {pctRank}/{position.PctTotal}");
        }

        if (position.PctVsMedian is { } vsMedian)
        {
            insights.Add(Math.Abs(vsMedian) < 0.5m
                ? "涨跌幅与行业中位数基本一致"
                : vsMedian > 0
                    ? $"强于行业中位数 {vsMedian:F2} 个百分点"
                    : $"弱于行业中位数 {Math.Abs(vsMedian):F2} 个百分点");
        }

        if (position is { CapRank: 1 })
        {
            // 市值第一时「前 0%」这种说法没有意义，直接说明排名
            insights.Add("市值为行业内最大");
        }
        else if (position.CapPercentile is { } capPercentile)
        {
            insights.Add(capPercentile >= 80
                ? $"市值处于行业前 {Display.Round(100 - capPercentile, 1)}%"
                : $"市值分位 {capPercentile:F1}%");
        }

        if (position.PeVsMedian is { } peVsMedian && medianPe is not null)
        {
            insights.Add(peVsMedian >= 0
                ? $"PE(TTM) 高于行业中位数 {peVsMedian:F2}%（中位数 {medianPe:F2}）"
                : $"PE(TTM) 低于行业中位数 {Math.Abs(peVsMedian):F2}%（中位数 {medianPe:F2}）");
        }

        if (self is null)
        {
            insights.Add("该标的当前无行情（停牌或未采集到快照），因此不参与同业统计");
        }

        return insights;
    }

    private static IndustryOverviewDto ToOverview(
        Sector sector,
        int memberCount,
        decimal? medianPct,
        decimal? medianPe,
        int rank,
        int total) =>
        new(
            Code: sector.Code,
            Name: sector.Name,
            Pct: Display.Round(sector.Pct),
            Flow: Display.ToYi(sector.MainNet),
            UpCount: sector.UpCount,
            DownCount: sector.DownCount,
            MemberCount: memberCount,
            MedianPct: medianPct,
            MedianPe: medianPe,
            Leader: sector.LeaderName,
            Rank: rank > 0 ? rank : null,
            TotalIndustries: total);

    private static PeerRowDto ToPeerRow(QuoteSnapshot row, Instrument instrument, bool isSelf) =>
        new(
            Code: row.Code,
            Name: instrument.Name,
            Price: Display.Round(row.Price),
            Pct: Display.Round(row.Pct),
            Turnover: Display.Round(row.Turnover),
            PeTtm: row.PeTtm > 0 ? Display.Round(row.PeTtm) : null,
            Pb: row.Pb > 0 ? Display.Round(row.Pb) : null,
            Cap: Display.ToYi(row.MarketCap),
            Amount: Display.ToYi(row.Amount),
            Board: instrument.Board,
            IsSt: instrument.IsSt,
            IsSelf: isSelf);

    /// <summary>
    /// 确保内存快照可用（与市场页共用同一份缓存，因此通常是一次进程生命周期内的一次装载）。
    /// </summary>
    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (!cache.IsEmpty)
        {
            return;
        }

        var rows = await quotes.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return;
        }

        var instrumentRows = await instruments.GetAllAsync(cancellationToken).ConfigureAwait(false);
        cache.Replace(rows, instrumentRows.ToDictionary(item => item.Code, StringComparer.Ordinal));
    }
}
