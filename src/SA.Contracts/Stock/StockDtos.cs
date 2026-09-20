namespace SA.Contracts.Stock;

/// <summary>
/// 个股行情条（详情页头部与总览页共用）。
/// </summary>
/// <param name="Code">代码。</param>
/// <param name="Name">名称。</param>
/// <param name="Py">拼音首字母。</param>
/// <param name="Board">板块。</param>
/// <param name="Industry">东财行业。</param>
/// <param name="Price">最新价；无行情为 null。</param>
/// <param name="Chg">涨跌额。</param>
/// <param name="Pct">涨跌幅。</param>
/// <param name="Open">今开。</param>
/// <param name="High">最高。</param>
/// <param name="Low">最低。</param>
/// <param name="PrevClose">昨收。</param>
/// <param name="Volume">成交量（手）。</param>
/// <param name="Amount">成交额（亿元）。</param>
/// <param name="Turnover">换手率（百分数）。</param>
/// <param name="VolRatio">量比。</param>
/// <param name="Cap">总市值（亿元）。</param>
/// <param name="FloatCap">流通市值（亿元）。</param>
/// <param name="Pe">市盈率（动态）。</param>
/// <param name="PeTtm">市盈率（TTM）。</param>
/// <param name="Pb">市净率。</param>
/// <param name="IsSt">是否 ST / 退市风险。</param>
/// <param name="AsOf">行情口径日。</param>
public sealed record StockProfileDto(
    string Code,
    string Name,
    string? Py,
    string Board,
    string? Industry,
    decimal? Price,
    decimal? Chg,
    decimal? Pct,
    decimal? Open,
    decimal? High,
    decimal? Low,
    decimal? PrevClose,
    decimal? Volume,
    decimal? Amount,
    decimal? Turnover,
    decimal? VolRatio,
    decimal? Cap,
    decimal? FloatCap,
    decimal? Pe,
    decimal? PeTtm,
    decimal? Pb,
    bool IsSt,
    string? AsOf);

/// <summary>
/// 总览页的一个模块摘要卡。
/// </summary>
/// <param name="Key">模块键：trend / finance / equity / capital / industry / events / risk / rating。</param>
/// <param name="Name">模块名。</param>
/// <param name="Status">状态：ready（已有真实数据） / collecting（本批尚未接入，界面显示采集中空态）。</param>
/// <param name="Tags">模块标签，如「多头排列」。</param>
/// <param name="Kpis">模块关键指标。</param>
/// <param name="Thumb">缩略图数据（趋势卡为近 60 日收盘序列）。</param>
/// <param name="Summary">一句话结论。</param>
/// <param name="Link">模块页路由。</param>
public sealed record ModuleCardDto(
    string Key,
    string Name,
    string Status,
    IReadOnlyList<ModuleTagDto> Tags,
    IReadOnlyList<ModuleKpiDto> Kpis,
    IReadOnlyList<decimal> Thumb,
    string? Summary,
    string Link);

/// <summary>模块标签。</summary>
/// <param name="Text">文案。</param>
/// <param name="Tone">色调：up / down / warn / neutral。</param>
public sealed record ModuleTagDto(string Text, string Tone);

/// <summary>模块关键指标。</summary>
/// <param name="Label">指标名。</param>
/// <param name="Value">已格式化好的展示值。</param>
/// <param name="Tone">色调。</param>
public sealed record ModuleKpiDto(string Label, string Value, string Tone);

/// <summary>
/// 个股总览（<c>GET /api/stocks/{code}/overview</c>）。
/// </summary>
/// <param name="Profile">行情条。</param>
/// <param name="Modules">8 个模块摘要卡。</param>
/// <param name="Summary">页头结论摘要。</param>
/// <param name="Notes">口径说明。</param>
public sealed record StockOverviewDto(
    StockProfileDto Profile,
    IReadOnlyList<ModuleCardDto> Modules,
    IReadOnlyList<ModuleTagDto> Summary,
    IReadOnlyList<string> Notes);

/// <summary>
/// 数据新鲜度（<c>GET /api/stocks/{code}/freshness</c>）。
/// </summary>
/// <param name="Code">代码。</param>
/// <param name="AsOf">行情口径日。</param>
/// <param name="DailyLastDate">日线已入库的最后交易日；尚未回补为 null。</param>
/// <param name="IndicatorLastDate">指标已入库的最后交易日。</param>
/// <param name="Collecting">是否正在回补（true 时界面显示「采集中」并可重试）。</param>
public sealed record StockFreshnessDto(
    string Code,
    string? AsOf,
    string? DailyLastDate,
    string? IndicatorLastDate,
    bool Collecting);
