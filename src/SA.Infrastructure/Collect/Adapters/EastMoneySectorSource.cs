using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 东方财富行业板块列表（<c>clist/get</c>，<c>fs=m:90+t:2+f:!50</c>）。
/// </summary>
/// <remarks>
/// 实测 496 个板块，单页上限同样为 100 行 ⇒ 5 次请求。字段口径：
/// <c>f12</c> 板块码（BK）、<c>f14</c> 名称、<c>f3</c> 涨跌幅、<c>f62</c> 主力净流入（元）、
/// <c>f104</c>/<c>f105</c> 涨跌家数、<c>f128</c>/<c>f140</c> 领涨股名称与代码。
/// <c>f115</c>（板块市盈率）在上游经常返回 <c>"-"</c>，此时按缺失处理而不是 0。
/// </remarks>
public sealed class EastMoneySectorSource(
    CollectHttpClient http,
    CollectOptions options,
    ILogger<EastMoneySectorSource> logger)
    : ISectorSource
{
    private const int PageSize = 100;
    private const int MaxPages = 20;
    private const string FieldList = "f12,f14,f2,f3,f62,f104,f105,f115,f128,f140";
    private const string Filter = "m:90+t:2+f:!50";

    /// <summary>行情主机基地址（可配置，见 <see cref="CollectOptions.Push2BaseUrl"/>）。</summary>
    private string BaseUrl => $"{options.Push2BaseUrl.TrimEnd('/')}/api/qt/clist/get";

    /// <inheritdoc />
    public string Name => "东方财富 · 行业板块";

    /// <inheritdoc />
    public string Domains => "行业,行情";

    /// <inheritdoc />
    public async Task<IReadOnlyList<SectorRow>> GetSectorsAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<SectorRow>();
        var total = 0;

        for (var page = 1; page <= MaxPages; page++)
        {
            var url = $"{BaseUrl}?pn={page}&pz={PageSize}&po=0&np=1&fltt=2&invt=2&fid=f12&fs={Filter}&fields={FieldList}";
            using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!JsonValueReader.TryGet(document.RootElement, "data", out var data)
                || data.ValueKind != JsonValueKind.Object
                || !JsonValueReader.TryGet(data, "diff", out var diff)
                || diff.ValueKind != JsonValueKind.Array)
            {
                throw new CollectHttpException($"{Name} 第 {page} 页缺少 data.diff", null, null);
            }

            total = JsonValueReader.Int(data, "total") ?? total;
            foreach (var element in diff.EnumerateArray())
            {
                var row = Parse(element);
                if (row is not null)
                {
                    rows.Add(row.Value);
                }
            }

            if (diff.GetArrayLength() < PageSize || rows.Count >= total)
            {
                break;
            }
        }

        logger.LogInformation("{Source} 扫描完成：{Rows} 个板块 / 上游声明 {Total}", Name, rows.Count, total);
        return rows;
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var url = $"{BaseUrl}?pn=1&pz=5&po=0&np=1&fltt=2&invt=2&fid=f12&fs={Filter}&fields={FieldList}";
            using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var rows = JsonValueReader.TryGet(document.RootElement, "data", out var data)
                && JsonValueReader.TryGet(data, "diff", out var diff)
                && diff.ValueKind == JsonValueKind.Array
                    ? diff.GetArrayLength()
                    : 0;

            return rows > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, rows)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "data.diff 为空");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }

    internal static SectorRow? Parse(JsonElement element)
    {
        var code = JsonValueReader.Text(element, "f12");
        var name = JsonValueReader.Text(element, "f14");
        if (code is null || name is null)
        {
            return null;
        }

        return new SectorRow(
            Code: code,
            Name: name,
            Pct: JsonValueReader.DecimalOrZero(element, "f3"),
            MainNet: JsonValueReader.DecimalOrZero(element, "f62"),
            UpCount: JsonValueReader.IntOrZero(element, "f104"),
            DownCount: JsonValueReader.IntOrZero(element, "f105"),
            LeaderName: JsonValueReader.Text(element, "f128"),
            LeaderCode: JsonValueReader.Text(element, "f140"),
            Pe: JsonValueReader.Decimal(element, "f115"));
    }
}
