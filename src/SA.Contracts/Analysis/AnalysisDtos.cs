namespace SA.Contracts.Analysis;

/// <summary>
/// 景气度打分的一个构成项。
/// </summary>
/// <param name="Name">项名。</param>
/// <param name="Value">实际值（已格式化）。</param>
/// <param name="Score">该项得分（0–100）。</param>
/// <param name="Weight">权重（百分数，各项合计 100）。</param>
/// <param name="Weighted">加权贡献（得分 × 权重 / 100）。</param>
/// <param name="Note">口径说明（写清阈值，便于自行核对）。</param>
public sealed record ProsperityFactorDto(
    string Name,
    string Value,
    decimal Score,
    decimal Weight,
    decimal Weighted,
    string Note);

/// <summary>
/// 行业景气度。
/// </summary>
/// <param name="Code">板块码。</param>
/// <param name="Name">行业名。</param>
/// <param name="Score">综合景气分（0–100）。</param>
/// <param name="Grade">分档：高景气 / 偏暖 / 中性 / 偏冷。</param>
/// <param name="Factors">构成项与权重。</param>
/// <param name="Bandwidth">传导带宽（行业指数与基准的相关性，0–1）；样本不足时为 null。</param>
/// <param name="RelativeStrength">近 20 日相对基准的超额收益（百分数）；样本不足时为 null。</param>
/// <param name="MemberCount">参与统计的成分股数。</param>
/// <param name="Samples">行业指数日线样本数。</param>
public sealed record ProsperityDto(
    string Code,
    string Name,
    int Score,
    string Grade,
    IReadOnlyList<ProsperityFactorDto> Factors,
    decimal? Bandwidth,
    decimal? RelativeStrength,
    int MemberCount,
    int Samples);

/// <summary>
/// 行业景气度排行。
/// </summary>
/// <param name="Items">按分数倒序的行业。</param>
/// <param name="AsOf">行情口径日。</param>
/// <param name="Notes">口径说明。</param>
public sealed record ProsperityRankDto(
    IReadOnlyList<ProsperityDto> Items,
    string? AsOf,
    IReadOnlyList<string> Notes);

/// <summary>
/// 因果链的一个环节。
/// </summary>
/// <param name="Order">序号（1 起）。</param>
/// <param name="Stage">阶段名，如「事件」「行业反应」「个股反应」「当前状态」。</param>
/// <param name="Title">标题。</param>
/// <param name="Evidence">证据（可复算的数字）。</param>
/// <param name="Tone">色调：up / down / neutral。</param>
/// <param name="Confidence">置信度（0–100）：由样本量与该环节的可验证程度决定。</param>
/// <param name="ConfidenceNote">置信度的判定依据。</param>
public sealed record CausalLinkDto(
    int Order,
    string Stage,
    string Title,
    string Evidence,
    string Tone,
    int Confidence,
    string ConfidenceNote);

/// <summary>
/// 传导带宽（个股对行业与基准的敏感度）。
/// </summary>
/// <param name="IndustryCode">行业板块码。</param>
/// <param name="IndustryName">行业名。</param>
/// <param name="IndustryCorrelation">与行业指数的日收益相关性（0–1）；样本不足为 null。</param>
/// <param name="BenchmarkCorrelation">与基准指数的日收益相关性。</param>
/// <param name="Beta">对行业指数的贝塔（行业每涨 1%，个股平均涨多少）。</param>
/// <param name="Samples">样本天数。</param>
/// <param name="Bandwidth">传导带宽：0–1，越大表示个股越跟随行业。</param>
/// <param name="Note">口径说明。</param>
public sealed record TransmissionBandwidthDto(
    string? IndustryCode,
    string? IndustryName,
    decimal? IndustryCorrelation,
    decimal? BenchmarkCorrelation,
    decimal? Beta,
    int Samples,
    decimal? Bandwidth,
    string Note);

/// <summary>
/// 因果链与传导带宽（<c>GET /api/stocks/{code}/causal</c>）。
/// </summary>
/// <param name="Code">证券代码。</param>
/// <param name="Name">证券名称。</param>
/// <param name="Industry">所属行业。</param>
/// <param name="AsOf">行情口径日。</param>
/// <param name="Links">因果链环节。</param>
/// <param name="Bandwidth">传导带宽。</param>
/// <param name="Insights">结论。</param>
/// <param name="Notes">口径说明。</param>
public sealed record CausalChainDto(
    string Code,
    string Name,
    string? Industry,
    string? AsOf,
    IReadOnlyList<CausalLinkDto> Links,
    TransmissionBandwidthDto Bandwidth,
    IReadOnlyList<string> Insights,
    IReadOnlyList<string> Notes);
