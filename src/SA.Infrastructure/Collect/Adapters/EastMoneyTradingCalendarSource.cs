using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using SA.Application.Abstractions;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 交易日历来源：上证指数的日线日期序列（<c>push2his …/kline/get</c>，<c>klt=101</c>）。
/// </summary>
/// <remarks>
/// 指数只在交易日产生 K 线，因此其日期集合就是交易日历，无需再引入一个日历数据源。
/// 实测（实施计划 §3.1）：该端点必须同时带 <c>beg</c> 与 <c>end</c>，只给 <c>lmt</c> 会返回空 <c>data</c>。
/// </remarks>
public sealed class EastMoneyTradingCalendarSource(CollectHttpClient http) : ITradingCalendarSource
{
    private const string BaseUrl =
        "https://push2his.eastmoney.com/api/qt/stock/kline/get?secid=1.000001&fields1=f1,f2,f3,f4,f5,f6&fields2=f51&klt=101&fqt=0";

    /// <inheritdoc />
    public string Name => "东方财富 · 指数日线（交易日历）";

    /// <inheritdoc />
    public string Domains => "日历";

    /// <inheritdoc />
    public async Task<IReadOnlyList<DateOnly>> GetTradingDaysAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}&beg={from:yyyyMMdd}&end={to:yyyyMMdd}";
        using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!JsonValueReader.TryGet(document.RootElement, "data", out var data)
            || data.ValueKind != JsonValueKind.Object
            || !JsonValueReader.TryGet(data, "klines", out var klines)
            || klines.ValueKind != JsonValueKind.Array)
        {
            throw new CollectHttpException($"{Name} 缺少 data.klines", null, null);
        }

        var days = new List<DateOnly>(klines.GetArrayLength());
        foreach (var element in klines.EnumerateArray())
        {
            var line = element.GetString();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // 每行形如 "2026-09-18,..."，只取日期
            var comma = line.IndexOf(',');
            var dateText = comma < 0 ? line : line[..comma];
            if (DateOnly.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                days.Add(day);
            }
        }

        return days;
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var to = Domain.Common.SaTime.Today;
            var days = await GetTradingDaysAsync(to.AddDays(-30), to, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return days.Count > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, days.Count)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "近 30 天无交易日");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }
}
