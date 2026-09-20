using SA.Domain.History;

namespace SA.Application.Abstractions;

/// <summary>
/// 日线历史的读写。实现为 Parquet（每标的一份文件）+ DuckDB 查询。
/// </summary>
/// <remarks>
/// 约定（实施计划 §5.3 / 详细设计 §4）：
/// <list type="bullet">
/// <item>写入按「标的」整体合并后覆盖该标的的文件，天然幂等，不产生逐行更新；</item>
/// <item>读取一律按标的取，且只取需要的行数（K 线上限 240 根）；</item>
/// <item>数据集之间用不同的目录区分，互不影响。</item>
/// </list>
/// </remarks>
public interface IDailyHistoryStore
{
    /// <summary>取某标的已入库的最后一个交易日；从未入库返回 null。</summary>
    Task<DateOnly?> GetLastDateAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取某标的最近 <paramref name="limit"/> 根日线，按日期升序返回。
    /// </summary>
    Task<IReadOnlyList<DailyBar>> GetLatestAsync(string code, int limit, CancellationToken cancellationToken = default);

    /// <summary>取某标的在给定区间内的日线，按日期升序返回。</summary>
    Task<IReadOnlyList<DailyBar>> GetRangeAsync(
        string code,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按日期合并写入（同日以新数据为准），返回合并后的总行数。
    /// </summary>
    Task<int> MergeAsync(string code, IReadOnlyList<DailyBar> bars, CancellationToken cancellationToken = default);

    /// <summary>已入库的标的数量。</summary>
    Task<int> CountCodesAsync(CancellationToken cancellationToken = default);

    /// <summary>删除某标的的全部历史（除权重算时使用）。</summary>
    Task DeleteAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>
/// 指标历史的读写。契约与日线一致，只是列不同。
/// </summary>
public interface IIndicatorStore
{
    /// <summary>取某标的已入库的最后一个交易日；从未入库返回 null。</summary>
    Task<DateOnly?> GetLastDateAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>取某标的最近 <paramref name="limit"/> 行指标，按日期升序返回。</summary>
    Task<IReadOnlyList<IndicatorRow>> GetLatestAsync(string code, int limit, CancellationToken cancellationToken = default);

    /// <summary>按日期合并写入，返回合并后的总行数。</summary>
    Task<int> MergeAsync(string code, IReadOnlyList<IndicatorRow> rows, CancellationToken cancellationToken = default);

    /// <summary>删除某标的的全部指标（除权重算与全量重算时使用）。</summary>
    Task DeleteAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>
/// 回补断点。对应新增表 <c>SyncCursor</c>（实施计划 §5.3）：按数据集 + 标的记录进度，
/// 让回补可以跨进程重启续跑，而不是每次从头再来。
/// </summary>
public interface ISyncCursorStore
{
    /// <summary>取某数据集全部游标。</summary>
    Task<IReadOnlyList<Domain.Entities.Collect.SyncCursor>> GetByDatasetAsync(
        string dataset,
        CancellationToken cancellationToken = default);

    /// <summary>取单个游标。</summary>
    Task<Domain.Entities.Collect.SyncCursor?> FindAsync(
        string dataset,
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>写入或更新游标。</summary>
    Task UpsertAsync(Domain.Entities.Collect.SyncCursor cursor, CancellationToken cancellationToken = default);

    /// <summary>批量写入或更新游标（一次回补批次结束后统一落库）。</summary>
    Task UpsertManyAsync(
        IReadOnlyList<Domain.Entities.Collect.SyncCursor> cursors,
        CancellationToken cancellationToken = default);
}
