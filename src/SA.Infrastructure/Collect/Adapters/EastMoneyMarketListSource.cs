using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 东方财富全市场证券列表（<c>push2.eastmoney.com/api/qt/clist/get</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 这是股票池与全市场快照的<b>唯一来源</b>：一次响应同时给出基础信息（代码 / 名称 / 市场 /
/// 东财行业 <c>f100</c>）与最新行情（价 / 涨跌 / 成交额 / 市值 / 估值）。
/// </para>
/// <para>
/// 三个实测事实决定了实现方式（实施计划 §3.1，2026-09-20 复核）：
/// </para>
/// <list type="number">
/// <item>单页上限 100 行（<c>pz=6000</c> 仍只返回 100 行），全市场约 5,900 只 ⇒ 约 60 次请求。</item>
/// <item>排序必须用 <c>fid=f12&amp;po=0</c>（按代码升序）：按涨跌幅排序时，翻页期间行情变化会让
/// 行在页间漂移，导致漏采与重复；按代码排序是稳定的。</item>
/// <item>退市 / 长期停牌标的（如 <c>000003</c>）仍在列表内，但数值字段返回字符串 <c>"-"</c>，
/// 因此解析必须容忍非数值（见 <see cref="JsonValueReader"/>）。</item>
/// </list>
/// </remarks>
public sealed class EastMoneyMarketListSource(
    CollectHttpClient http,
    CollectOptions options,
    ILogger<EastMoneyMarketListSource> logger)
    : IMarketListSource
{
    private const int PageSize = 100;

    /// <summary>安全上限：防止上游 total 异常导致无限翻页。</summary>
    private const int MaxPages = 120;

    private const string FieldList =
        "f12,f13,f14,f2,f3,f4,f5,f6,f8,f9,f10,f115,f23,f20,f21,f15,f16,f17,f18,f100";

    /// <summary>沪深主板 A、创业板、科创板、北交所。</summary>
    private const string UniverseFilter = "m:0+t:6,m:0+t:80,m:1+t:2,m:1+t:23,m:0+t:81+s:2048";

    /// <summary>行情主机基地址（可配置，见 <see cref="CollectOptions.Push2BaseUrl"/>）。</summary>
    private string BaseUrl => $"{options.Push2BaseUrl.TrimEnd('/')}/api/qt/clist/get";

    /// <inheritdoc />
    public string Name => "东方财富 · 全市场列表";

    /// <inheritdoc />
    public string Domains => "行情,股票池";

    /// <inheritdoc />
    public async Task<MarketListResult> GetMarketListAsync(
        Func<IReadOnlyList<MarketListRow>, Task>? onPage = null,
        CancellationToken cancellationToken = default)
    {
        var rows = 0;
        var total = 0;
        var asOf = SaTime.Today;

        for (var page = 1; page <= MaxPages; page++)
        {
            var url = $"{BaseUrl}?pn={page}&pz={PageSize}&po=0&np=1&fltt=2&invt=2&fid=f12&fs={UniverseFilter}&fields={FieldList}";
            using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!JsonValueReader.TryGet(document.RootElement, "data", out var data)
                || data.ValueKind != JsonValueKind.Object
                || !JsonValueReader.TryGet(data, "diff", out var diff)
                || diff.ValueKind != JsonValueKind.Array)
            {
                // 上游在限频或参数异常时返回 data:null，此时不是「没有股票」而是「这页没拿到」
                throw new CollectHttpException($"{Name} 第 {page} 页缺少 data.diff", null, null);
            }

            total = JsonValueReader.Int(data, "total") ?? total;
            var pageRows = new List<MarketListRow>(diff.GetArrayLength());
            foreach (var element in diff.EnumerateArray())
            {
                var row = Parse(element);
                if (row is not null)
                {
                    pageRows.Add(row.Value);
                }
            }

            rows += pageRows.Count;
            if (pageRows.Count > 0 && onPage is not null)
            {
                await onPage(pageRows).ConfigureAwait(false);
            }

            // 空页或已取满即结束；以实际行数为准而不是 total，避免上游 total 抖动导致死循环
            if (diff.GetArrayLength() < PageSize || rows >= total)
            {
                logger.LogInformation("{Source} 扫描完成：{Rows} 行 / 上游声明 {Total} 行，共 {Pages} 页", Name, rows, total, page);
                break;
            }
        }

        return new MarketListResult(rows, total, asOf);
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var url = $"{BaseUrl}?pn=1&pz=5&po=0&np=1&fltt=2&invt=2&fid=f12&fs={UniverseFilter}&fields={FieldList}";
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

    /// <summary>
    /// 解析一行。数值字段缺失（退市 / 停牌为 <c>"-"</c>）时按 0 处理，
    /// 名称统一去掉全角空格等异常空白（如「万  科Ａ」）。
    /// </summary>
    internal static MarketListRow? Parse(JsonElement element)
    {
        var code = JsonValueReader.Text(element, "f12");
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var name = JsonValueReader.TextOrEmpty(element, "f14").Replace('\u3000', ' ').Trim();
        name = string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return new MarketListRow(
            Code: code,
            Name: name,
            Market: JsonValueReader.Int(element, "f13") ?? MarketCodes.MarketOf(code),
            Industry: JsonValueReader.Text(element, "f100"),
            Price: JsonValueReader.DecimalOrZero(element, "f2"),
            Change: JsonValueReader.DecimalOrZero(element, "f4"),
            Pct: JsonValueReader.DecimalOrZero(element, "f3"),
            Volume: JsonValueReader.DecimalOrZero(element, "f5"),
            Amount: JsonValueReader.DecimalOrZero(element, "f6"),
            Turnover: JsonValueReader.DecimalOrZero(element, "f8"),
            VolRatio: JsonValueReader.DecimalOrZero(element, "f10"),
            Pe: JsonValueReader.DecimalOrZero(element, "f9"),
            PeTtm: JsonValueReader.DecimalOrZero(element, "f115"),
            Pb: JsonValueReader.DecimalOrZero(element, "f23"),
            MarketCap: JsonValueReader.DecimalOrZero(element, "f20"),
            FloatCap: JsonValueReader.DecimalOrZero(element, "f21"),
            Open: JsonValueReader.DecimalOrZero(element, "f17"),
            High: JsonValueReader.DecimalOrZero(element, "f15"),
            Low: JsonValueReader.DecimalOrZero(element, "f16"),
            PrevClose: JsonValueReader.DecimalOrZero(element, "f18"));
    }
}
