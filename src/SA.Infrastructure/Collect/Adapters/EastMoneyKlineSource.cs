using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using SA.Application.Abstractions;
using SA.Domain.History;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 东方财富 K 线（<c>push2his.eastmoney.com/api/qt/stock/kline/get</c>）。
/// </summary>
/// <remarks>
/// <para>
/// <b>字段序<b>（实测，实施计划 §3.1）</b></b>：<c>klines[]</c> 每行是逗号分隔的 11 段，
/// 顺序为 <c>日期,开,收,高,低,成交量(手),成交额(元),振幅,涨跌幅,涨跌额,换手率</c>。
/// 注意<b>第 2 段是收盘、第 3 段是最高</b>（不是常见的 OHLC 顺序），照 OHLC 读会把高低价读反。
/// </para>
/// <para>
/// <b>必须同时带 <c>beg</c> 与 <c>end</c></b>：只给 <c>lmt</c> 时上游返回空 <c>data</c>（实测）。
/// 复权口径：<c>fqt=1</c> 前复权（指标与图形口径）、<c>fqt=0</c> 不复权（除权检测）、
/// <c>fqt=2</c> 后复权（长周期序列不会出现负价）。
/// </para>
/// </remarks>
public sealed class EastMoneyKlineSource(CollectHttpClient http) : IKlineSource
{
    /// <summary>前复权（默认口径）。</summary>
    public const int AdjustFront = 1;

    /// <summary>不复权（用于除权除息检测）。</summary>
    public const int AdjustNone = 0;

    /// <summary>后复权。</summary>
    public const int AdjustBack = 2;

    /// <summary>日线。</summary>
    public const int PeriodDaily = 101;

    /// <summary>周线。</summary>
    public const int PeriodWeekly = 102;

    /// <summary>月线。</summary>
    public const int PeriodMonthly = 103;

    private const string BaseUrl = "https://push2his.eastmoney.com/api/qt/stock/kline/get";

    private const string Fields = "fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61";

    /// <inheritdoc />
    public string Name => "东方财富 · K 线";

    /// <inheritdoc />
    public string Domains => "行情,历史";

    /// <inheritdoc />
    public async Task<IReadOnlyList<DailyBar>> GetDailyAsync(
        string code,
        DateOnly from,
        DateOnly to,
        int adjust = AdjustFront,
        int period = PeriodDaily,
        CancellationToken cancellationToken = default)
    {
        // 用 ResolveSecId 而不是 SecId：指数（沪深300 = 1.000300）与板块（90.BKxxxx）的前缀
        // 都不能按代码首位推断，拼错会静默返回空 data
        var url = $"{BaseUrl}?secid={Domain.Common.MarketCodes.ResolveSecId(code)}&{Fields}" +
                  $"&klt={period}&fqt={adjust}&beg={from:yyyyMMdd}&end={to:yyyyMMdd}";

        using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!JsonValueReader.TryGet(document.RootElement, "data", out var data)
            || data.ValueKind != JsonValueKind.Object
            || !JsonValueReader.TryGet(data, "klines", out var klines)
            || klines.ValueKind != JsonValueKind.Array)
        {
            throw new CollectHttpException($"{Name} 缺少 data.klines（code={code}）", null, null);
        }

        var bars = new List<DailyBar>(klines.GetArrayLength());
        foreach (var element in klines.EnumerateArray())
        {
            var line = element.GetString();
            if (!string.IsNullOrWhiteSpace(line))
            {
                var bar = ParseKline(line);
                if (bar is not null)
                {
                    bars.Add(bar.Value);
                }
            }
        }

        return bars;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DailyBar>> GetSectorDailyAsync(
        string sectorCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        // 板块指数没有复权概念，fqt 固定 0；secid 必须带 90. 前缀
        var url = $"{BaseUrl}?secid={Domain.Common.MarketCodes.SectorSecId(sectorCode)}&{Fields}" +
                  $"&klt={PeriodDaily}&fqt={AdjustNone}&beg={from:yyyyMMdd}&end={to:yyyyMMdd}";

        using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!JsonValueReader.TryGet(document.RootElement, "data", out var data)
            || data.ValueKind != JsonValueKind.Object
            || !JsonValueReader.TryGet(data, "klines", out var klines)
            || klines.ValueKind != JsonValueKind.Array)
        {
            // 板块刚建立或上游无该板块日线时返回空：由调用方按「样本不足」处理
            return [];
        }

        var bars = new List<DailyBar>(klines.GetArrayLength());
        foreach (var element in klines.EnumerateArray())
        {
            var line = element.GetString();
            if (!string.IsNullOrWhiteSpace(line) && ParseKline(line) is { } bar)
            {
                bars.Add(bar);
            }
        }

        return bars;
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var to = Domain.Common.SaTime.Today;
            var bars = await GetDailyAsync("300750", to.AddDays(-30), to, AdjustFront, PeriodDaily, cancellationToken)
                .ConfigureAwait(false);
            stopwatch.Stop();
            return bars.Count > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, bars.Count)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "近 30 天无日线");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 解析一行 K 线。字段序见类型注释；<b>第 2 段是收盘、第 3 段是最高</b>。
    /// </summary>
    internal static DailyBar? ParseKline(string line)
    {
        var parts = line.Split(',');
        if (parts.Length < 11)
        {
            return null;
        }

        if (!DateOnly.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return null;
        }

        return new DailyBar(
            Date: date,
            Open: Number(parts[1]),
            // 实测序：1=开、2=收、3=高、4=低
            Close: Number(parts[2]),
            High: Number(parts[3]),
            Low: Number(parts[4]),
            Volume: Number(parts[5]),
            Amount: Number(parts[6]),
            Turnover: Number(parts[10]),
            VolRatio: 0m,
            AdjFactor: 1m);
    }

    private static decimal Number(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
}
