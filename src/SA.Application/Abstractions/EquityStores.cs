using SA.Domain.Entities.Equity;

namespace SA.Application.Abstractions;

/// <summary>
/// 股权结构数据源：十大股东 / 十大流通股东 / 股东户数 / 股权质押。
/// </summary>
/// <remarks>
/// 四个数据集来自同一数据中心（东财 F10），但分属不同报表，因此各自取数、各自可失败：
/// 某个报表拿不到不应让整页没有数据（页面按区块降级）。
/// </remarks>
public interface IEquitySource : IProbeable
{
    /// <summary>
    /// 取十大股东（含流通口径）。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="periods">取最近多少个报告期。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<TopHolder>> GetTopHoldersAsync(
        string code,
        int periods = 2,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取股东户数历史（按报告期倒序）。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="periods">取最近多少期。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<HolderCount>> GetHolderCountsAsync(
        string code,
        int periods = 12,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取最新的股权质押数据。
    /// </summary>
    Task<PledgeStat?> GetPledgeAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>
/// 股权结构存储。
/// </summary>
public interface IEquityStore
{
    /// <summary>取十大股东（按报告期倒序、排名升序）。</summary>
    Task<IReadOnlyList<TopHolder>> GetTopHoldersAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>取股东户数历史（按报告期升序，便于画趋势）。</summary>
    Task<IReadOnlyList<HolderCount>> GetHolderCountsAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>取最新股权质押。</summary>
    Task<PledgeStat?> GetPledgeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>写入十大股东（按 代码+报告期+排名+口径 upsert）。</summary>
    Task<int> UpsertTopHoldersAsync(IReadOnlyList<TopHolder> holders, CancellationToken cancellationToken = default);

    /// <summary>写入股东户数（按 代码+报告期 upsert）。</summary>
    Task<int> UpsertHolderCountsAsync(IReadOnlyList<HolderCount> counts, CancellationToken cancellationToken = default);

    /// <summary>写入股权质押（按 代码+日期 upsert）。</summary>
    Task<int> UpsertPledgeAsync(PledgeStat pledge, CancellationToken cancellationToken = default);

    /// <summary>最近写入时间（用于展示数据时间）。</summary>
    Task<DateTimeOffset?> GetLastUpdatedAtAsync(string code, CancellationToken cancellationToken = default);
}
