namespace SA.Contracts.Market;

/// <summary>
/// 指数卡片。对应原型 <c>design/web/market.html</c> 的指数卡（5 张），
/// 字段名与 <c>data.js</c> 的 <c>indices</c> 保持一致（详细设计 §1.3）。
/// </summary>
/// <param name="Code">指数代码。</param>
/// <param name="Name">指数名称。</param>
/// <param name="Price">最新点位。</param>
/// <param name="Chg">涨跌点数。</param>
/// <param name="Pct">涨跌幅（百分数）。</param>
/// <param name="Amount">成交额（亿元）。</param>
/// <param name="Spark">近 40 个交易日的收盘序列，用于迷你走势；样本不足时为空数组。</param>
/// <param name="AsOf">数据时间（<c>yyyy-MM-dd HH:mm</c>）。</param>
public sealed record IndexCardDto(
    string Code,
    string Name,
    decimal Price,
    decimal Chg,
    decimal Pct,
    decimal Amount,
    IReadOnlyList<decimal> Spark,
    string AsOf);

/// <summary>
/// 市场宽度（涨跌家数与两市资金）。
/// </summary>
/// <param name="Up">上涨家数。</param>
/// <param name="Down">下跌家数。</param>
/// <param name="Flat">平盘家数。</param>
/// <param name="LimitUp">涨停家数（交易所口径）。</param>
/// <param name="LimitDown">跌停家数（交易所口径）。</param>
/// <param name="Total">统计口径内的标的总数。</param>
/// <param name="Turnover">两市成交额（亿元）。</param>
/// <param name="TurnoverPct">成交额较上一交易日变化（百分数）；无上一交易日样本时为 null。</param>
/// <param name="Northbound">北向净流入（亿元）。<b>本轮恒为 null</b>：公开接口已不再提供逐日净买入，
/// 见 <paramref name="NorthboundNote"/>。</param>
/// <param name="Northbound5">近 5 个交易日北向序列（亿元）；本轮为空数组。</param>
/// <param name="NorthboundNote">北向数据的口径说明与降级原因，界面必须原样展示（不允许静默留空）。</param>
/// <param name="MarginBalance">融资余额（亿元，沪深合计）。</param>
/// <param name="MarginChg">融资余额变化（百分数）；无上一交易日样本时为 null。</param>
/// <param name="AsOf">行情数据时间。</param>
/// <param name="MarginAsOf">两融数据时间（披露晚于行情，单独标注）。</param>
public sealed record MarketBreadthDto(
    int Up,
    int Down,
    int Flat,
    int LimitUp,
    int LimitDown,
    int Total,
    decimal Turnover,
    decimal? TurnoverPct,
    decimal? Northbound,
    IReadOnlyList<decimal> Northbound5,
    string NorthboundNote,
    decimal MarginBalance,
    decimal? MarginChg,
    string AsOf,
    string? MarginAsOf);

/// <summary>
/// 行业条目（热力图与排行共用）。行业分类为东财口径。
/// </summary>
/// <param name="Code">东财板块码，如 <c>BK1201</c>。</param>
/// <param name="Name">行业名。</param>
/// <param name="Pct">涨跌幅（百分数）。</param>
/// <param name="Flow">主力净流入（亿元）。</param>
/// <param name="Leader">领涨股名称。</param>
/// <param name="LeaderCode">领涨股代码。</param>
/// <param name="Pe">板块市盈率；缺失为 null。</param>
/// <param name="PePct">估值分位；本轮无同口径历史样本时为 null。</param>
/// <param name="UpCount">板块内上涨家数。</param>
/// <param name="DownCount">板块内下跌家数。</param>
public sealed record SectorDto(
    string Code,
    string Name,
    decimal Pct,
    decimal Flow,
    string? Leader,
    string? LeaderCode,
    decimal? Pe,
    decimal? PePct,
    int UpCount,
    int DownCount);

