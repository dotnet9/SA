namespace SA.Contracts.Rating;

/// <summary>
/// 一档评级的家数。
/// </summary>
/// <param name="Level">档位名（买入 / 增持 / 中性 / 减持 / 卖出）。</param>
/// <param name="Count">家数。</param>
/// <param name="Tone">色调（up / neutral / down）。</param>
public sealed record RatingBucketDto(string Level, int Count, string Tone);

/// <summary>
/// 一个预测年度的 EPS。
/// </summary>
/// <param name="Year">年度。</param>
/// <param name="Eps">每股收益（元）。</param>
/// <param name="IsActual">是否为已实现（上游标记为 A）；false 表示预测值。</param>
/// <param name="GrowthVsPrevious">相对上一个有值年度的增速（百分数）；无可比上一年度时为 null。</param>
public sealed record RatingYearDto(int Year, decimal Eps, bool IsActual, decimal? GrowthVsPrevious);

/// <summary>
/// 机构评级与预测（<c>GET /api/stocks/{code}/rating</c>）。
/// </summary>
/// <param name="Code">证券代码。</param>
/// <param name="Name">证券名称。</param>
/// <param name="AsOf">数据时间。</param>
/// <param name="OrgNum">给出评级的机构总数。</param>
/// <param name="Buckets">评级分布。</param>
/// <param name="ConsensusLevel">综合倾向文案，如「买入为主」「中性偏多」。</param>
/// <param name="BullishRatio">看多家数（买入 + 增持）占比（百分数）。</param>
/// <param name="AimPriceMin">目标价下限（元）。</param>
/// <param name="AimPriceMax">目标价上限（元）。</param>
/// <param name="CurrentPrice">现价（元）。</param>
/// <param name="UpsideMin">相对目标价下限的空间（百分数）。</param>
/// <param name="UpsideMax">相对目标价上限的空间（百分数）。</param>
/// <param name="Years">EPS 预测年度序列（含实际值）。</param>
/// <param name="Insights">结论。</param>
/// <param name="Notes">口径说明。</param>
public sealed record RatingDto(
    string Code,
    string Name,
    string? AsOf,
    int OrgNum,
    IReadOnlyList<RatingBucketDto> Buckets,
    string ConsensusLevel,
    decimal? BullishRatio,
    decimal? AimPriceMin,
    decimal? AimPriceMax,
    decimal? CurrentPrice,
    decimal? UpsideMin,
    decimal? UpsideMax,
    IReadOnlyList<RatingYearDto> Years,
    IReadOnlyList<string> Insights,
    IReadOnlyList<string> Notes);
