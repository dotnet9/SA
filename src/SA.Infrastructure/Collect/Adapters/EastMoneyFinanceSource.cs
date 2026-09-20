using System.Diagnostics;
using System.Text.Json;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Finance;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 东方财富财务报表（数据中心 <c>RPT_LICO_FN_CPD</c> 与 <c>RPT_PUBLIC_OP_NEWPREDICT</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 字段口径由 2026-09-20 的真实响应核对（300750 2026 半年报）：
/// <c>TOTAL_OPERATE_INCOME</c> 营业总收入（元）、<c>PARENT_NETPROFIT</c> 归母净利润（元）、
/// <c>YSTZ</c>/<c>SJLTZ</c> 营收/净利同比、<c>WEIGHTAVG_ROE</c> 加权 ROE、<c>XSMLL</c> 销售毛利率、
/// <c>BPS</c> 每股净资产、<c>MGJYXJJE</c> 每股经营现金流、<c>BASIC_EPS</c>/<c>DEDUCT_BASIC_EPS</c> 每股收益、
/// <c>ASSIGNDSCRPT</c> 分红方案、<c>ZXGXL</c> 股息率。
/// </para>
/// <para>
/// 同比与环比是<b>上游算好的</b>（不在本地重算）：上游用的是「调整后」的同口径数据，
/// 本地用两期相除会在会计政策变更、追溯调整时算错。
/// </para>
/// </remarks>
public sealed class EastMoneyFinanceSource(CollectHttpClient http) : IFinanceSource
{
    private const string BaseUrl = "https://datacenter-web.eastmoney.com/api/data/v1/get";

    /// <inheritdoc />
    public string Name => "东方财富 · 财务报表";

    /// <inheritdoc />
    public string Domains => "财务";

    /// <inheritdoc />
    public async Task<IReadOnlyList<FinancialReport>> GetReportsAsync(
        string code,
        int limit = 24,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 80);
        var url = $"{BaseUrl}?reportName=RPT_LICO_FN_CPD&columns=ALL" +
                  $"&filter={Filter("SECURITY_CODE", code)}" +
                  $"&pageSize={take}&pageNumber=1&sortColumns=REPORTDATE&sortTypes=-1";

        var root = await QueryAsync(url, code, cancellationToken).ConfigureAwait(false);
        var reports = ParseReports(code, root).ToList();

