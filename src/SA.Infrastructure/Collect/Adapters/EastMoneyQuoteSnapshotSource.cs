using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 东方财富多标的实时快照（<c>push2.eastmoney.com/api/qt/ulist.np/get</c>）。
/// </summary>
/// <remarks>
/// 与全市场列表端点不同，本端点按代码列表取数，<b>请求数与标的数无关</b>，
/// 因此适合自选股这类小集合的高频刷新（3 秒推送）。代码按 100 个一组切分，
/// 避免 URL 过长被上游截断。
/// </remarks>
public sealed class EastMoneyQuoteSnapshotSource(
    CollectHttpClient http,
    CollectOptions options,
    ILogger<EastMoneyQuoteSnapshotSource> logger)
    : IQuoteSnapshotSource
{
    /// <summary>单次请求的代码数上限（实测 100 个 secid 的 URL 约 900 字符，安全）。</summary>
    private const int BatchSize = 100;

    private const string FieldList =
        "f12,f13,f14,f2,f3,f4,f5,f6,f8,f9,f10,f115,f23,f20,f21,f15,f16,f17,f18,f124";

    /// <summary>行情主机基地址（可配置，见 <see cref="CollectOptions.Push2BaseUrl"/>）。</summary>
    private string BaseUrl => $"{options.Push2BaseUrl.TrimEnd('/')}/api/qt/ulist.np/get";

    /// <inheritdoc />
    public string Name => "东方财富 · 行情快照";

    /// <inheritdoc />
    public string Domains => "行情,自选";

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuoteRow>> GetQuotesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken = default)
    {
        var wanted = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (wanted.Count == 0)
        {
            return [];
        }

        var rows = new List<QuoteRow>(wanted.Count);
        foreach (var batch in wanted.Chunk(BatchSize))
        {
            var secIds = string.Join(',', batch.Select(MarketCodes.SecId));
            var url = $"{BaseUrl}?fltt=2&secids={secIds}&fields={FieldList}";
            using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!JsonValueReader.TryGet(document.RootElement, "data", out var data)
                || data.ValueKind != JsonValueKind.Object
                || !JsonValueReader.TryGet(data, "diff", out var diff)
                || diff.ValueKind != JsonValueKind.Array)
            {
                throw new CollectHttpException($"{Name} 缺少 data.diff", null, null);
            }

            foreach (var element in diff.EnumerateArray())
            {
                var row = Parse(element);
                if (row is not null)
                {
                    rows.Add(row.Value);
                }
            }
        }

        logger.LogDebug("{Source} 取回 {Rows}/{Wanted} 条快照", Name, rows.Count, wanted.Count);
        return rows;
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var rows = await GetQuotesAsync(["300750", "600519"], cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return rows.Count > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, rows.Count)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "未返回任何快照");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 解析一行快照。价格为 <c>"-"</c>（停牌 / 退市）的行直接丢弃：
    /// 界面需要的是「有行情的标的」，无行情的由基础信息表承担。
    /// </summary>
    private static QuoteRow? Parse(JsonElement element)
    {
        var code = JsonValueReader.Text(element, "f12");
        var price = JsonValueReader.Decimal(element, "f2");
        if (code is null || price is null || price <= 0)
        {
            return null;
        }

        return new QuoteRow(
            Code: code,
            Name: JsonValueReader.TextOrEmpty(element, "f14").Trim(),
            Market: JsonValueReader.Int(element, "f13") ?? MarketCodes.MarketOf(code),
            Price: price.Value,
            Change: JsonValueReader.DecimalOrZero(element, "f4"),
            Pct: JsonValueReader.DecimalOrZero(element, "f3"),
            Volume: JsonValueReader.DecimalOrZero(element, "f5"),
            Amount: JsonValueReader.DecimalOrZero(element, "f6"),
            Turnover: JsonValueReader.DecimalOrZero(element, "f8"),
            VolRatio: JsonValueReader.DecimalOrZero(element, "f10"),
            Open: JsonValueReader.DecimalOrZero(element, "f17"),
            High: JsonValueReader.DecimalOrZero(element, "f15"),
            Low: JsonValueReader.DecimalOrZero(element, "f16"),
            PrevClose: JsonValueReader.DecimalOrZero(element, "f18"),
            MarketCap: JsonValueReader.DecimalOrZero(element, "f20"),
            FloatCap: JsonValueReader.DecimalOrZero(element, "f21"),
            Pe: JsonValueReader.DecimalOrZero(element, "f9"),
            PeTtm: JsonValueReader.DecimalOrZero(element, "f115"),
            Pb: JsonValueReader.DecimalOrZero(element, "f23"),
            AsOf: QuoteTimestamp(element));
    }

    /// <summary>解析 <c>f124</c>（Unix 秒）为业务时区时间。</summary>
    internal static DateTimeOffset? QuoteTimestamp(JsonElement element)
    {
        var seconds = JsonValueReader.Int(element, "f124");
        if (seconds is null or <= 0)
        {
            return null;
        }

        var instant = DateTimeOffset.FromUnixTimeSeconds(seconds.Value);
        return TimeZoneInfo.ConvertTime(instant, SaTime.Zone);
    }
}