/// <summary>
/// 榜单单行。对应原型 <c>rankings</c> 的 <c>{ code, name, price, pct, amount }</c>。
/// </summary>
/// <param name="Code">证券代码。</param>
/// <param name="Name">证券名称。</param>
/// <param name="Price">最新价（元）。</param>
/// <param name="Pct">涨跌幅（百分数）。</param>
/// <param name="Amount">成交额（亿元）。</param>
/// <param name="Industry">东财行业。</param>
/// <param name="Board">板块。</param>
/// <param name="IsSt">是否 ST / 退市风险标的（列表需显著标注）。</param>
/// <param name="IsNew">是否新股 / 次新股（名称前缀 N / C）；涨跌幅榜已剔除，此处透出以便界面解释。</param>
public sealed record RankingRowDto(
    string Code,
    string Name,
    decimal Price,
    decimal Pct,
    decimal Amount,
    string? Industry,
    string Board,
    bool IsSt,
    bool IsNew);

/// <summary>
/// 榜单集合：成交额榜 / 涨幅榜 / 跌幅榜。
/// </summary>
/// <param name="Amount">成交额榜。</param>
/// <param name="Gainers">涨幅榜。</param>
/// <param name="Losers">跌幅榜。</param>
/// <param name="AsOf">数据时间。</param>
public sealed record MarketRankingsDto(
    IReadOnlyList<RankingRowDto> Amount,
    IReadOnlyList<RankingRowDto> Gainers,
    IReadOnlyList<RankingRowDto> Losers,
    string AsOf);

/// <summary>
/// 全市场资金分层净额（单位亿元；主力 = 超大单 + 大单）。
/// </summary>
/// <param name="SuperLarge">超大单净额。</param>
/// <param name="Large">大单净额。</param>
/// <param name="Medium">中单净额。</param>
/// <param name="Small">小单净额。</param>
/// <param name="MainNet">主力净额。</param>
public sealed record FundFlowLayersDto(decimal SuperLarge, decimal Large, decimal Medium, decimal Small, decimal MainNet);

/// <summary>
/// 市场资金与杠杆卡片。
/// </summary>
/// <param name="Layers">分层净额（两市合计）。</param>
/// <param name="FinanceBalance">融资余额（亿元，沪深合计）。</param>
/// <param name="LoanBalance">融券余额（亿元，沪深合计）。</param>
/// <param name="MarginChg">融资余额较上一交易日变化（百分数）；无上一交易日样本时为 null。</param>
/// <param name="AsOf">资金流口径日。</param>
/// <param name="MarginAsOf">两融口径日（披露晚于行情，单独标注）。</param>
public sealed record MarketFundFlowDto(
    FundFlowLayersDto Layers,
    decimal FinanceBalance,
    decimal LoanBalance,
    decimal? MarginChg,
    string AsOf,
    string? MarginAsOf);

/// <summary>
/// 数据新鲜度与采集状态。对应实施计划 §6 的 <c>*GET /api/market/status</c>，
/// 界面在每张卡与页头显示数据时间与来源（不允许把旧数据当新数据展示）。
/// </summary>
/// <param name="AsOf">快照业务日期（<c>yyyy-MM-dd</c>）。</param>
/// <param name="UpdatedAt">快照写入时间（<c>yyyy-MM-dd HH:mm:ss</c>）。</param>
/// <param name="TradingDay">今日是否交易日。</param>
/// <param name="MarketPhase">市场阶段：盘前 / 交易中 / 午间休市 / 已收盘 / 非交易日。</param>
/// <param name="IsReady">首屏所需数据是否已就绪；false 时接口返回 <c>1003</c>。</param>
/// <param name="Sources">各数据源健康状态。</param>
public sealed record MarketStatusDto(
    string? AsOf,
    string? UpdatedAt,
    bool TradingDay,
    string MarketPhase,
    bool IsReady,
    IReadOnlyList<DataSourceStatusDto> Sources);

