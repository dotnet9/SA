using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Capital;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 东方财富个股资金流（行情侧 <c>push2his</c> 的 <c>stock/fflow/daykline</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 字段序已在实体注释里用算术自洽核对（主力 = 大单 + 超大单，五档合计为 0）。
/// </para>
/// <para>
/// 必须用 <c>push2his</c>（历史）主机：<c>push2</c> 上同名端点<b>只返回最新一天</b>
/// （实测 <c>lmt=60</c> 仍只有 1 行），那样资金流趋势图就退化成一根柱子。
/// </para>
/// </remarks>
public sealed class EastMoneyFundFlowSource(CollectHttpClient http) : IFundFlowSource
{
    /// <summary>资金流字段序（与解析逻辑一一对应）。</summary>
    private const string Fields = "fields1=f1,f2,f3,f7&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61,f62,f63";

    private const string BaseUrl = "https://push2his.eastmoney.com/api/qt/stock/fflow/daykline/get";

    /// <inheritdoc />
    public string Name => "东方财富 · 个股资金流";

    /// <inheritdoc />
    public string Domains => "资金,筹码";

    /// <inheritdoc />
    public async Task<IReadOnlyList<FundFlowDaily>> GetFundFlowAsync(
        string code,
        int days = 60,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(days, 5, 250);
        var url = $"{BaseUrl}?lmt={take}&klt=101&secid={MarketCodes.SecId(code)}&{Fields}";

        using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!JsonValueReader.TryGet(document.RootElement, "data", out var data)
            || data.ValueKind != JsonValueKind.Object
            || !JsonValueReader.TryGet(data, "klines", out var klines)
            || klines.ValueKind != JsonValueKind.Array)
        {
            // 停牌或新股可能没有资金流数据：这不是错误
            return [];
        }

        return EastMoneyCapitalSource.ParseFundFlow(
            code,
            klines.EnumerateArray()
                .Select(element => element.GetString())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line!)
                .ToList()).ToList();
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var rows = await GetFundFlowAsync("300750", 5, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return rows.Count > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, rows.Count)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "未返回资金流");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }
}

/// <summary>
/// 东方财富资金面报表（数据中心主机：龙虎榜 / 大宗交易 / 两融明细 / 陆股通持股）。
/// </summary>
/// <remarks>
/// 与个股资金流分成两个数据源（两台主机），因此其中一台不可用不会冷却掉另一台的数据。
/// </remarks>
public sealed class EastMoneyCapitalSource(CollectHttpClient http) : ICapitalSource
{
    private const string DataCenter = "https://datacenter-web.eastmoney.com/api/data/v1/get";

    /// <inheritdoc />
    public string Name => "东方财富 · 资金面报表";

    /// <inheritdoc />
    public string Domains => "资金,筹码";

