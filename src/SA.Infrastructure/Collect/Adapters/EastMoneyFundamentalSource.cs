using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Finance;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 东方财富基本面指标（<c>datacenter-web</c> 的 <c>RPT_F10_FINANCE_MAINFINADATA</c>）。
/// </summary>
/// <remarks>
/// <para>
/// <b>一次请求给齐 100+ 个因子</b>（实测 2026-09-21：165 个字段），且<b>支持按报告期扫全市场</b>：
/// <c>filter=(REPORT_DATE='2026-06-30')</c> 共 27 页 / 13,420 行 / 约 2 MB，
/// 因此全市场基本面采集是 27 次请求而不是 5,900 次（实施计划 §2.3）。
/// </para>
/// <para>
/// <b>必须过滤非 A 股标的</b>（实施计划 §2.3 未提及、实测发现的第 5 个坑）：
/// 13,420 行里只有 5,832 行是真上市 A 股。其余构成：
/// </para>
/// <list type="bullet">
/// <item><c>.NQ</c> 后缀 6,852 行 —— 新三板，含 <c>400xxx</c> 老三板退市股（如 <c>400016 金田A3</c>）；</item>
/// <item>非 6 位数字代码 736 行 —— IPO 申报主体（如 <c>A26229 云豹智能</c>、<c>A22444 联亚药业</c>），
/// 尚未上市，但已披露财务数据。</item>
/// </list>
/// <para>
/// 判据用「<c>SECUCODE</c> 后缀 ∈ {SH, SZ, BJ} 且代码为 6 位数字」，实测恰好得到
/// 沪 2,441 + 深 3,039 + 北 352 = 5,832 只。
/// <b>不能复用 <see cref="MarketCodes.IsStockCode"/></b>：它的兜底分支会把 <c>400016</c> 判成「深市主板」。
/// </para>
/// <para>
/// 上游的 <c>ROEKCJQ</c> 等字段对金融业为 <c>null</c> 是<b>行业口径不同</b>而不是数据缺失，
/// 本类照原样落 null，由上层（选股器口径提示、界面三态）区分「不适用」与「暂无数据」。
/// </para>
/// </remarks>
public sealed class EastMoneyFundamentalSource(
    CollectHttpClient http,
    ILogger<EastMoneyFundamentalSource> logger) : IFundamentalSource
{
    private const string BaseUrl = "https://datacenter-web.eastmoney.com/api/data/v1/get";

    private const string ReportName = "RPT_F10_FINANCE_MAINFINADATA";

    /// <summary>单页行数（实测 500 可接受，一个报告期 27 页）。</summary>
    private const int PageSize = 500;

    /// <summary>安全上限：实测 27 页，留出余量防止上游 total 异常导致无限翻页。</summary>
    private const int MaxPages = 80;

    /// <summary>只看 A 股三个交易所的后缀。</summary>
    private static readonly string[] ListedSuffixes = ["SH", "SZ", "BJ"];

    /// <inheritdoc />
    public string Name => "东方财富 · 基本面指标";

    /// <inheritdoc />
    public string Domains => "财务,选股";

    /// <inheritdoc />
    public async Task<DateOnly?> GetLatestReportDateAsync(CancellationToken cancellationToken = default)
    {
        // 不带 REPORT_DATE 过滤、按报告期倒序取 1 行：首行即最新已披露报告期。
        // 不用「距今最近的季末」推断——披露季里那个季末可能一行数据都没有。
        var url = $"{BaseUrl}?reportName={ReportName}&columns=REPORT_DATE&pageSize=1&pageNumber=1" +
                  "&sortColumns=REPORT_DATE&sortTypes=-1";

        using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

        foreach (var row in RowsOf(document.RootElement))
        {
            if (JsonValueReader.Date(row, "REPORT_DATE") is { } date)
            {
                return date;
            }
        }

        logger.LogWarning("{Source} 未返回任何报告期", Name);
        return null;
    }

    /// <inheritdoc />
    public async Task<(int Rows, int Total)> GetByReportDateAsync(
        DateOnly reportDate,
        Func<IReadOnlyList<FundamentalMetric>, Task>? onPage = null,
        CancellationToken cancellationToken = default)
    {
        var rows = 0;
        var total = 0;
        var skipped = 0;
        var filter = $"(REPORT_DATE%3D%27{reportDate:yyyy-MM-dd}%27)";

        for (var page = 1; page <= MaxPages; page++)
        {
            var url = $"{BaseUrl}?reportName={ReportName}&columns=ALL&filter={filter}" +
                      $"&pageSize={PageSize}&pageNumber={page}&sortColumns=SECUCODE&sortTypes=1";

            using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!JsonValueReader.TryGet(document.RootElement, "result", out var result)
                || result.ValueKind != JsonValueKind.Object
                || !JsonValueReader.TryGet(result, "data", out var data)
                || data.ValueKind != JsonValueKind.Array)
            {
                // 上游在参数异常时返回 result:null，那不是「没有数据」而是「这页没拿到」
                throw new CollectHttpException($"{Name} 第 {page} 页缺少 result.data（{reportDate:yyyy-MM-dd}）", null, null);
            }

            total = JsonValueReader.Int(result, "count") ?? total;

            var pageRows = new List<FundamentalMetric>(data.GetArrayLength());
            foreach (var element in data.EnumerateArray())
            {
                var metric = Parse(element, reportDate);
                if (metric is null)
                {
                    skipped++;
                    continue;
                }

                pageRows.Add(metric);
            }

            rows += pageRows.Count;
            if (pageRows.Count > 0 && onPage is not null)
            {
                await onPage(pageRows).ConfigureAwait(false);
            }

            if (data.GetArrayLength() < PageSize)
            {
                break;
            }
        }

        logger.LogInformation(
            "{Source} {ReportDate:yyyy-MM-dd} 扫描完成：{Rows} 只 A 股（上游 {Total} 行，过滤掉 {Skipped} 行非 A 股）",
            Name, reportDate, rows, total, skipped);

        return (rows, total);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FundamentalMetric>> GetHistoryAsync(
        string code,
        int periods = 40,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(periods, 1, 120);
        var url = $"{BaseUrl}?reportName={ReportName}&columns=ALL" +
                  $"&filter=(SECUCODE%3D%22{Uri.EscapeDataString(MarketCodes.SecUCode(code))}%22)" +
                  $"&pageSize={take}&pageNumber=1&sortColumns=REPORT_DATE&sortTypes=-1";

        using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

        var metrics = new List<FundamentalMetric>();
        foreach (var row in RowsOf(document.RootElement))
        {
            var metric = Parse(row, null);
            if (metric is not null)
            {
                metrics.Add(metric);
            }
        }

        // 上游按报告期倒序返回；入库与画图都用升序
        metrics.Reverse();
        return metrics;
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var latest = await GetLatestReportDateAsync(cancellationToken).ConfigureAwait(false);
            if (latest is null)
            {
                stopwatch.Stop();
                return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "未返回任何报告期");
            }

            // 探针只取一页：完整扫描是 27 页，不该在探活里跑完
            var url = $"{BaseUrl}?reportName={ReportName}&columns=SECUCODE,SECURITY_CODE,ROEJQ" +
                      $"&filter=(REPORT_DATE%3D%27{latest.Value:yyyy-MM-dd}%27)&pageSize=50&pageNumber=1";
            using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

            var rows = RowsOf(document.RootElement).Count(row => Parse(row, null) is not null);
            stopwatch.Stop();

            return rows > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, rows)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, $"{latest:yyyy-MM-dd} 无可解析的 A 股行");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 解析一行。<paramref name="reportDateFallback"/> 为 null 时用上游的 <c>REPORT_DATE</c>。
    /// </summary>
    /// <remarks>
    /// 返回 null 表示该行不是上市 A 股（新三板 / IPO 申报 / 无代码），调用方按「过滤掉」处理。
    /// <c>PRATIO</c> 刻意不解析：口径未确认（实施计划 §2.3 第 4 条）。
    /// </remarks>
    internal static FundamentalMetric? Parse(JsonElement element, DateOnly? reportDateFallback)
    {
        var securityCode = JsonValueReader.Text(element, "SECURITY_CODE");
        var secUCode = JsonValueReader.Text(element, "SECUCODE");

        if (!IsListedAShare(securityCode, secUCode))
        {
            return null;
        }

        var reportDate = reportDateFallback ?? JsonValueReader.Date(element, "REPORT_DATE");
        if (reportDate is null)
        {
            return null;
        }

        return new FundamentalMetric
        {
            Code = securityCode!.Trim(),
            ReportDate = reportDate.Value,
            ReportType = JsonValueReader.Text(element, "REPORT_TYPE"),
            OrgType = JsonValueReader.Text(element, "ORG_TYPE"),
            NoticeDate = JsonValueReader.Date(element, "NOTICE_DATE"),

            RoeWeighted = JsonValueReader.Decimal(element, "ROEJQ"),
            RoeDeducted = JsonValueReader.Decimal(element, "ROEKCJQ"),
            GrossMargin = JsonValueReader.Decimal(element, "XSMLL"),
            NetMargin = JsonValueReader.Decimal(element, "XSJLL"),
            Roic = JsonValueReader.Decimal(element, "ROIC"),
            OperatingCashFlowToRevenue = JsonValueReader.Decimal(element, "JYXJLYYSR"),
            OperatingCashFlow = JsonValueReader.Decimal(element, "NETCASH_OPERATE_PK"),
            OperatingCashFlowToNetProfit = JsonValueReader.Decimal(element, "NCO_NETPROFIT"),
            OperatingCashFlowToOperatingProfit = JsonValueReader.Decimal(element, "NCO_OP"),
            FreeCashFlow = JsonValueReader.Decimal(element, "FCFF_FORWARD"),

            DebtRatio = JsonValueReader.Decimal(element, "ZCFZL"),
            CurrentRatio = JsonValueReader.Decimal(element, "LD"),
            QuickRatio = JsonValueReader.Decimal(element, "SD"),
            InterestDebtRatio = JsonValueReader.Decimal(element, "INTEREST_DEBT_RATIO"),
            InterestCoverageRatio = JsonValueReader.Decimal(element, "INTSTCOVRATE"),
            LiquidationRatio = JsonValueReader.Decimal(element, "LIQUIDATION_RATIO"),

            InventoryTurnoverDays = JsonValueReader.Decimal(element, "CHZZTS"),
            ReceivableTurnoverDays = JsonValueReader.Decimal(element, "YSZKZZTS"),
            AssetTurnoverDays = JsonValueReader.Decimal(element, "ZZCZZTS"),

            RevenueYoy = JsonValueReader.Decimal(element, "TOTALOPERATEREVETZ"),
            NetProfitYoy = JsonValueReader.Decimal(element, "PARENTNETPROFITTZ"),
            DeductedNetProfitYoy = JsonValueReader.Decimal(element, "KCFJCXSYJLRTZ"),

            Eps = JsonValueReader.Decimal(element, "EPSJB"),
            EpsDeducted = JsonValueReader.Decimal(element, "EPSKCJB"),
            Bps = JsonValueReader.Decimal(element, "BPS"),
            OperatingCashFlowPerShare = JsonValueReader.Decimal(element, "MGJYXJJE"),

            RndExpense = JsonValueReader.Decimal(element, "RDEXPEND"),
            RndExpenseRatio = JsonValueReader.Decimal(element, "RE_RATIO_PK"),
            RndPersonnel = JsonValueReader.Decimal(element, "RDPERSONNEL"),

            Revenue = JsonValueReader.Decimal(element, "TOTALOPERATEREVE"),
            NetProfit = JsonValueReader.Decimal(element, "PARENTNETPROFIT"),
            TotalAssets = JsonValueReader.Decimal(element, "TOTAL_ASSETS_PK"),
            TotalEquity = JsonValueReader.Decimal(element, "TOTAL_EQUITY_PK"),
            Liability = JsonValueReader.Decimal(element, "LIABILITY"),
            TotalShare = JsonValueReader.Decimal(element, "TOTAL_SHARE"),
            FreeShare = JsonValueReader.Decimal(element, "A_FREE_SHARE"),
            StaffNumber = JsonValueReader.Decimal(element, "STAFF_NUM"),

            UpdatedAt = SaTime.Now
        };
    }

    /// <summary>
    /// 判断一行是否为「上市 A 股」。
    /// </summary>
    /// <remarks>
    /// 两个条件都要满足：<c>SECUCODE</c> 后缀属于沪/深/北，且代码是 6 位数字。
    /// 只看代码位数会把 <c>400016</c>（老三板退市股）算进来；只看后缀会把
    /// <c>A23295</c>（IPO 申报主体）算进来——两者实测都存在。
    /// </remarks>
    internal static bool IsListedAShare(string? securityCode, string? secUCode)
    {
        if (string.IsNullOrWhiteSpace(securityCode) || string.IsNullOrWhiteSpace(secUCode))
        {
            return false;
        }

        var code = securityCode.Trim();
        if (code.Length != 6 || !code.All(char.IsAsciiDigit))
        {
            return false;
        }

        var dot = secUCode.LastIndexOf('.');
        if (dot < 0 || dot == secUCode.Length - 1)
        {
            return false;
        }

        var suffix = secUCode[(dot + 1)..].Trim();
        return Array.Exists(ListedSuffixes, s => s.Equals(suffix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>从响应中取出数据行（兼容根节点与 <c>result</c> 节点，供测试复用）。</summary>
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
}
