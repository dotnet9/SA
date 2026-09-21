using SA.Domain.Entities.Market;

namespace SA.Application.Market;

/// <summary>
/// 全市场快照的内存副本。市场概览与排行榜都建立在「全市场一次性聚合」之上，
/// 每请求从 SQLite 读 6,000 行既慢又无意义，因此由采集任务在每轮扫描成功后整体替换，
/// 读侧只读内存（详细设计 §12「全市场快照：整体替换而非逐条更新」）。
/// </summary>
/// <remarks>
/// 单例注册。读侧拿到的永远是<b>不可变的一份引用</b>，替换是交换引用，
/// 因此并发读不会读到写了一半的集合。
/// </remarks>
public sealed class MarketSnapshotCache
{
    private volatile Snapshot _current = Snapshot.Empty;

    /// <summary>当前快照。</summary>
    public Snapshot Current => _current;

    /// <summary>快照是否为空（尚未采集过）。</summary>
    public bool IsEmpty => _current.Rows.Count == 0;

    /// <summary>
    /// 用一轮扫描结果整体替换，口径日与写入时间由行内数据推导。
    /// </summary>
    /// <param name="rows">快照行。</param>
    /// <param name="instruments">基础信息（行业、板块、ST 标记）。</param>
    /// <summary>
    /// 用一轮扫描结果整体替换行情快照，口径日与写入时间由行内数据推导。
    /// </summary>
    /// <remarks>
    /// <b>已加载的基本面必须原样带过去</b>：行情每 60 秒整体替换一次，而基本面是季频的、
    /// 由 <see cref="ReplaceFundamentals"/> 单独更新。若在这里重置，每轮扫描都会把基本面清空，
    /// 选股器的价值字段会变成「时有时无」——表现为随刷新而随机丢失筛选条件。
    /// </remarks>
    /// <param name="rows">快照行。</param>
    /// <param name="instruments">基础信息（行业、板块、ST 标记）。</param>
    public void Replace(IReadOnlyList<QuoteSnapshot> rows, IReadOnlyDictionary<string, Instrument> instruments) =>
        _current = Snapshot.Create(rows, instruments) with
        {
            Fundamentals = _current.Fundamentals,
            AnnualFundamentals = _current.AnnualFundamentals,
            Dividends = _current.Dividends,
            FundamentalsLoaded = _current.FundamentalsLoaded
        };

    /// <summary>
    /// 更新基本面数据（最新一期 + 年报序列 + 股息率）。
    /// </summary>
    /// <remarks>
    /// 与行情快照分开更新：基本面是<b>季频</b>数据（一个报告期一次），
    /// 而行情快照每 60 秒整体替换。若把两者绑在一起，每次行情刷新都要重读万级基本面行。
    /// </remarks>
    /// <param name="latest">代码到「最新一期基本面」。</param>
    /// <param name="annual">代码到「年报序列（升序）」。</param>
    /// <param name="dividends">代码到「最新股息率」；来自业绩报表，与基本面报表不同源。</param>
    public void ReplaceFundamentals(
        IReadOnlyDictionary<string, Domain.Entities.Finance.FundamentalMetric> latest,
        IReadOnlyDictionary<string, IReadOnlyList<Domain.Entities.Finance.FundamentalMetric>> annual,
        IReadOnlyDictionary<string, decimal?> dividends) =>
        _current = _current with
        {
            Fundamentals = latest,
            AnnualFundamentals = annual,
            Dividends = dividends,
            FundamentalsLoaded = true
        };

    /// <summary>
    /// 不可变快照。
    /// </summary>
    /// <param name="Rows">快照行（按代码升序）。</param>
    /// <param name="ByCode">代码到快照行的映射，供按代码取价。 </param>
    /// <param name="Instruments">代码到基础信息的映射。</param>
    /// <param name="AsOf">行情口径日。</param>
    /// <param name="UpdatedAt">写入时间。</param>
    /// <param name="Fundamentals">代码到「最新一期基本面」的映射；未加载时为空。</param>
    /// <param name="AnnualFundamentals">代码到「年报序列（按报告期升序）」的映射。</param>
    /// <param name="Dividends">代码到「最新股息率」；来自业绩报表。</param>
    /// <param name="FundamentalsLoaded">基本面是否已加载过（用于区分「没加载」与「确实没有」）。</param>
    public sealed record Snapshot(
        IReadOnlyList<QuoteSnapshot> Rows,
        IReadOnlyDictionary<string, QuoteSnapshot> ByCode,
        IReadOnlyDictionary<string, Instrument> Instruments,
        DateOnly AsOf,
        DateTimeOffset UpdatedAt,
        IReadOnlyDictionary<string, Domain.Entities.Finance.FundamentalMetric> Fundamentals,
        IReadOnlyDictionary<string, IReadOnlyList<Domain.Entities.Finance.FundamentalMetric>> AnnualFundamentals,
        IReadOnlyDictionary<string, decimal?> Dividends,
        bool FundamentalsLoaded)
    {
        /// <summary>空快照。</summary>
        public static Snapshot Empty { get; } = new(
            [],
            new Dictionary<string, QuoteSnapshot>(StringComparer.Ordinal),
            new Dictionary<string, Instrument>(StringComparer.Ordinal),
            DateOnly.MinValue,
            DateTimeOffset.MinValue,
            new Dictionary<string, Domain.Entities.Finance.FundamentalMetric>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<Domain.Entities.Finance.FundamentalMetric>>(StringComparer.Ordinal),
            new Dictionary<string, decimal?>(StringComparer.Ordinal),
            false);

        /// <summary>
        /// 由行集合构建快照：统一在这里推导口径日与写入时间，避免各调用点各写一遍。
        /// </summary>
        /// <remarks>
        /// 基本面字段刻意<b>不在这里</b>填充：它们由 <see cref="ReplaceFundamentals"/> 单独更新，
        /// 因此整体替换行情时必须把已加载的基本面原样带过去，否则每轮扫描都会把基本面清空。
        /// </remarks>
        public static Snapshot Create(
            IReadOnlyList<QuoteSnapshot> rows,
            IReadOnlyDictionary<string, Instrument> instruments)
        {
            if (rows.Count == 0)
            {
                return Empty with { Instruments = instruments };
            }

            return new Snapshot(
                Rows: rows,
                ByCode: rows.ToDictionary(r => r.Code, StringComparer.Ordinal),
                Instruments: instruments,
                AsOf: rows.Max(r => r.AsOf),
                UpdatedAt: rows.Max(r => r.UpdatedAt),
                Fundamentals: new Dictionary<string, Domain.Entities.Finance.FundamentalMetric>(StringComparer.Ordinal),
                AnnualFundamentals: new Dictionary<string, IReadOnlyList<Domain.Entities.Finance.FundamentalMetric>>(StringComparer.Ordinal),
                Dividends: new Dictionary<string, decimal?>(StringComparer.Ordinal),
                FundamentalsLoaded: false);
        }
    }
}
