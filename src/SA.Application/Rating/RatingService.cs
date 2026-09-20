using SA.Application.Abstractions;
using SA.Application.Common;
using SA.Contracts.Common;
using SA.Contracts.Rating;
using SA.Domain.Common;

namespace SA.Application.Rating;

/// <summary>
/// 机构评级与盈利预测读模型。
/// </summary>
/// <remarks>
/// <list type="number">
/// <item>评级口径完全沿用上游（哪家算「买入」由上游决定），本地不做加权评分，避免出现第二套标准；</item>
/// <item>目标价空间以<b>现价</b>为基准计算，并明确标注「相对现价」，避免被读成预期收益；</item>
/// <item>EPS 序列区分实际值（上游标记 A）与预测值（E）：把已实现值当预测值展示会误导。</item>
/// </list>
/// </remarks>
public sealed class RatingService(
    IRatingStore store,
    IInstrumentStore instruments,
    IQuoteSnapshotStore quotes,
    IOnDemandQueue onDemand)
{
    /// <summary>
    /// 组装评级视图。
    /// </summary>
    public async Task<ServiceResult<RatingDto>> GetAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var instrument = await instruments.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (instrument is null)
        {
            return ServiceResult<RatingDto>.Fail(ErrorCode.NotFound, $"未找到证券代码 {code}");
        }

        var consensus = await store.GetAsync(code, cancellationToken).ConfigureAwait(false);
        if (consensus is null)
        {
            onDemand.TryEnqueue(code);
            return ServiceResult<RatingDto>.Fail(ErrorCode.DataNotReady, "机构评级数据正在采集，请稍后重试");
        }

        var quote = (await quotes.GetByCodesAsync([code], cancellationToken).ConfigureAwait(false))
            .TryGetValue(code, out var snapshot)
                ? snapshot
                : null;

        var buckets = BuildBuckets(consensus);
        var bullish = consensus.BuyNum + consensus.AddNum;
        decimal? bullishRatio = consensus.RatingOrgNum > 0
            ? Display.Round((decimal)bullish / consensus.RatingOrgNum * 100m, 1)
            : null;

        var current = quote?.Price;
        decimal? upsideMin = null;
        decimal? upsideMax = null;

        if (current is > 0)
        {
            if (consensus.AimPriceMin is { } min)
            {
                upsideMin = Display.Round((min / current.Value - 1) * 100m);
            }

            if (consensus.AimPriceMax is { } max)
            {
                upsideMax = Display.Round((max / current.Value - 1) * 100m);
            }
        }

        var years = BuildYears(consensus);

        return ServiceResult<RatingDto>.Success(new RatingDto(
            Code: code,
            Name: instrument.Name,
            AsOf: consensus.UpdatedAt == default ? null : SaTime.Format(consensus.UpdatedAt),
            OrgNum: consensus.RatingOrgNum,
            Buckets: buckets,
            ConsensusLevel: DescribeConsensus(bullishRatio, consensus.RatingOrgNum),
            BullishRatio: bullishRatio,
            AimPriceMin: Display.Round(consensus.AimPriceMin),
            AimPriceMax: Display.Round(consensus.AimPriceMax),
            CurrentPrice: current is null ? null : Display.Round(current),
            UpsideMin: upsideMin,
            UpsideMax: upsideMax,
            Years: years,
            Insights: BuildInsights(consensus, bullishRatio, upsideMin, upsideMax, years, current),
            Notes:
            [
                "口径：评级档位沿用上游定义（本地不做加权评分，避免出现第二套评级标准）。",
                "目标价空间以现价为基准，表示「相对现价」的距离，不代表预期收益。",
                "EPS 序列区分实际值与预测值：上游用 YEAR_MARK 标记（A 为实际、E 为预测）。",
                "机构覆盖数为 0 或数据缺失时不展示评级结论——没有机构覆盖本身也是一种信息，但不该被渲染成「中性」。",
                "数据来源：东方财富公开接口（机构评级预测）。"
            ]));
    }

    private static List<RatingBucketDto> BuildBuckets(Domain.Entities.Rating.RatingConsensus consensus) =>
    [
        new("买入", consensus.BuyNum, "up"),
        new("增持", consensus.AddNum, "up"),
        new("中性", consensus.NeutralNum ?? 0, "neutral"),
        new("减持", consensus.ReduceNum ?? 0, "down"),
        new("卖出", consensus.SaleNum ?? 0, "down")
    ];

    /// <summary>
    /// EPS 年度序列（含同比增速；只与上一个有值的年度比较）。
    /// </summary>
    private static List<RatingYearDto> BuildYears(Domain.Entities.Rating.RatingConsensus consensus)
    {
        var raw = new (int? Year, decimal? Eps, string? Mark)[]
        {
            (consensus.Year1, consensus.Eps1, consensus.YearMark1),
            (consensus.Year2, consensus.Eps2, consensus.YearMark2),
            (consensus.Year3, consensus.Eps3, consensus.YearMark3),
            (consensus.Year4, consensus.Eps4, consensus.YearMark4)
        };

        var years = new List<RatingYearDto>();
        decimal? previous = null;

        foreach (var (year, eps, mark) in raw)
        {
            if (year is null || eps is null)
            {
                continue;
            }

            decimal? growth = null;
            if (previous is > 0)
            {
                growth = Display.Round((eps.Value / previous.Value - 1) * 100m);
            }

            years.Add(new RatingYearDto(
                Year: year.Value,
                Eps: Display.Round(eps.Value),
                // 上游标记 A 为实际值；其余（E 或缺失）一律按预测处理，宁可按预测展示
                IsActual: string.Equals(mark, "A", StringComparison.OrdinalIgnoreCase),
                GrowthVsPrevious: growth));

            previous = eps;
        }

        return years;
    }

    /// <summary>综合倾向文案。机构数过少时不下结论。</summary>
    private static string DescribeConsensus(decimal? bullishRatio, int orgNum)
    {
        if (orgNum == 0)
        {
            return "暂无机构覆盖";
        }

        if (orgNum < 3)
        {
            return $"覆盖机构较少（{orgNum} 家），不足以判断倾向";
        }

        if (bullishRatio is null)
        {
            return "评级分布缺失";
        }

        return bullishRatio switch
        {
            >= 90m => "一致看多",
            >= 70m => "买入为主",
            >= 50m => "偏多",
            >= 30m => "分歧较大",
            _ => "偏空"
        };
    }

    private static List<string> BuildInsights(
        Domain.Entities.Rating.RatingConsensus consensus,
        decimal? bullishRatio,
        decimal? upsideMin,
        decimal? upsideMax,
        IReadOnlyList<RatingYearDto> years,
        decimal? current)
    {
        var insights = new List<string>();

        if (consensus.RatingOrgNum > 0)
        {
            insights.Add($"{consensus.RatingOrgNum} 家机构给出评级");
        }

        if (bullishRatio is { } ratio)
        {
            insights.Add($"看多占比 {ratio:F1}%（买入 {consensus.BuyNum} + 增持 {consensus.AddNum}）");
        }

        if (upsideMax is { } max)
        {
            insights.Add(max >= 0
                ? $"目标价上限相对现价 +{max:F2}%"
                : $"目标价上限低于现价 {max:F2}%（机构目标价已被现价超越）");
        }

        if (upsideMin is { } min && min < 0 && upsideMax is > 0)
        {
            insights.Add("目标价区间跨越现价，机构内部分歧明显");
        }

        // 预测增速：只看第一个预测年度，且必须是预测值
        var firstForecast = years.FirstOrDefault(year => !year.IsActual && year.GrowthVsPrevious is not null);
        if (firstForecast is not null)
        {
            insights.Add($"{firstForecast.Year} 年预测 EPS 增速 {firstForecast.GrowthVsPrevious:F2}%");
        }

        if (current is null)
        {
            insights.Add("该标的当前无行情，目标价空间无法计算");
        }

        return insights;
    }
}
