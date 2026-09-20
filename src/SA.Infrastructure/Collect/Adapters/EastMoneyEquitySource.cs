using System.Diagnostics;
using System.Text.Json;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Equity;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 东方财富股权结构（数据中心 F10 报表）。
/// </summary>
/// <remarks>
/// <para>
/// 覆盖四张报表：<c>RPT_F10_EH_HOLDERS</c>（十大股东）、<c>RPT_F10_EH_FREEHOLDERS</c>（十大流通股东）、
/// <c>RPT_HOLDERNUM_DET</c>（股东户数历史）、<c>RPT_CSDC_LIST_NEWEST</c>（股权质押）。
/// </para>
/// <para>
/// <b>两个实测坑</b>：
/// 一是十大股东系列只接受带市场后缀的 <c>SECUCODE</c>（<c>300750.SZ</c>），用 <c>SECURITY_CODE</c> 过滤会返回空；
/// 二是股东户数的复权/市值字段单位各不相同（户均市值为元、质押股数为万股、质押市值为万元），
/// 已在实体注释中逐项核对，不在适配器里做换算，避免单位在两层之间被换两次。
/// </para>
/// </remarks>
public sealed class EastMoneyEquitySource(CollectHttpClient http) : IEquitySource
{
    private const string BaseUrl = "https://datacenter-web.eastmoney.com/api/data/v1/get";

    /// <inheritdoc />
    public string Name => "东方财富 · 股权结构";

    /// <inheritdoc />
    public string Domains => "股权";