    /// <inheritdoc />
    public async Task<IReadOnlyList<BillboardRecord>> GetBillboardsAsync(
        string code,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync(
            $"{DataCenter}?reportName=RPT_DAILYBILLBOARD_DETAILSNEW&columns=ALL" +
            $"&filter={Filter("SECURITY_CODE", code)}&pageSize={Math.Clamp(limit, 1, 50)}&pageNumber=1" +
            "&sortColumns=TRADE_DATE&sortTypes=-1",
            cancellationToken).ConfigureAwait(false);

        return ParseBillboards(code, rows).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BlockTrade>> GetBlockTradesAsync(
        string code,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync(
            $"{DataCenter}?reportName=RPT_DATA_BLOCKTRADE&columns=ALL" +
            $"&filter={Filter("SECURITY_CODE", code)}&pageSize={Math.Clamp(limit, 1, 50)}&pageNumber=1" +
            "&sortColumns=TRADE_DATE&sortTypes=-1",
            cancellationToken).ConfigureAwait(false);

        return ParseBlockTrades(code, rows).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MarginDetail>> GetMarginDetailsAsync(
        string code,
        int limit = 30,
        CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync(
            $"{DataCenter}?reportName=RPTA_WEB_RZRQ_GGMX&columns=ALL" +
            $"&filter={Filter("SCODE", code)}&pageSize={Math.Clamp(limit, 1, 60)}&pageNumber=1" +
            "&sortColumns=DATE&sortTypes=-1",
            cancellationToken).ConfigureAwait(false);

        return ParseMarginDetails(code, rows).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NorthboundHolding>> GetNorthboundAsync(
        string code,
        int limit = 8,
        CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync(
            $"{DataCenter}?reportName=RPT_MUTUAL_HOLDRANK_NEW&columns=ALL" +
            $"&filter={Filter("SECURITY_CODE", code)}&pageSize={Math.Clamp(limit, 1, 20)}&pageNumber=1" +
            "&sortColumns=HOLD_DATE&sortTypes=-1",
            cancellationToken).ConfigureAwait(false);

        return ParseNorthbound(code, rows).ToList();
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var rows = await GetMarginDetailsAsync("300750", 5, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return rows.Count > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, rows.Count)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "未返回两融明细");
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

    /// <summary>
    /// 解析个股资金流的 <c>klines</c> 行。
    /// </summary>
    /// <remarks>
    /// 字段序：<c>[0]</c> 日期、<c>[1]</c> 主力、<c>[2]</c> 小单、<c>[3]</c> 中单、<c>[4]</c> 大单、<c>[5]</c> 超大单、
    /// <c>[6]</c> 主力占比、<c>[7..10]</c> 各档占比、<c>[11]</c> 收盘、<c>[12]</c> 涨跌幅。
    /// </remarks>
    internal static IEnumerable<FundFlowDaily> ParseFundFlow(string code, IEnumerable<string> klines)
    {
        foreach (var line in klines)
        {
            var parts = line.Split(',');
            if (parts.Length < 13)
            {
                continue;
            }

            if (!DateOnly.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            yield return new FundFlowDaily
            {
                Code = code,
                Date = date,
                MainNet = Number(parts[1]),
                SmallNet = Number(parts[2]),
                MediumNet = Number(parts[3]),
                LargeNet = Number(parts[4]),
                SuperLargeNet = Number(parts[5]),
                MainRatio = ParseNumber(parts[6]),
                Close = ParseNumber(parts[11]),
                ChangePercent = ParseNumber(parts[12]),
                UpdatedAt = SaTime.Now
            };
        }
    }

    /// <summary>解析龙虎榜。</summary>
    internal static IEnumerable<BillboardRecord> ParseBillboards(string code, IEnumerable<JsonElement> rows)
    {
        foreach (var row in rows)
        {
            if (JsonValueReader.Date(row, "TRADE_DATE") is not { } tradeDate)
            {
                continue;
            }

            yield return new BillboardRecord
            {
                Code = code,
                TradeDate = tradeDate,
                Reason = JsonValueReader.Text(row, "EXPLANATION"),
                Explain = JsonValueReader.Text(row, "EXPLAIN"),
                Close = JsonValueReader.Decimal(row, "CLOSE_PRICE"),
                ChangePercent = JsonValueReader.Decimal(row, "CHANGE_RATE"),
                TurnoverRate = JsonValueReader.Decimal(row, "TURNOVERRATE"),
                NetAmount = JsonValueReader.Decimal(row, "BILLBOARD_NET_AMT"),
                BuyAmount = JsonValueReader.Decimal(row, "BILLBOARD_BUY_AMT"),
                SellAmount = JsonValueReader.Decimal(row, "BILLBOARD_SELL_AMT"),
                DealAmount = JsonValueReader.Decimal(row, "BILLBOARD_DEAL_AMT"),
                Next1Change = JsonValueReader.Decimal(row, "D1_CLOSE_ADJCHRATE"),
                Next5Change = JsonValueReader.Decimal(row, "D5_CLOSE_ADJCHRATE"),
                Next10Change = JsonValueReader.Decimal(row, "D10_CLOSE_ADJCHRATE"),
                UpdatedAt = SaTime.Now
            };
        }
    }

    /// <summary>解析大宗交易。</summary>
    internal static IEnumerable<BlockTrade> ParseBlockTrades(string code, IEnumerable<JsonElement> rows)
    {
        foreach (var row in rows)
        {
            if (JsonValueReader.Date(row, "TRADE_DATE") is not { } tradeDate)
            {
                continue;
            }

            yield return new BlockTrade
            {
                Code = code,
                TradeDate = tradeDate,
                DealPrice = JsonValueReader.Decimal(row, "DEAL_PRICE"),
                PremiumRatio = JsonValueReader.Decimal(row, "PREMIUM_RATIO"),
                DealVolume = JsonValueReader.Decimal(row, "DEAL_VOLUME"),
                DealAmount = JsonValueReader.Decimal(row, "DEAL_AMT"),
                BuyerName = JsonValueReader.Text(row, "BUYER_NAME"),
                SellerName = JsonValueReader.Text(row, "SELLER_NAME"),
                Close = JsonValueReader.Decimal(row, "CLOSE_PRICE"),
                UpdatedAt = SaTime.Now
            };
        }
    }

    /// <summary>解析个股两融明细。</summary>
    internal static IEnumerable<MarginDetail> ParseMarginDetails(string code, IEnumerable<JsonElement> rows)
    {
        foreach (var row in rows)
        {
            if (JsonValueReader.Date(row, "DATE") is not { } date)
            {
                continue;
            }

            yield return new MarginDetail
            {
                Code = code,
                Date = date,
                FinanceBalance = JsonValueReader.Decimal(row, "RZYE"),
                FinanceBuy = JsonValueReader.Decimal(row, "RZMRE"),
                FinanceNetBuy = JsonValueReader.Decimal(row, "RZJME"),
                LoanBalance = JsonValueReader.Decimal(row, "RQYE"),
                LoanVolume = JsonValueReader.Decimal(row, "RQYL"),
                TotalBalance = JsonValueReader.Decimal(row, "RZRQYE"),
                FinanceBalanceRatio = JsonValueReader.Decimal(row, "RZYEZB"),
                Close = JsonValueReader.Decimal(row, "SPJ"),
                ChangePercent = JsonValueReader.Decimal(row, "ZDF"),
                UpdatedAt = SaTime.Now
            };
        }
    }

    /// <summary>解析陆股通持股（季频）。</summary>
    internal static IEnumerable<NorthboundHolding> ParseNorthbound(string code, IEnumerable<JsonElement> rows)
    {
        foreach (var row in rows)
        {
            if (JsonValueReader.Date(row, "HOLD_DATE") is not { } holdDate)
            {
                continue;
            }

            yield return new NorthboundHolding
            {
                Code = code,
                HoldDate = holdDate,
                DateType = JsonValueReader.Text(row, "DATE_TYPE"),
                HoldShares = JsonValueReader.Decimal(row, "HOLD_SHARES"),
                PreviousHoldShares = JsonValueReader.Decimal(row, "HOLD_SHARES_LAST"),
                AddShares = JsonValueReader.Decimal(row, "ADD_SHARES_REPAIR"),
                AddSharesAmp = JsonValueReader.Decimal(row, "ADD_SHARES_AMP"),
                HoldMarketCap = JsonValueReader.Decimal(row, "HOLD_MARKET_CAP"),
                OrgQuantity = JsonValueReader.Int(row, "ORG_QUANTITY"),
                PreviousOrgQuantity = JsonValueReader.Int(row, "ORG_QUANTITY_LAST"),
                FreeSharesRatio = JsonValueReader.Decimal(row, "FREE_SHARES_RATIO"),
                TotalSharesRatio = JsonValueReader.Decimal(row, "TOTAL_SHARES_RATIO"),
                Industry = JsonValueReader.Text(row, "INDUSTRY_NAME") ?? JsonValueReader.Text(row, "BOARD_NAME"),
                UpdatedAt = SaTime.Now
            };
        }
    }

    /// <summary>取数据行；无数据返回空序列（暂无上榜记录是合法状态）。</summary>
    private async Task<IReadOnlyList<JsonElement>> QueryAsync(string url, CancellationToken cancellationToken)
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

    private static decimal Number(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;

    private static decimal? ParseNumber(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static string Filter(string field, string value) => $"({field}%3D%22{Uri.EscapeDataString(value)}%22)";
}
