using SA.Application.Abstractions;
using SA.Application.Common;
using SA.Application.Market;
using SA.Contracts.Analysis;
using SA.Contracts.Common;
using SA.Domain.Analysis;
using SA.Domain.Common;

namespace SA.Application.Analysis;

/// <summary>
/// 行业景气度打分。
/// </summary>
/// <remarks>
/// <para>
/// 采用<b>规则引擎</b>而不是模型推断（实施计划 §2 决策 13）：每一项都有明确阈值与权重，
/// 返回体里同时给出实际值、得分、权重与口径说明，用户可以逐项核对分数是怎么来的。
/// </para>
/// <para>
/// 五个维度与权重：资金（30）、涨跌家数（25）、相对强弱（20）、估值位置（15）、传导带宽（10）。
/// 权重不是「最优参数」，而是「先让最当日可观测的资金与广度占主导」的取舍；
/// 调权重会改变分数但不改变口径，因此权重也写在返回体里。
/// </para>
/// <para>
/// 任一维度样本不足时该项<b>按中性 50 分计入并注明</b>，而不是把它当 0 分：
/// 数据缺失不应该被解读成「该行业很差」。
/// </para>
/// </remarks>
public sealed class ProsperityService(
    MarketSnapshotCache cache,
    IQuoteSnapshotStore quotes,
    IInstrumentStore instruments,
    ISectorStore sectors,
    IDailyHistoryStore daily,
    IIndexStore indices)
{
    /// <summary>相对强弱与带宽的回看天数。</summary>
    private const int LookbackDays = 60;

    /// <summary>带宽所需的最少样本（约 3 个月）。</summary>
    private const int MinBandwidthSamples = 40;

    /// <summary>
    /// 计算全部行业的景气度并按分数倒序返回。
    /// </summary>
    /// <param name="take">返回条数上限（0 表示全部）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<ServiceResult<ProsperityRankDto>> GetRankAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var snapshot = cache.Current;
        if (snapshot.Rows.Count == 0)
        {
            return ServiceResult<ProsperityRankDto>.Fail(ErrorCode.DataNotReady, "全市场快照正在采集，请稍后重试");
        }

        var sectorsAll = await sectors.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (sectorsAll.Count == 0)
        {
            return ServiceResult<ProsperityRankDto>.Fail(ErrorCode.DataNotReady, "行业板块快照正在采集，请稍后重试");
        }

        // 成分股按行业分组：一次遍历即可，避免每个行业都扫一遍全市场
        var membersByIndustry = new Dictionary<string, List<Domain.Entities.Market.QuoteSnapshot>>(StringComparer.Ordinal);
        foreach (var row in snapshot.Rows)
        {
            if (!snapshot.Instruments.TryGetValue(row.Code, out var instrument) || instrument.Industry is null)
            {
                continue;
            }

            if (!membersByIndustry.TryGetValue(instrument.Industry, out var list))
            {
                list = [];
                membersByIndustry[instrument.Industry] = list;
            }

            list.Add(row);
        }

        var benchmark = await LoadBenchmarkAsync(cancellationToken).ConfigureAwait(false);

        // 行业估值分位需要全行业的中位 PE：先算一遍，供「估值位置」维度使用
        var medianPeByIndustry = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (name, members) in membersByIndustry)
        {
            var pes = members.Where(row => row.PeTtm > 0).Select(row => row.PeTtm).OrderBy(value => value).ToList();
            if (Indicators.Median(pes) is { } median)
            {
                medianPeByIndustry[name] = median;
            }
        }

        var allPeMedians = medianPeByIndustry.Values.OrderBy(value => value).ToList();

        var items = new List<ProsperityDto>(sectorsAll.Count);
        foreach (var sector in sectorsAll)
        {
            membersByIndustry.TryGetValue(sector.Name, out var members);
            members ??= [];

            var prosperity = await BuildAsync(
                sector, members, benchmark, medianPeByIndustry, allPeMedians, snapshot, cancellationToken)
                .ConfigureAwait(false);

            items.Add(prosperity);
        }

        var ordered = items.OrderByDescending(item => item.Score)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToList();

        if (take > 0)
        {
            ordered = ordered.Take(take).ToList();
        }

        return ServiceResult<ProsperityRankDto>.Success(new ProsperityRankDto(
            Items: ordered,
            AsOf: snapshot.AsOf == DateOnly.MinValue ? null : SaTime.Format(snapshot.AsOf),
            Notes:
            [
                "口径：规则引擎打分（非模型推断）。五个维度与权重——资金 30、涨跌家数 25、相对强弱 20、估值位置 15、传导带宽 10；"
                + "权重与得分都在返回体里，可逐项核对。",
                "样本不足的维度按中性 50 分计入并注明，不作为 0 分——数据缺失不等于行业变差。",
                "相对强弱与传导带宽基于行业指数日线（90.BKxxxx）与基准（沪深300）日线计算，回看 60 个交易日。",
                "涨跌家数与估值位置由同一时点的全市场快照横截面算出；估值分位为「该行业 PE 中位数在全部行业中的位置」。",
                "数据来源：东方财富公开接口（行业板块 / 行业指数日线 / 全市场快照）。"
            ]));
    }

    /// <summary>单个行业的景气度（个股页可用它展示「所属行业的景气度」）。</summary>
    public async Task<ProsperityDto?> FindAsync(string industryName, CancellationToken cancellationToken = default)
    {
        var rank = await GetRankAsync(0, cancellationToken).ConfigureAwait(false);
        return rank.Ok && rank.Value is not null
            ? rank.Value.Items.FirstOrDefault(item => item.Name == industryName)
            : null;
    }

    /// <summary>
    /// 组装单行业评分。
    /// </summary>
    private async Task<ProsperityDto> BuildAsync(
        Domain.Entities.Market.Sector sector,
        IReadOnlyList<Domain.Entities.Market.QuoteSnapshot> members,
        IReadOnlyList<Domain.History.DailyBar> benchmark,
        IReadOnlyDictionary<string, decimal> medianPeByIndustry,
        IReadOnlyList<decimal> allPeMedians,
        MarketSnapshotCache.Snapshot snapshot,
        CancellationToken cancellationToken)
    {
        var factors = new List<ProsperityFactorDto>();

        /* 1) 资金（权重 30）：主力净流入（亿元）按 ±10 亿映射到 0–100 */
        var flowYi = Display.ToYi(sector.MainNet);
        var flowScore = Clamp01((flowYi + 10m) / 20m) * 100m;
        factors.Add(new ProsperityFactorDto(
            "主力资金", $"{flowYi:+0.00;-0.00} 亿", Round(flowScore), 30m, Round(flowScore * 0.3m),
            "主力净流入按 ±10 亿元线性映射到 0–100（0 亿 = 50 分）"));

        /* 2) 涨跌家数（权重 25）：上涨占比 */
        var up = members.Count(row => row.Pct > 0);
        var down = members.Count(row => row.Pct < 0);
        var breadthScore = up + down > 0 ? (decimal)up / (up + down) * 100m : 50m;
        factors.Add(new ProsperityFactorDto(
            "涨跌家数", $"{up} 涨 / {down} 跌", Round(breadthScore), 25m, Round(breadthScore * 0.25m),
            "按成分股中上涨家数占比计分（平盘不计入分母）"));

        /* 3) 相对强弱（权重 20）：行业指数近 20 日相对基准超额 */
        var sectorBars = await daily.GetLatestAsync(sector.Code, LookbackDays + 5, cancellationToken).ConfigureAwait(false);

        var relativeStrength = ComputeRelativeStrength(sectorBars, benchmark, 20);
        var rsScore = relativeStrength is null
            ? 50m
            : Clamp01((relativeStrength.Value + 10m) / 20m) * 100m;
        factors.Add(new ProsperityFactorDto(
            "相对强弱",
            relativeStrength is null ? "样本不足" : $"{relativeStrength.Value:+0.00;-0.00}%",
            Round(rsScore), 20m, Round(rsScore * 0.2m),
            relativeStrength is null
                ? $"行业指数日线不足（需要约 20 个交易日），按中性 50 分计入"
                : "近 20 日行业指数收益 − 基准收益，按 ±10 个百分点映射到 0–100"));

        /* 4) 估值位置（权重 15）：行业 PE 中位数在全部行业中的分位（越低越好） */
        decimal? pePercentile = null;
        if (medianPeByIndustry.TryGetValue(sector.Name, out var medianPe) && allPeMedians.Count >= 5)
        {
            pePercentile = DistributionRank(allPeMedians, medianPe);
        }

        var valuationScore = pePercentile is null ? 50m : 100m - pePercentile.Value;
        factors.Add(new ProsperityFactorDto(
            "估值位置",
            pePercentile is null ? "样本不足" : $"行业分位 {pePercentile:F0}%",
            Round(valuationScore), 15m, Round(valuationScore * 0.15m),
            pePercentile is null
                ? "同口径行业中位 PE 样本不足，按中性 50 分计入"
                : "PE 中位数的行业分位越低得分越高（同行比较下更便宜）"));

        /* 5) 传导带宽（权重 10）：行业与基准的相关性越高，行业信号越可信 */
        var bandwidth = ComputeCorrelation(
            sectorBars.Select(bar => bar.Close).ToList(),
            benchmark.Select(bar => bar.Close).ToList(),
            LookbackDays);

        var bandwidthScore = bandwidth is null ? 50m : bandwidth.Value * 100m;
        factors.Add(new ProsperityFactorDto(
            "传导带宽",
            bandwidth is null ? "样本不足" : $"相关性 {bandwidth:F2}",
            Round(bandwidthScore), 10m, Round(bandwidthScore * 0.1m),
            bandwidth is null
                ? $"行业指数与基准的重叠样本不足 {MinBandwidthSamples} 天，按中性 50 分计入"
                : "行业指数与基准的日收益相关性（越接近 1 说明行业走势越能代表市场）"));

        var score = (int)Math.Round(factors.Sum(factor => factor.Weighted));

        return new ProsperityDto(
            Code: sector.Code,
            Name: sector.Name,
            Score: Math.Clamp(score, 0, 100),
            Grade: Grade(score),
            Factors: factors,
            Bandwidth: bandwidth,
            RelativeStrength: relativeStrength,
            MemberCount: members.Count,
            Samples: sectorBars.Count);
    }

    /// <summary>分档：85+ 高景气、70+ 偏暖、45+ 中性、以下偏冷。</summary>
    private static string Grade(decimal score) => score switch
    {
        >= 85m => "高景气",
        >= 70m => "偏暖",
        >= 45m => "中性",
        _ => "偏冷"
    };

    /// <summary>
    /// 相对强弱：行业指数与基准在最后 N 天的区间收益之差（百分点）。
    /// </summary>
    internal static decimal? ComputeRelativeStrength(
        IReadOnlyList<Domain.History.DailyBar> sectorBars,
        IReadOnlyList<Domain.History.DailyBar> benchmarkBars,
        int days)
    {
        if (sectorBars.Count < days || benchmarkBars.Count < days)
        {
            return null;
        }

        var sector = sectorBars.TakeLast(days).Select(bar => bar.Close).ToList();
        var benchmark = benchmarkBars.TakeLast(days).Select(bar => bar.Close).ToList();

        if (sector[0] == 0 || benchmark[0] == 0)
        {
            return null;
        }

        var sectorReturn = (sector[^1] / sector[0] - 1) * 100m;
        var benchmarkReturn = (benchmark[^1] / benchmark[0] - 1) * 100m;

        return Math.Round(sectorReturn - benchmarkReturn, 2);
    }

    /// <summary>
    /// 两条收盘序列的日收益相关系数（皮尔逊）。
    /// </summary>
    /// <remarks>
    /// 用<b>重叠日期</b>对齐而不是简单取尾部：行业指数与基准的交易日可能不同步
    /// （某天行业指数缺失时，按尾部取会把不同日期的收益配成一对，得到无意义的相关系数）。
    /// </remarks>
    internal static decimal? ComputeCorrelation(
        IReadOnlyList<decimal> first,
        IReadOnlyList<decimal> second,
        int window)
    {
        var count = Math.Min(first.Count, second.Count);
        if (count < MinBandwidthSamples)
        {
            return null;
        }

        var take = Math.Min(count, window);
        var a = first.Skip(first.Count - take).ToList();
        var b = second.Skip(second.Count - take).ToList();

        var returnsA = new List<double>(take - 1);
        var returnsB = new List<double>(take - 1);

        for (var i = 1; i < take; i++)
        {
            if (a[i - 1] <= 0 || b[i - 1] <= 0)
            {
                continue;
            }

            returnsA.Add((double)(a[i] / a[i - 1] - 1));
            returnsB.Add((double)(b[i] / b[i - 1] - 1));
        }

        if (returnsA.Count < MinBandwidthSamples - 1)
        {
            return null;
        }

        var meanA = returnsA.Average();
        var meanB = returnsB.Average();

        double covariance = 0, varianceA = 0, varianceB = 0;
        for (var i = 0; i < returnsA.Count; i++)
        {
            var da = returnsA[i] - meanA;
            var db = returnsB[i] - meanB;
            covariance += da * db;
            varianceA += da * da;
            varianceB += db * db;
        }

        if (varianceA <= 0 || varianceB <= 0)
        {
            return null;
        }

        var correlation = covariance / Math.Sqrt(varianceA * varianceB);

        // 取绝对值：我们要的是「跟随程度」，方向由相对强弱表达
        return Math.Round((decimal)Math.Clamp(Math.Abs(correlation), 0, 1), 4);
    }

    /// <summary>某值在升序样本中的分位（百分数）。</summary>
    internal static decimal DistributionRank(IReadOnlyList<decimal> sorted, decimal value) =>
        Indicators.Quantile(sorted, value, minSamples: 1) ?? 50m;

    /// <summary>取基准指数日线（沪深 300）。</summary>
    private async Task<IReadOnlyList<Domain.History.DailyBar>> LoadBenchmarkAsync(CancellationToken cancellationToken)
    {
        var configured = await indices.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var code = configured.FirstOrDefault(index => index.Code == "000300")?.Code ?? "000300";
        return await daily.GetLatestAsync(code, LookbackDays + 30, cancellationToken).ConfigureAwait(false);
    }

    private static decimal Clamp01(decimal value) => Math.Clamp(value, 0m, 1m);

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

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
