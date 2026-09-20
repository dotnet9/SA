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
    public void Replace(IReadOnlyList<QuoteSnapshot> rows, IReadOnlyDictionary<string, Instrument> instruments) =>
        _current = Snapshot.Create(rows, instruments);

    /// <summary>
    /// 不可变快照。
    /// </summary>
    /// <param name="Rows">快照行（按代码升序）。</param>
    /// <param name="ByCode">代码到快照行的映射，供按代码取价。 </param>
    /// <param name="Instruments">代码到基础信息的映射。</param>
    /// <param name="AsOf">行情口径日。</param>
    /// <param name="UpdatedAt">写入时间。</param>
    public sealed record Snapshot(
        IReadOnlyList<QuoteSnapshot> Rows,
        IReadOnlyDictionary<string, QuoteSnapshot> ByCode,
        IReadOnlyDictionary<string, Instrument> Instruments,
        DateOnly AsOf,
        DateTimeOffset UpdatedAt)
    {
        /// <summary>空快照。</summary>
        public static Snapshot Empty { get; } = new(
            [],
            new Dictionary<string, QuoteSnapshot>(StringComparer.Ordinal),
            new Dictionary<string, Instrument>(StringComparer.Ordinal),
            DateOnly.MinValue,
            DateTimeOffset.MinValue);

        /// <summary>
        /// 由行集合构建快照：统一在这里推导口径日与写入时间，避免各调用点各写一遍。
        /// </summary>
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
                UpdatedAt: rows.Max(r => r.UpdatedAt));
        }
    }
}
