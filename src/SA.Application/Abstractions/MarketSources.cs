namespace SA.Application.Abstractions;

/// <summary>
/// 采集源共有的探活能力。所有适配器都实现它，<c>--probe</c> 与健康巡检共用，
/// 避免「界面上显示正常、实际早已失效」的情况（实施计划 §5.4）。
/// </summary>
public interface IProbeable
{
    /// <summary>数据源名（写入 <c>DataSourceStatus</c> 与日志）。</summary>
    string Name { get; }

    /// <summary>承载的域，逗号分隔。</summary>
    string Domains { get; }

    /// <summary>执行一次轻量探活。</summary>
    Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 探活结果。
/// </summary>
/// <param name="Ok">是否成功。</param>
/// <param name="LatencyMs">耗时（毫秒）。</param>
/// <param name="HttpStatus">HTTP 状态码。</param>
/// <param name="Rows">返回行数，用于确认不只是「HTTP 200 但空数据」。</param>
/// <param name="Error">失败摘要。</param>
public readonly record struct SourceProbeResult(bool Ok, long LatencyMs, int? HttpStatus, int Rows, string? Error)
{
    /// <summary>构造成功结果。</summary>
    public static SourceProbeResult Success(long latencyMs, int httpStatus, int rows) =>
        new(true, latencyMs, httpStatus, rows, null);

    /// <summary>构造失败结果。</summary>
    public static SourceProbeResult Failure(long latencyMs, int? httpStatus, string error) =>
        new(false, latencyMs, httpStatus, 0, error);
}

/// <summary>
/// 全市场证券列表源。一次响应同时给出基础信息与最新行情，
/// 因此它既是「股票池」也是「快照」的来源（东财 <c>clist/get</c>）。
/// </summary>
/// <remarks>
/// <b>分页事实</b>：该端点单页上限为 100 行（实测 <c>pz=6000</c> 仍只返回 100 行），
/// 全市场约 5,900 只因此需要约 60 次请求才能扫完一轮，这决定了全市场快照的刷新区间
/// 不能按自选股的 3 秒来设（见 <see cref="CollectOptions.FullScanIntervalSeconds"/>）。
/// </remarks>
public interface IMarketListSource : IProbeable
{
    /// <summary>
    /// 拉取全市场证券列表（内部自动翻页）。
    /// </summary>
    /// <param name="onPage">每页回调，便于边拉边写、避免一次性驻留全量。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<MarketListResult> GetMarketListAsync(
        Func<IReadOnlyList<MarketListRow>, Task>? onPage = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 全市场列表的一次拉取结果。
/// </summary>
/// <param name="Rows">行数。</param>
/// <param name="Total">上游声明的总数，用于校验分页是否完整。</param>
/// <param name="AsOf">上游返回的业务日期（<c>data.diff</c> 不含日期时取本机业务日）。</param>
public readonly record struct MarketListResult(int Rows, int Total, DateOnly AsOf);

/// <summary>
/// 全市场列表的一行（已按字段名解析，未做单位换算）。
/// </summary>
/// <param name="Code">证券代码。</param>
/// <param name="Name">证券名称。</param>
/// <param name="Market">东财市场标志：1=沪市，0=深市与北交所。</param>
/// <param name="Industry">东财行业（<c>f100</c>），可能为空。</param>
/// <param name="Price">最新价。</param>
/// <param name="Change">涨跌额。</param>
/// <param name="Pct">涨跌幅（百分数）。</param>
/// <param name="Volume">成交量（手）。</param>
/// <param name="Amount">成交额（元）。</param>
/// <param name="Turnover">换手率（百分数）。</param>
/// <param name="VolRatio">量比。</param>
/// <param name="Pe">市盈率（动态）。</param>
/// <param name="PeTtm">市盈率（TTM）。</param>
/// <param name="Pb">市净率。</param>
/// <param name="MarketCap">总市值（元）。</param>
/// <param name="FloatCap">流通市值（元）。</param>
/// <param name="Open">今开。</param>
/// <param name="High">最高。</param>
/// <param name="Low">最低。</param>
/// <param name="PrevClose">昨收。</param>
public readonly record struct MarketListRow(
    string Code,
    string Name,
    int Market,
    string? Industry,
    decimal Price,
    decimal Change,
    decimal Pct,
    decimal Volume,
    decimal Amount,
    decimal Turnover,
    decimal VolRatio,
    decimal Pe,
    decimal PeTtm,
    decimal Pb,
    decimal MarketCap,
    decimal FloatCap,
    decimal Open,
    decimal High,
    decimal Low,
    decimal PrevClose);

/// <summary>
/// 多标的实时快照源（按代码列表取，一次请求覆盖全部标的）。
/// </summary>
/// <remarks>
/// 与 <see cref="IMarketListSource"/> 的分工：本接口按<b>给定代码</b>取快照，请求数与标的数无关，
/// 因此适合自选股等小集合的高频刷新；全市场扫描只能走列表端点（需翻页）。
/// 主源为东财，备源为腾讯，两者都实现本接口即可被 <c>SourceRegistry</c> 串成降级链。
/// </remarks>
public interface IQuoteSnapshotSource : IProbeable
{
    /// <summary>
    /// 取指定代码的最新快照。
    /// </summary>
    /// <param name="codes">证券代码集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<QuoteRow>> GetQuotesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 多标的快照行。
/// </summary>
/// <param name="Code">证券代码。</param>
/// <param name="Name">证券名称。</param>
/// <param name="Market">市场标志：1=沪市，0=深市与北交所。</param>
/// <param name="Price">最新价。</param>
/// <param name="Change">涨跌额。</param>
/// <param name="Pct">涨跌幅（百分数）。</param>
/// <param name="Volume">成交量（手）。</param>
/// <param name="Amount">成交额（元）。</param>
/// <param name="Turnover">换手率（百分数）。</param>
/// <param name="VolRatio">量比。</param>
/// <param name="Open">今开。</param>
/// <param name="High">最高。</param>
/// <param name="Low">最低。</param>
/// <param name="PrevClose">昨收。</param>
/// <param name="MarketCap">总市值（元）。</param>
/// <param name="FloatCap">流通市值（元）。</param>
/// <param name="Pe">市盈率（动态）。</param>
/// <param name="PeTtm">市盈率（TTM）。</param>
/// <param name="Pb">市净率。</param>
/// <param name="AsOf">行情时间戳（上游给出时）。</param>
public readonly record struct QuoteRow(
    string Code,
    string Name,
    int Market,
    decimal Price,
    decimal Change,
    decimal Pct,
    decimal Volume,
    decimal Amount,
    decimal Turnover,
    decimal VolRatio,
    decimal Open,
    decimal High,
    decimal Low,
    decimal PrevClose,
    decimal MarketCap,
    decimal FloatCap,
    decimal Pe,
    decimal PeTtm,
    decimal Pb,
    DateTimeOffset? AsOf);

/// <summary>
/// 指数快照源。
/// </summary>
public interface IIndexSource : IProbeable
{
    /// <summary>拉取市场概览所需的指数快照。</summary>
    Task<IReadOnlyList<IndexQuoteRow>> GetIndicesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 指数快照行。
/// </summary>
/// <param name="Code">指数代码。</param>
/// <param name="Name">指数名称。</param>
/// <param name="Market">市场标志。</param>
/// <param name="Price">最新点位。</param>
/// <param name="Change">涨跌点数。</param>
/// <param name="Pct">涨跌幅（百分数）。</param>
/// <param name="Volume">成交量（手）。</param>
/// <param name="Amount">成交额（元）。</param>
/// <param name="AsOf">上游给出的行情时间戳；<b>全市场数据的业务日期</b>由此确定
/// （列表端点不返回日期，用本机日期会把上一交易日的数据标成当天）。</param>
public readonly record struct IndexQuoteRow(
    string Code,
    string Name,
    int Market,
    decimal Price,
    decimal Change,
    decimal Pct,
    decimal Volume,
    decimal Amount,
    DateTimeOffset? AsOf);

/// <summary>
/// 行业板块源（东财行业口径）。
/// </summary>
public interface ISectorSource : IProbeable
{
    /// <summary>拉取行业板块列表（东财行业板块，约 500 个）。</summary>
    Task<IReadOnlyList<SectorRow>> GetSectorsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 行业板块行。
/// </summary>
/// <param name="Code">板块码（BK 码）。</param>
/// <param name="Name">板块名。</param>
/// <param name="Pct">涨跌幅（百分数）。</param>
/// <param name="MainNet">主力净流入（元）。</param>
/// <param name="UpCount">上涨家数。</param>
/// <param name="DownCount">下跌家数。</param>
/// <param name="LeaderName">领涨股名称。</param>
/// <param name="LeaderCode">领涨股代码。</param>
/// <param name="Pe">板块市盈率；上游给 <c>-</c> 表示缺失。</param>
public readonly record struct SectorRow(
    string Code,
    string Name,
    decimal Pct,
    decimal MainNet,
    int UpCount,
    int DownCount,
    string? LeaderName,
    string? LeaderCode,
    decimal? Pe);

/// <summary>
/// 涨跌停统计源。涨停/跌停家数用交易所口径的专用端点获取，
/// 而不是用涨跌幅阈值反推（阈值反推在 20% 涨跌幅板块与 ST 上都会算错）。
/// </summary>
public interface ILimitPoolSource : IProbeable
{
    /// <summary>
    /// 拉取指定交易日的涨跌停家数。
    /// </summary>
    /// <remarks>
    /// 上游会忽略早于最新交易日的 <c>date</c> 参数，因此以响应中的 <c>qdate</c> 作为
    /// 实际口径日期返回，避免把最新交易日的数据标成历史日期。
    /// </remarks>
    Task<LimitPoolResult> GetLimitPoolsAsync(DateOnly? date = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// 涨跌停统计结果。
/// </summary>
/// <param name="Date">上游返回的实际交易日。</param>
/// <param name="LimitUp">涨停家数。</param>
/// <param name="LimitDown">跌停家数。</param>
public readonly record struct LimitPoolResult(DateOnly Date, int LimitUp, int LimitDown);

/// <summary>
/// 两市两融余额源。
/// </summary>
public interface IMarginMarketSource : IProbeable
{
    /// <summary>
    /// 拉取最近一个「沪深两市均已披露」交易日的两融余额。
    /// </summary>
    /// <remarks>
    /// 两融按市场逐个披露，最新交易日往往只有沪市，直接取最新日期会把余额少算一半；
    /// 因此实现按「沪证 + 深证均已出现的最近日期」取值。
    /// </remarks>
    Task<MarginMarketResult?> GetLatestAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 两市两融余额。
/// </summary>
/// <param name="Date">口径交易日。</param>
/// <param name="FinanceBalance">融资余额（元，沪深两市合计）。</param>
/// <param name="LoanBalance">融券余额（元，沪深两市合计）。</param>
public readonly record struct MarginMarketResult(DateOnly Date, decimal FinanceBalance, decimal LoanBalance);

/// <summary>
/// 大盘资金流源（全市场资金分层，市场页「两市资金与杠杆」卡片）。
/// </summary>/// <remarks>
/// 个股的分层资金流端点（<c>stock/fflow/daykline/get</c>）不适用于全市场；
/// 全市场用指数口径的 <c>stock/fflow/kline/get</c>，分别取沪市（<c>1.000001</c>）与
/// 深市（<c>0.399001</c>）后按层相加，得到「两市合计」。
/// </remarks>
public interface IMarketFundFlowSource : IProbeable
{
    /// <summary>取最近一个交易日的两市资金分层（单位：元）。</summary>
    Task<MarketFundFlowResult?> GetLatestAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 两市资金分层结果。
/// </summary>
/// <param name="Date">口径交易日。</param>
/// <param name="MainNet">主力净额（= 大单 + 超大单）。</param>
/// <param name="SuperLarge">超大单净额。</param>
/// <param name="Large">大单净额。</param>
/// <param name="Medium">中单净额。</param>
/// <param name="Small">小单净额。</param>
public readonly record struct MarketFundFlowResult(
    DateOnly Date,
    decimal MainNet,
    decimal SuperLarge,
    decimal Large,
    decimal Medium,
    decimal Small);

/// <summary>
/// 交易日历源：由指数日线的日期序列推导交易日，不单独引入日历数据源（实施计划 §5.3）。
/// </summary>
public interface ITradingCalendarSource : IProbeable
{
    /// <summary>
    /// 取给定区间内的指数日线日期（即交易日）。
    /// </summary>
    /// <param name="from">起始日期（含）。</param>
    /// <param name="to">结束日期（含）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<DateOnly>> GetTradingDaysAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
