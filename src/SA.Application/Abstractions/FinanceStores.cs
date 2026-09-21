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

    /// <summary>
    /// 取全市场「每只标的最新一期股息率」。
    /// </summary>
    /// <remarks>
    /// 股息率只在业绩报表（<c>RPT_LICO_FN_CPD</c> 的 <c>ZXGXL</c>）里，
    /// 基本面报表 <c>RPT_F10_FINANCE_MAINFINADATA</c> 不含该字段，因此选股器要单独取一次。
    /// 每只标的取<b>最新报告期</b>的那一条（股息率随分红方案更新）。
    /// </remarks>
    Task<IReadOnlyDictionary<string, decimal?>> GetLatestDividendYieldsAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 基本面指标源（<c>RPT_F10_FINANCE_MAINFINADATA</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="IFinanceSource"/> 的分工：后者按<b>证券代码</b>逐只拉业绩报表，
/// 本接口按<b>报告期</b>扫全市场（实测一个报告期 27 页 / 13,420 行），
/// 因此它是「横截面因子表」的来源，用于选股器与价值研究的历史序列。
/// </para>
/// <para>
/// <b>必须过滤掉非 A 股标的</b>：实测一个报告期返回的 13,420 行里，
/// 只有 5,832 行是真上市 A 股（沪 2,441 / 深 3,039 / 北 352），
/// 其余是新三板（<c>.NQ</c> 后缀 6,852 行，含 <c>400xxx</c> 老三板退市股）
/// 与 IPO 申报主体（736 行，代码形如 <c>A26229</c>，尚未上市）。
/// 不过滤会让选股器把未上市公司与退市股混进结果。
/// </para>
/// </remarks>
public interface IFundamentalSource : IProbeable
{
    /// <summary>
    /// 取「最新已披露报告期」。
    /// </summary>
    /// <remarks>
    /// 实测发现方式：不带 <c>REPORT_DATE</c> 过滤、按 <c>REPORT_DATE</c> 倒序取 1 行，
    /// 首行的报告期即最新已披露期（与「距今最近的季末」不同——后者在披露季会是空的）。
    /// </remarks>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>最新报告期；上游无任何数据时返回 null。</returns>
    Task<DateOnly?> GetLatestReportDateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 扫描一个报告期的全市场基本面指标。
    /// </summary>
    /// <param name="reportDate">报告期。</param>
    /// <param name="onPage">每页回调，便于边拉边写。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>行数与上游声明的总数。</returns>
    Task<(int Rows, int Total)> GetByReportDateAsync(
        DateOnly reportDate,
        Func<IReadOnlyList<FundamentalMetric>, Task>? onPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取单只个股的历史报告期序列（用于「周期位置」判断）。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="periods">最多返回多少期（按报告期倒序取，返回时按报告期升序）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<FundamentalMetric>> GetHistoryAsync(
        string code,
        int periods = 40,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 基本面指标存储。按（代码, 报告期）upsert。
/// </summary>
public interface IFundamentalStore
{
    /// <summary>写入或覆盖基本面指标，返回写出行数。</summary>
    Task<int> UpsertAsync(
        IReadOnlyList<FundamentalMetric> metrics,
        CancellationToken cancellationToken = default);

    /// <summary>取某标的的全部报告期（按报告期升序）。</summary>
    Task<IReadOnlyList<FundamentalMetric>> GetHistoryAsync(
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取全市场「每个代码的最新报告期」。
    /// </summary>
    /// <remarks>
    /// 选股器需要的是「最新一期」，而表里同时存着全市场快照与单只历史序列，
    /// 因此必须按代码取最大报告期，不能直接取「某个报告期的全部行」
    /// （历史序列会让同一代码出现多行）。
    /// </remarks>
    Task<IReadOnlyDictionary<string, FundamentalMetric>> GetLatestPerCodeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取全部年报（<c>REPORT_TYPE = 年报</c>）序列，供连续性条件使用。
    /// </summary>
    /// <param name="codes">限定代码集合；为空表示全市场。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyDictionary<string, IReadOnlyList<FundamentalMetric>>> GetAnnualByCodeAsync(
        IReadOnlyCollection<string>? codes = null,
        CancellationToken cancellationToken = default);

    /// <summary>该报告期是否已有数据（用于跳过重复扫描）。</summary>
    Task<bool> HasReportDateAsync(DateOnly reportDate, CancellationToken cancellationToken = default);

    /// <summary>已落库的报告期数量。</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
