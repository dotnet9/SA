namespace SA.Contracts.Equity;

/// <summary>
/// 股东一条（十大股东 / 十大流通股东）。
/// </summary>
/// <param name="Rank">排名。</param>
/// <param name="Name">股东名称。</param>
/// <param name="HoldNum">持股数量（股）。</param>
/// <param name="HoldRatio">占总股本比例（百分数）。</param>
/// <param name="FreeHoldRatio">占流通股比例（百分数，仅流通口径）。</param>
/// <param name="Change">较上期变动文案（上游原文，如「不变」「新进」）。</param>
/// <param name="HolderType">股东类型。</param>
/// <param name="SharesType">股份类型。</param>
/// <param name="MarketCap">持股市值（亿元）。</param>
public sealed record EquityHolderDto(
    int Rank,
    string Name,
    decimal HoldNum,
    decimal? HoldRatio,
    decimal? FreeHoldRatio,
    string? Change,
    string? HolderType,
    string? SharesType,
    decimal? MarketCap);

/// <summary>
/// 一期股东名单。
/// </summary>
/// <param name="EndDate">报告期。</param>
/// <param name="NoticeDate">公告日期。</param>
/// <param name="Holders">股东列表（按排名）。</param>
/// <param name="TotalRatio">前十大合计持股比例（百分数）。</param>
/// <param name="Concentration">筹码集中度提示文案，如「前十大合计 58.2%」。</param>
public sealed record EquityPeriodDto(
    string EndDate,
    string? NoticeDate,
    IReadOnlyList<EquityHolderDto> Holders,
    decimal? TotalRatio,
    string? Concentration);

/// <summary>
/// 股东户数一期。
/// </summary>
/// <param name="EndDate">报告期。</param>
/// <param name="ReportName">报告期文案，如「2026 2季末」。</param>
/// <param name="HolderNum">股东户数。</param>
/// <param name="Change">较上期变化量（正为增加）。</param>
/// <param name="ChangeRatio">较上期变化比例（百分数）。</param>
/// <param name="AvgHoldNum">户均持股（股）。</param>
/// <param name="AvgMarketCap">户均持股市值（万元）。</param>
/// <param name="TotalShares">总股本（亿股）。</param>
public sealed record HolderCountDto(
    string EndDate,
    string? ReportName,
    int HolderNum,
    int? Change,
    decimal? ChangeRatio,
    decimal? AvgHoldNum,
    decimal? AvgMarketCap,
    decimal? TotalShares);

/// <summary>
/// 股权质押。
/// </summary>
/// <param name="TradeDate">数据日期。</param>
/// <param name="PledgeRatio">质押比例（百分数，占总股本）。</param>
/// <param name="PledgeShares">质押股数（万股）。</param>
/// <param name="PledgeDealNum">质押笔数。</param>
/// <param name="PledgeMarketCap">质押市值（亿元）。</param>
/// <param name="Industry">行业。</param>
/// <param name="Year1Change">近一年涨跌幅（百分数）。</param>
public sealed record PledgeDto(
    string TradeDate,
    decimal? PledgeRatio,
    decimal? PledgeShares,
    int? PledgeDealNum,
    decimal? PledgeMarketCap,
    string? Industry,
    decimal? Year1Change);

/// <summary>
/// 投资与股权结构（<c>GET /api/stocks/{code}/equity</c>）。
/// </summary>
/// <param name="Code">证券代码。</param>
/// <param name="Name">证券名称。</param>
/// <param name="AsOf">数据时间。</param>
/// <param name="Latest">最新报告期的十大股东。</param>
/// <param name="LatestFreeFloat">最新报告期的十大流通股东。</param>
/// <param name="History">历史报告期（不含最新一期）。</param>
/// <param name="HolderCounts">股东户数历史（按报告期升序）。</param>
/// <param name="Pledge">股权质押。</param>
/// <param name="Insights">股权结构结论（由可复算规则给出）。</param>
/// <param name="Notes">口径说明。</param>
public sealed record EquityDto(
    string Code,
    string Name,
    string? AsOf,
    EquityPeriodDto? Latest,
    EquityPeriodDto? LatestFreeFloat,
    IReadOnlyList<EquityPeriodDto> History,
    IReadOnlyList<HolderCountDto> HolderCounts,
    PledgeDto? Pledge,
    IReadOnlyList<string> Insights,
    IReadOnlyList<string> Notes);
