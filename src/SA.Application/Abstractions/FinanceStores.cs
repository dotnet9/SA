using SA.Domain.Entities.Finance;

namespace SA.Application.Abstractions;

/// <summary>
/// 财务报表源（业绩报表与业绩预告）。
/// </summary>
/// <remarks>
/// 数据来自东财数据中心的公开报表接口，按证券代码过滤。
/// 报告期是「按季度累积」的口径（半年报即上半年累计），接口层不再做单季拆分，
/// 以免与用户对「半年报营收」的直觉不一致。
/// </remarks>
public interface IFinanceSource : IProbeable
{
    /// <summary>
    /// 取某标的的业绩报表。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="limit">最多返回多少期（按报告期倒序取，返回时按报告期升序）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<FinancialReport>> GetReportsAsync(
        string code,
        int limit = 24,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取某标的的业绩预告。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="limit">最多返回多少条。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<EarningsForecast>> GetForecastsAsync(
        string code,
        int limit = 8,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 财务报表存储。按（代码, 报告期）upsert，重复采集即覆盖。
/// </summary>
public interface IFinanceStore
{
    /// <summary>取某标的的业绩报表（按报告期升序）。</summary>
    Task<IReadOnlyList<FinancialReport>> GetReportsAsync(
        string code,
        int limit = 24,
        CancellationToken cancellationToken = default);

    /// <summary>取某标的的业绩预告（按报告期倒序）。</summary>
    Task<IReadOnlyList<EarningsForecast>> GetForecastsAsync(
        string code,
        int limit = 8,
        CancellationToken cancellationToken = default);

    /// <summary>写入或覆盖业绩报表，返回写出行数。</summary>
    Task<int> UpsertReportsAsync(
        IReadOnlyList<FinancialReport> reports,
        CancellationToken cancellationToken = default);

    /// <summary>写入或覆盖业绩预告，返回写出行数。</summary>
    Task<int> UpsertForecastsAsync(
        IReadOnlyList<EarningsForecast> forecasts,
        CancellationToken cancellationToken = default);

    /// <summary>该标的最近一次写入时间；从未采集返回 null。</summary>
    Task<DateTimeOffset?> GetLastUpdatedAtAsync(string code, CancellationToken cancellationToken = default);
}
