namespace SA.Contracts.Industry;

/// <summary>
/// 行业概览（该股所属行业的整体表现）。
/// </summary>
/// <param name="Code">行业板块码（BK 码）。</param>
/// <param name="Name">行业名。</param>
/// <param name="Pct">行业涨跌幅（百分数）。</param>
/// <param name="Flow">主力净流入（亿元）。</param>
/// <param name="UpCount">行业内上涨家数。</param>
/// <param name="DownCount">行业内下跌家数。</param>
/// <param name="MemberCount">行业成分股数（有行情的）。</param>
/// <param name="MedianPct">成分股涨跌幅中位数（百分数），比均值更抗极端值。</param>
/// <param name="MedianPe">成分股 PE(TTM) 中位数。</param>
/// <param name="Leader">领涨股名称（来自板块快照）。</param>
/// <param name="Rank">该行业在全市场行业涨跌幅中的排名（1 为最强）。</param>
/// <param name="TotalIndustries">参与排名的行业总数。</param>
public sealed record IndustryOverviewDto(
    string? Code,
    string Name,
    decimal Pct,
    decimal Flow,
    int UpCount,
    int DownCount,
    int MemberCount,
    decimal? MedianPct,
    decimal? MedianPe,
    string? Leader,
    int? Rank,
    int TotalIndustries);

/// <summary>
/// 同行业个股一行。
/// </summary>
/// <param name="Code">代码。</param>
/// <param name="Name">名称。</param>
/// <param name="Price">最新价。</param>
/// <param name="Pct">涨跌幅（百分数）。</param>
/// <param name="Turnover">换手率（百分数）。</param>
/// <param name="PeTtm">PE(TTM)。</param>
/// <param name="Pb">PB。</param>
/// <param name="Cap">总市值（亿元）。</param>
/// <param name="Amount">成交额（亿元）。</param>
/// <param name="Board">板块。</param>
/// <param name="IsSt">是否 ST。</param>
/// <param name="IsSelf">是否为当前查看的标的。</param>
public sealed record PeerRowDto(
    string Code,
    string Name,
    decimal Price,
    decimal Pct,
    decimal Turnover,
    decimal? PeTtm,
    decimal? Pb,
    decimal Cap,
    decimal Amount,
    string Board,
    bool IsSt,
    bool IsSelf);

/// <summary>
/// 该股在行业中的相对位置。
/// </summary>
/// <param name="PctRank">涨跌幅在行业内的排名（1 为最高）。</param>
/// <param name="PctTotal">行业可比标的数。</param>
/// <param name="CapRank">市值在行业内的排名（1 为最大）。</param>
/// <param name="CapPercentile">市值分位（百分数，越大表示在行业内越大）。</param>
/// <param name="PeRank">PE 在行业内的排名（1 为最高）；该股 PE 缺失时为 null。</param>
/// <param name="PePercentile">PE 分位（百分数）；该股 PE 缺失时为 null。</param>
/// <param name="PeVsMedian">PE 相对行业中位数的偏离（百分数）。</param>
/// <param name="PctVsMedian">涨跌幅相对行业中位数的偏离（百分点）。</param>
public sealed record IndustryPositionDto(
    int? PctRank,
    int PctTotal,
    int? CapRank,
    decimal? CapPercentile,
    int? PeRank,
    decimal? PePercentile,
    decimal? PeVsMedian,
    decimal? PctVsMedian);

/// <summary>
/// 行业与同业对比（<c>GET /api/stocks/{code}/industry</c>）。
/// </summary>
/// <param name="Code">证券代码。</param>
/// <param name="Name">证券名称。</param>
/// <param name="Industry">所属行业（东财行业）。</param>
/// <param name="AsOf">行情口径日。</param>
/// <param name="Overview">行业概览；行业快照缺失时为 null。</param>
/// <param name="Position">行业内相对位置。</param>
/// <param name="Peers">同业对比（按市值倒序，最多 30 家）。</param>
/// <param name="TopIndustries">全市场行业涨跌幅前 10（用于对比该行业所处的环境）。</param>
/// <param name="Insights">结论。</param>
/// <param name="Notes">口径说明。</param>
public sealed record IndustryComparisonDto(
    string Code,
    string Name,
    string? Industry,
    string? AsOf,
    IndustryOverviewDto? Overview,
    IndustryPositionDto Position,
    IReadOnlyList<PeerRowDto> Peers,
    IReadOnlyList<IndustryOverviewDto> TopIndustries,
    IReadOnlyList<string> Insights,
    IReadOnlyList<string> Notes);