    /// <inheritdoc />
    public async Task<IReadOnlyList<TopHolder>> GetTopHoldersAsync(
        string code,
        int periods = 2,
        CancellationToken cancellationToken = default)
    {
        var secucode = MarketCodes.SecUCode(code);
        var take = Math.Clamp(periods * 10, 10, 60);

        var hold = await QueryAsync(
            $"{BaseUrl}?reportName=RPT_F10_EH_HOLDERS&columns=ALL" +
            $"&filter={Filter("SECUCODE", secucode)}&pageSize={take}&pageNumber=1" +
            "&sortColumns=END_DATE,HOLDER_RANK&sortTypes=-1,1",
            code,
            cancellationToken).ConfigureAwait(false);

        var free = await QueryAsync(
            $"{BaseUrl}?reportName=RPT_F10_EH_FREEHOLDERS&columns=ALL" +
            $"&filter={Filter("SECUCODE", secucode)}&pageSize={take}&pageNumber=1" +
            "&sortColumns=END_DATE,HOLDER_RANK&sortTypes=-1,1",
            code,
            cancellationToken).ConfigureAwait(false);

        var rows = ParseTopHolders(code, hold, isFreeFloat: false)
            .Concat(ParseTopHolders(code, free, isFreeFloat: true))
            .ToList();

        // 只保留最近 periods 个报告期，避免把历史期全部写库
        var keep = rows.Select(row => row.EndDate).Distinct().OrderByDescending(date => date).Take(Math.Max(1, periods)).ToHashSet();
        return rows.Where(row => keep.Contains(row.EndDate)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HolderCount>> GetHolderCountsAsync(
        string code,
        int periods = 12,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(periods, 1, 60);
        var rows = await QueryAsync(
            $"{BaseUrl}?reportName=RPT_HOLDERNUM_DET&columns=ALL" +
            $"&filter={Filter("SECURITY_CODE", code)}&pageSize={take}&pageNumber=1" +
            "&sortColumns=END_DATE&sortTypes=-1",
            code,
            cancellationToken).ConfigureAwait(false);

        return ParseHolderCounts(code, rows).ToList();
    }

    /// <inheritdoc />
    public async Task<PledgeStat?> GetPledgeAsync(string code, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync(
            $"{BaseUrl}?reportName=RPT_CSDC_LIST_NEWEST&columns=ALL" +
            $"&filter={Filter("SECURITY_CODE", code)}&pageSize=1&pageNumber=1",
            code,
            cancellationToken).ConfigureAwait(false);

        return ParsePledge(code, rows).FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var holders = await GetTopHoldersAsync("300750", 1, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return holders.Count > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, holders.Count)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "未返回十大股东");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }

    /* ------------------------------------------------------------------
       解析（internal 供测试用真实样本固化字段映射）
       ------------------------------------------------------------------ */

    /// <summary>解析十大股东 / 十大流通股东。</summary>
    internal static IEnumerable<TopHolder> ParseTopHolders(string code, IEnumerable<JsonElement> rows, bool isFreeFloat)
    {
        foreach (var row in rows)
        {
            if (JsonValueReader.Date(row, "END_DATE") is not { } endDate)
            {
                continue;
            }

            var name = JsonValueReader.Text(row, "HOLDER_NAME");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            yield return new TopHolder
            {
                Code = code,
                EndDate = endDate,
                Rank = JsonValueReader.IntOrZero(row, "HOLDER_RANK"),
                IsFreeFloat = isFreeFloat,
                HolderName = name,
                HoldNum = JsonValueReader.DecimalOrZero(row, "HOLD_NUM"),
                // 两种口径的总股本占比字段名不同：全量用 HOLD_NUM_RATIO，流通用 FREE_HOLDNUM_RATIO
                HoldRatio = isFreeFloat
                    ? JsonValueReader.Decimal(row, "HOLD_RATIO") ?? JsonValueReader.Decimal(row, "HOLD_NUM_RATIO")
                    : JsonValueReader.Decimal(row, "HOLD_NUM_RATIO"),
                FreeHoldRatio = isFreeFloat ? JsonValueReader.Decimal(row, "FREE_HOLDNUM_RATIO") : null,
                HoldChange = JsonValueReader.Text(row, "HOLD_NUM_CHANGE") ?? JsonValueReader.Text(row, "HOLD_CHANGE"),
                HolderType = JsonValueReader.Text(row, "HOLDER_TYPE"),
                SharesType = JsonValueReader.Text(row, "SHARES_TYPE"),
                MarketCap = JsonValueReader.Decimal(row, "HOLDER_MARKET_CAP"),
                NoticeDate = JsonValueReader.Date(row, "NOTICE_DATE") ?? JsonValueReader.Date(row, "UPDATE_DATE"),
                UpdatedAt = SaTime.Now
            };
        }
    }

    /// <summary>解析股东户数。</summary>
    internal static IEnumerable<HolderCount> ParseHolderCounts(string code, IEnumerable<JsonElement> rows)
    {
        foreach (var row in rows)
        {
            if (JsonValueReader.Date(row, "END_DATE") is not { } endDate)
            {
                continue;
            }

            yield return new HolderCount
            {
                Code = code,
                EndDate = endDate,
                HolderNum = JsonValueReader.IntOrZero(row, "HOLDER_NUM"),
                PreviousHolderNum = JsonValueReader.Int(row, "PRE_HOLDER_NUM"),
                HolderNumChange = JsonValueReader.Int(row, "HOLDER_NUM_CHANGE"),
                HolderNumRatio = JsonValueReader.Decimal(row, "HOLDER_NUM_RATIO"),
                AvgHoldNum = JsonValueReader.Decimal(row, "AVG_HOLD_NUM"),
                AvgMarketCap = JsonValueReader.Decimal(row, "AVG_MARKET_CAP"),
                TotalMarketCap = JsonValueReader.Decimal(row, "TOTAL_MARKET_CAP"),
                TotalShares = JsonValueReader.Decimal(row, "TOTAL_A_SHARES"),
                ChangeReason = JsonValueReader.Text(row, "CHANGE_REASON"),
                ReportName = JsonValueReader.Text(row, "REPORT"),
                NoticeDate = JsonValueReader.Date(row, "HOLD_NOTICE_DATE"),
                UpdatedAt = SaTime.Now
            };
        }
    }

    /// <summary>解析股权质押。</summary>
    internal static IEnumerable<PledgeStat> ParsePledge(string code, IEnumerable<JsonElement> rows)
    {
        foreach (var row in rows)
        {
            if (JsonValueReader.Date(row, "TRADE_DATE") is not { } tradeDate)
            {
                continue;
            }

            yield return new PledgeStat
            {
                Code = code,
                TradeDate = tradeDate,
                PledgeRatio = JsonValueReader.Decimal(row, "PLEDGE_RATIO"),
                PledgeSharesWan = JsonValueReader.Decimal(row, "REPURCHASE_BALANCE"),
                PledgeDealNum = JsonValueReader.Int(row, "PLEDGE_DEAL_NUM"),
                PledgeMarketCapWan = JsonValueReader.Decimal(row, "PLEDGE_MARKET_CAP"),
                Industry = JsonValueReader.Text(row, "INDUSTRY"),
                Year1ChangePercent = JsonValueReader.Decimal(row, "Y1_CLOSE_ADJCHRATE"),
                UpdatedAt = SaTime.Now
            };
        }
    }

    /// <summary>取数据行；无数据时返回空序列（新股暂无股东数据是合法状态）。</summary>
    private async Task<IReadOnlyList<JsonElement>> QueryAsync(
        string url,
        string code,
        CancellationToken cancellationToken)
    {
        using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!JsonValueReader.TryGet(document.RootElement, "result", out var result)
            || result.ValueKind != JsonValueKind.Object
            || !JsonValueReader.TryGet(result, "data", out var rows)
            || rows.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return rows.EnumerateArray().Select(row => row.Clone()).ToList();
    }

    /// <summary>拼东财 filter 片段。</summary>
    private static string Filter(string field, string value) => $"({field}%3D%22{Uri.EscapeDataString(value)}%22)";
}