        // 上游按报告期倒序返回；入库与展示都用升序，便于按时间画图
        reports.Reverse();
        return reports;
    }

    /// <summary>
    /// 把上游响应解析成业绩报表（按报告期倒序，与上游一致）。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="root">响应根节点（也可以是 <c>result</c> 节点）。</param>
    internal static IEnumerable<FinancialReport> ParseReports(string code, JsonElement root)
    {
        foreach (var row in RowsOf(root))
        {
            // 报告期缺失的行无法归档，直接丢弃（上游偶有重复/占位行）
            if (JsonValueReader.Date(row, "REPORTDATE") is not { } reportDate)
            {
                continue;
            }

            yield return new FinancialReport
            {
                Code = code,
                ReportDate = reportDate,
                ReportType = JsonValueReader.Text(row, "DATATYPE"),
                Quarter = JsonValueReader.Text(row, "QDATE"),
                Revenue = JsonValueReader.Decimal(row, "TOTAL_OPERATE_INCOME"),
                RevenueYoy = JsonValueReader.Decimal(row, "YSTZ"),
                NetProfit = JsonValueReader.Decimal(row, "PARENT_NETPROFIT"),
                NetProfitYoy = JsonValueReader.Decimal(row, "SJLTZ"),
                Eps = JsonValueReader.Decimal(row, "BASIC_EPS"),
                DeductedEps = JsonValueReader.Decimal(row, "DEDUCT_BASIC_EPS"),
                Roe = JsonValueReader.Decimal(row, "WEIGHTAVG_ROE"),
                Bps = JsonValueReader.Decimal(row, "BPS"),
                OperatingCashFlowPerShare = JsonValueReader.Decimal(row, "MGJYXJJE"),
                GrossMargin = JsonValueReader.Decimal(row, "XSMLL"),
                RevenueQoq = JsonValueReader.Decimal(row, "YSHZ"),
                NetProfitQoq = JsonValueReader.Decimal(row, "SJLHZ"),
                DividendPlan = JsonValueReader.Text(row, "ASSIGNDSCRPT"),
                DividendYield = JsonValueReader.Decimal(row, "ZXGXL"),
                NoticeDate = JsonValueReader.Date(row, "NOTICE_DATE"),
                Industry = JsonValueReader.Text(row, "BOARD_NAME") ?? JsonValueReader.Text(row, "PUBLISHNAME"),
                UpdatedAt = SaTime.Now
            };
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EarningsForecast>> GetForecastsAsync(
        string code,
        int limit = 8,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 40);
        var url = $"{BaseUrl}?reportName=RPT_PUBLIC_OP_NEWPREDICT&columns=ALL" +
                  $"&filter={Filter("SECURITY_CODE", code)}" +
                  $"&pageSize={take}&pageNumber=1&sortColumns=REPORT_DATE&sortTypes=-1";

        var root = await QueryAsync(url, code, cancellationToken).ConfigureAwait(false);

        // 同一报告期收敛为「最新一次披露」（规则在领域层，见 EarningsForecastRules）
        return EarningsForecastRules.LatestPerReportDate(ParseForecasts(code, root)).ToList();
    }

    /// <summary>
    /// 把上游响应解析成业绩预告。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="root">响应根节点（也可以是 <c>result</c> 节点）。</param>
    internal static IEnumerable<EarningsForecast> ParseForecasts(string code, JsonElement root)
    {
        foreach (var row in RowsOf(root))
        {
            if (JsonValueReader.Date(row, "REPORT_DATE") is not { } reportDate)
            {
                continue;
            }

            yield return new EarningsForecast
            {
                Code = code,
                ReportDate = reportDate,
                Caliber = JsonValueReader.Text(row, "PREDICT_FINANCE"),
                ForecastType = JsonValueReader.Text(row, "PREDICT_TYPE"),
                Summary = JsonValueReader.Text(row, "PREDICT_CONTENT"),
                NetProfitMin = JsonValueReader.Decimal(row, "PREDICT_AMT_LOWER"),
                NetProfitMax = JsonValueReader.Decimal(row, "PREDICT_AMT_UPPER"),
                ChangeMin = JsonValueReader.Decimal(row, "ADD_AMP_LOWER"),
                ChangeMax = JsonValueReader.Decimal(row, "ADD_AMP_UPPER"),
                NoticeDate = JsonValueReader.Date(row, "NOTICE_DATE"),
                UpdatedAt = SaTime.Now
            };
        }
    }

    /// <summary>
    /// 从响应中取出数据行。
    /// </summary>
    /// <remarks>
    /// 同时接受根节点与 <c>result</c> 节点：调用方（以及测试）传哪一种都能用，
    /// 且 <c>result</c> 为 null 时返回空序列（新股无财报是合法状态，不是错误）。
    /// </remarks>
    private static IEnumerable<JsonElement> RowsOf(JsonElement root)
    {
        var node = root;
        if (JsonValueReader.TryGet(root, "result", out var result))
        {
            if (result.ValueKind != JsonValueKind.Object)
            {
                yield break;
            }

            node = result;
        }

        if (!JsonValueReader.TryGet(node, "data", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var row in rows.EnumerateArray())
        {
            yield return row;
        }
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var reports = await GetReportsAsync("300750", 4, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return reports.Count > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, reports.Count)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "未返回业绩报表");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 取上游响应并交给解析器；仅在响应结构本身不可用时抛错（无数据不算错）。
    /// </summary>
    private async Task<JsonElement> QueryAsync(
        string url,
        string code,
        CancellationToken cancellationToken)
    {
        using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (JsonValueReader.TryGet(document.RootElement, "result", out var result)
            && result.ValueKind == JsonValueKind.Object
            && JsonValueReader.TryGet(result, "data", out var rows)
            && rows.ValueKind != JsonValueKind.Array)
        {
            throw new CollectHttpException($"{Name} result.data 不是数组（code={code}）", null, null);
        }

        // 直接返回根节点：解析器负责处理 result 为 null / 缺 data 的情况
        return document.RootElement.Clone();
    }

    /// <summary>拼东财的 filter 片段：<c>(FIELD="VALUE")</c>。</summary>
    private static string Filter(string field, string value) =>
        $"({field}%3D%22{Uri.EscapeDataString(value)}%22)";
}
