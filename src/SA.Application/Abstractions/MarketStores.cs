using SA.Domain.Entities.Collect;
using SA.Domain.Entities.Market;

namespace SA.Application.Abstractions;

/// <summary>
/// 证券基础信息（股票池）的读写。
/// </summary>
public interface IInstrumentStore
{
    /// <summary>取全部证券基础信息（搜索与聚合的内存索引来源）。</summary>
    Task<IReadOnlyList<Instrument>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>取指定代码的基础信息。</summary>
    Task<IReadOnlyDictionary<string, Instrument>> GetByCodesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken = default);

    /// <summary>按代码取单只基础信息。</summary>
    Task<Instrument?> FindAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>当前池内证券数量。</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量 upsert（按代码）。返回写入行数。
    /// </summary>
    /// <remarks>
    /// 幂等：同一代码重复写入只更新，不产生重复行（实施计划 §9.2）。
    /// </remarks>
    Task<int> UpsertAsync(IReadOnlyList<Instrument> instruments, CancellationToken cancellationToken = default);

    /// <summary>批量更新拼音首字母（只写非空值）。</summary>
    Task<int> UpdatePinyinAsync(
        IReadOnlyDictionary<string, string> pinyinByCode,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 个股行情快照的读写。整体替换语义：一次扫描成功后库内即为该轮结果。
/// </summary>
public interface IQuoteSnapshotStore
{
    /// <summary>取全部快照（市场页聚合与选股器的内存来源）。</summary>
    Task<IReadOnlyList<QuoteSnapshot>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>取指定代码的快照。</summary>
    Task<IReadOnlyDictionary<string, QuoteSnapshot>> GetByCodesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken = default);

    /// <summary>当前快照条数。</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>最近一次写入时间；从未采集时返回 null。</summary>
    Task<DateTimeOffset?> GetLastUpdatedAtAsync(CancellationToken cancellationToken = default);

    /// <summary>用一轮扫描结果整体替换（先按代码 upsert，再清理本轮未出现的退市/停牌标的）。</summary>
    Task<int> ReplaceAllAsync(IReadOnlyList<QuoteSnapshot> snapshots, CancellationToken cancellationToken = default);
}

/// <summary>
/// 行业板块快照的读写。
/// </summary>
public interface ISectorStore
{
    /// <summary>取全部行业板块快照。</summary>
    Task<IReadOnlyList<Sector>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>整体替换。</summary>
    Task<int> ReplaceAllAsync(IReadOnlyList<Sector> sectors, CancellationToken cancellationToken = default);

    /// <summary>最近一次写入时间。</summary>
    Task<DateTimeOffset?> GetLastUpdatedAtAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 指数快照的读写。
/// </summary>
public interface IIndexStore
{
    /// <summary>取全部指数快照（按展示顺序）。</summary>
    Task<IReadOnlyList<IndexQuote>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>整体替换。</summary>
    Task<int> ReplaceAllAsync(IReadOnlyList<IndexQuote> indices, CancellationToken cancellationToken = default);

    /// <summary>最近一次写入时间。</summary>
    Task<DateTimeOffset?> GetLastUpdatedAtAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 采集监控：数据源状态与任务日志。
/// </summary>
public interface ICollectStatusStore
{
    /// <summary>取全部数据源状态。</summary>
    Task<IReadOnlyList<DataSourceStatus>> GetSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 记录一次数据源执行结果：成功则刷新耗时、清零连续失败；失败则累加并置降级状态。
    /// </summary>
    /// <param name="source">数据源名。</param>
    /// <param name="domains">承载的域。</param>
    /// <param name="type">主源 / 备源。</param>
    /// <param name="ok">是否成功。</param>
    /// <param name="latencyMs">耗时（毫秒）。</param>
    /// <param name="error">失败摘要。</param>
    /// <param name="degradeAfterFailures">连续失败降级阈值。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task RecordSourceAsync(
        string source,
        string domains,
        string type,
        bool ok,
        long latencyMs,
        string? error,
        int degradeAfterFailures,
        CancellationToken cancellationToken = default);

    /// <summary>写一条采集任务日志，返回自增 Id。</summary>
    Task<long> AddTaskLogAsync(CollectTaskLog log, CancellationToken cancellationToken = default);

    /// <summary>取最近若干条采集任务日志（按时间倒序）。</summary>
    Task<IReadOnlyList<CollectTaskLog>> GetRecentTasksAsync(int take, CancellationToken cancellationToken = default);
}

/// <summary>
/// 交易日历的读写。日历由指数日线推导，不单独引入数据源（实施计划 §5.3）。
/// </summary>
public interface ITradingCalendarStore
{    /// <summary>取日历区间（按日期升序）。</summary>
    Task<IReadOnlyList<TradingDay>> GetRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>批量 upsert 日历。</summary>
    Task<int> UpsertAsync(IReadOnlyList<TradingDay> days, CancellationToken cancellationToken = default);

    /// <summary>日历中的最后一天；尚未生成时返回 null。</summary>
    Task<DateOnly?> GetLastDateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 市场级日度统计的读写。承载无法由个股快照推导的字段（涨跌停、资金分层、两融）。
/// </summary>
public interface IMarketStatStore
{
    /// <summary>取指定业务日的统计；不存在返回 null。</summary>
    Task<MarketStat?> FindAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>取最近一条统计（不限定日期，用于「上一交易日」口径）。</summary>
    Task<MarketStat?> GetLatestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 按业务日合并写入：只覆盖本次实际采到的字段，未采到的保留原值。
    /// </summary>
    /// <remarks>
    /// 涨跌停、资金流、两融由三个独立任务采集且披露时间不同，因此必须是字段级合并，
    /// 否则后跑的任务会把先跑任务的结果清零。
    /// </remarks>
    Task UpsertAsync(MarketStatPatch patch, CancellationToken cancellationToken = default);
}

/// <summary>
/// 市场统计的字段级补丁：为 null 的字段表示「本次没采到，保持原值」。
/// </summary>
/// <param name="Date">业务日。</param>
/// <param name="LimitUp">涨停家数。</param>
/// <param name="LimitDown">跌停家数。</param>
/// <param name="FundFlow">资金分层。</param>
/// <param name="FundFlowDate">资金流口径日。</param>
/// <param name="Margin">两融余额。</param>
/// <param name="MarginDate">两融口径日。</param>
public readonly record struct MarketStatPatch(
    DateOnly Date,
    int? LimitUp = null,
    int? LimitDown = null,
    MarketFundFlowResult? FundFlow = null,
    DateOnly? FundFlowDate = null,
    MarginMarketResult? Margin = null,
    DateOnly? MarginDate = null);
