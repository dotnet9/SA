using System.Diagnostics;
using System.Text.Json;
using SA.Application.Abstractions;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 东方财富两融余额（数据中心 <c>RPTA_WEB_RZRQ_LSSH</c>，沪深两市历史）。
/// </summary>
/// <remarks>
/// <para>
/// 两融数据按市场逐条披露，且沪市先出：实测 2026-09-20 查询时，最新交易日 2026-09-18
/// <b>只有沪证一行</b>，深证尚未发布。若直接取「最新日期」，融资余额会少算约一半
/// （13,346 亿 vs 26,015 亿）。
/// </para>
/// <para>
/// 因此实现取「沪证（SCDM=007）与深证（SCDM=001）均已出现的最近交易日」，
/// 并把两市场求和。北交所（SCDM=002）规模小且披露更晚，按口径说明排除。
/// </para>
/// </remarks>
public sealed class EastMoneyMarginMarketSource(CollectHttpClient http) : IMarginMarketSource
{
    private const string Shanghai = "007";
    private const string Shenzhen = "001";
    private const string BaseUrl = "https://datacenter-web.eastmoney.com/api/data/v1/get"
        + "?reportName=RPTA_WEB_RZRQ_LSSH&columns=ALL&pageSize=60&pageNumber=1"
        + "&sortColumns=DIM_DATE&sortTypes=-1";

    /// <inheritdoc />
    public string Name => "东方财富 · 两融余额";

    /// <inheritdoc />
    public string Domains => "资金,两融";

    /// <inheritdoc />
    public async Task<MarginMarketResult?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        using var document = await http.GetJsonAsync(BaseUrl, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!JsonValueReader.TryGet(document.RootElement, "result", out var result)
            || !JsonValueReader.TryGet(result, "data", out var rows)
            || rows.ValueKind != JsonValueKind.Array)
        {
            throw new CollectHttpException($"{Name} 缺少 result.data", null, null);
        }

        // 按日期分组，找出沪深均已披露的最近一个交易日
        var candidates = new List<MarginRow>();
        foreach (var element in rows.EnumerateArray())
        {
            var date = JsonValueReader.Date(element, "DIM_DATE");
            var market = JsonValueReader.Text(element, "SCDM");
            if (date is null || market is null)
            {
                continue;
            }

            candidates.Add(new MarginRow(
                date.Value,
                market,
                JsonValueReader.DecimalOrZero(element, "RZYE"),
                JsonValueReader.DecimalOrZero(element, "RQYE")));
        }

        return SelectLatestComplete(candidates);
    }

    /// <summary>
    /// 两融原始行。
    /// </summary>
    /// <param name="Date">口径日。</param>
    /// <param name="Market">市场代码（007 沪证 / 001 深证 / 002 京证）。</param>
    /// <param name="FinanceBalance">融资余额（元）。</param>
    /// <param name="LoanBalance">融券余额（元）。</param>
    internal readonly record struct MarginRow(DateOnly Date, string Market, decimal FinanceBalance, decimal LoanBalance);

    /// <summary>
    /// 取「沪证与深证均已出现」的最近交易日，并把两市场求和。
    /// </summary>
    /// <remarks>
    /// 不能直接取最大日期：实测 2026-09-18 当日只有沪证一行（深证次日才补），
    /// 直接取最新日会把融资余额少算约一半（13,346 亿 vs 26,015 亿）。
    /// </remarks>
    internal static MarginMarketResult? SelectLatestComplete(IEnumerable<MarginRow> rows)
    {
        var byDate = new Dictionary<DateOnly, (decimal Finance, decimal Loan, bool HasSh, bool HasSz)>();

        foreach (var row in rows)
        {
            var (finance, loan, hasSh, hasSz) = byDate.TryGetValue(row.Date, out var existing)
                ? existing
                : (0m, 0m, false, false);

            if (row.Market == Shanghai)
            {
                byDate[row.Date] = (finance + row.FinanceBalance, loan + row.LoanBalance, true, hasSz);
            }
            else if (row.Market == Shenzhen)
            {
                byDate[row.Date] = (finance + row.FinanceBalance, loan + row.LoanBalance, hasSh, true);
            }
        }

        var complete = byDate
            .Where(kv => kv.Value.HasSh && kv.Value.HasSz)
            .OrderByDescending(kv => kv.Key)
            .Select(kv => (Date: kv.Key, kv.Value.Finance, kv.Value.Loan))
            .FirstOrDefault();

        return complete.Date == default
            ? null
            : new MarginMarketResult(complete.Date, complete.Finance, complete.Loan);
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await GetLatestAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return result is not null
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, 1)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "未取到沪深齐备的两融日期");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }
}