/// <summary>
/// 单个数据源的健康状态。
/// </summary>
/// <param name="Name">数据源名。</param>
/// <param name="Type">主源 / 备源。</param>
/// <param name="Domains">承载的域。</param>
/// <param name="Status">ok / warn / err / idle。</param>
/// <param name="LastOkAt">最近成功时间。</param>
/// <param name="LatencyMs">最近耗时（毫秒）。</param>
/// <param name="FailCount">连续失败次数。</param>
/// <param name="LastError">最近错误摘要。</param>
public sealed record DataSourceStatusDto(
    string Name,
    string? Type,
    string? Domains,
    string Status,
    string? LastOkAt,
    int? LatencyMs,
    int FailCount,
    string? LastError);

/// <summary>
/// 市场概览聚合（一次请求返回市场页首屏所需的全部数据，避免多次往返）。
/// </summary>
/// <param name="Indices">指数卡片。</param>
/// <param name="Breadth">涨跌家数与两市资金。</param>
/// <param name="FundFlow">两市资金分层与两融杠杆。</param>
/// <param name="Industries">行业热力与排行。</param>
/// <param name="Rankings">榜单。</param>
/// <param name="Status">新鲜度与数据源状态。</param>
public sealed record MarketOverviewDto(
    IReadOnlyList<IndexCardDto> Indices,
    MarketBreadthDto Breadth,
    MarketFundFlowDto FundFlow,
    IReadOnlyList<SectorDto> Industries,
    MarketRankingsDto Rankings,
    MarketStatusDto Status);

/// <summary>
/// 全市场列表的一行。
/// </summary>
/// <remarks>
/// 大盘概况页要「一个页面搞定整个 A 股市场」，因此需要一个能排序 / 筛选 / 分页的全市场列表。
/// 字段与 <see cref="SA.Contracts.Search.SearchRowDto"/> 保持一致（同一套行情快照与基础信息），
/// 但不含 <c>MatchedBy</c>（那是搜索命中的解释，列表里没有意义）。
/// </remarks>
/// <param name="Code">代码。</param>
/// <param name="Name">名称。</param>
/// <param name="Py">拼音首字母（小写）。</param>
/// <param name="Board">板块：沪市主板 / 深市主板 / 创业板 / 科创板 / 北交所。</param>
/// <param name="Industry">东财行业。</param>
/// <param name="Price">最新价（元）；无快照时为 null。</param>
/// <param name="Chg">涨跌额（元）。</param>
/// <param name="Pct">涨跌幅（百分数）。</param>
/// <param name="VolRatio">量比。</param>
/// <param name="Turnover">换手率（百分数）。</param>
/// <param name="Pe">市盈率（动态）。</param>
/// <param name="Pb">市净率。</param>
/// <param name="Cap">总市值（亿元）。</param>
/// <param name="IsSt">是否 ST / 退市风险标的。</param>
public sealed record MarketStockRowDto(
    string Code,
    string Name,
    string? Py,
    string Board,
    string? Industry,
    decimal? Price,
    decimal? Chg,
    decimal? Pct,
    decimal? VolRatio,
    decimal? Turnover,
    decimal? Pe,
    decimal? Pb,
    decimal? Cap,
    bool IsSt);

/// <summary>
/// 全市场列表响应。
/// </summary>
/// <param name="Total">筛选后的总条数（未分页前）。</param>
/// <param name="Page">当前页（1 起）。</param>
/// <param name="PageSize">每页条数。</param>
/// <param name="Rows">结果行。</param>
/// <param name="Boards">可选板块（界面页签直接用，不在前端硬编码）。</param>
/// <param name="AsOf">行情口径日。</param>
/// <param name="ScopeNote">数据范围受限时的提示文案。</param>
public sealed record MarketStocksDto(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<MarketStockRowDto> Rows,
    IReadOnlyList<string> Boards,
    string? AsOf,
    string? ScopeNote);
