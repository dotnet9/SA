using System.Diagnostics;
using System.Text.Json;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 东方财富指数快照（<c>push2.eastmoney.com/api/qt/ulist.np/get</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 除点位与涨跌外，本适配器负责提供<b>全市场数据的业务日期</b>：列表端点不返回日期，
/// 若按本机日期落库，周末与节假日会把上一个交易日的收盘数据标成当天数据
/// （实施计划 §10「快照沿用上一收盘值并标注时间」）。
/// </para>
/// <para>
/// 依据是 <c>f124</c>（Unix 秒级时间戳，实测 2026-09-20 取到 1789719092 → 2026-09-18）。
/// </para>
/// </remarks>
public sealed class EastMoneyIndexSource(CollectHttpClient http, CollectOptions options) : IIndexSource
{
    /// <summary>指数集合：与原型 <c>market.html</c> 的 5 张卡一致，另加沪深 300（仅作基准）。</summary>
    private static readonly (string SecId, string Code, string Name, int SortOrder, bool Displayed)[] Indices =
    [
        ("1.000001", "000001", "上证指数", 1, true),
        ("0.399001", "399001", "深证成指", 2, true),
        ("0.399006", "399006", "创业板指", 3, true),
        ("1.000688", "000688", "科创50", 4, true),
        ("0.899050", "899050", "北证50", 5, true),
        ("1.000300", "000300", "沪深300", 6, false)
    ];

    private const string FieldList = "f1,f2,f3,f4,f5,f6,f12,f13,f14,f124";

    /// <summary>行情主机基地址（可配置，见 <see cref="CollectOptions.Push2BaseUrl"/>）。</summary>
    private string BaseUrl => $"{options.Push2BaseUrl.TrimEnd('/')}/api/qt/ulist.np/get";

    /// <inheritdoc />
    public string Name => "东方财富 · 指数行情";

    /// <inheritdoc />
    public string Domains => "行情,指数";

    /// <summary>
    /// 最近一次快照的行情时间戳（由上游 <c>f124</c> 给出）。
    /// </summary>
    /// <remarks>
    /// 指数与个股在同一轮行情快照里生成，因此这个时间戳可以直接作为全市场快照的业务时间。
    /// </remarks>
    public DateTimeOffset? LastQuoteTime { get; private set; }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IndexQuoteRow>> GetIndicesAsync(CancellationToken cancellationToken = default)
    {
        var secIds = string.Join(',', Indices.Select(i => i.SecId));
        var url = $"{BaseUrl}?fltt=2&secids={secIds}&fields={FieldList}";
        using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!JsonValueReader.TryGet(document.RootElement, "data", out var data)
            || data.ValueKind != JsonValueKind.Object
            || !JsonValueReader.TryGet(data, "diff", out var diff)
            || diff.ValueKind != JsonValueKind.Array)
        {
            throw new CollectHttpException($"{Name} 缺少 data.diff", null, null);
        }

        var byCode = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var element in diff.EnumerateArray())
        {
            var code = JsonValueReader.Text(element, "f12");
            if (code is not null)
            {
                byCode[code] = element.Clone();
            }
        }

        var rows = new List<IndexQuoteRow>(Indices.Length);
        foreach (var (_, code, name, _, _) in Indices)
        {
            if (!byCode.TryGetValue(code, out var element))
            {
                // 单个指数缺失不阻断其余：少一张卡好过整页失败，缺口由前端以空态呈现
                continue;
            }

            var price = JsonValueReader.DecimalOrZero(element, "f2");
            if (price <= 0)
            {
                continue;
            }

            var quoteTime = EpochSeconds(element);
            if (quoteTime is not null)
            {
                LastQuoteTime = LastQuoteTime is null || quoteTime > LastQuoteTime ? quoteTime : LastQuoteTime;
            }

            rows.Add(new IndexQuoteRow(
                Code: code,
                Name: JsonValueReader.Text(element, "f14") ?? name,
                Market: JsonValueReader.Int(element, "f13") ?? MarketCodes.MarketOf(code),
                Price: price,
                Change: JsonValueReader.DecimalOrZero(element, "f4"),
                Pct: JsonValueReader.DecimalOrZero(element, "f3"),
                Volume: JsonValueReader.DecimalOrZero(element, "f5"),
                Amount: JsonValueReader.DecimalOrZero(element, "f6"),
                AsOf: quoteTime));
        }

        return rows;
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var rows = await GetIndicesAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return rows.Count > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, rows.Count)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "指数列表为空");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 取展示顺序（供写入时使用）。
    /// </summary>
    public static int SortOrderOf(string code) =>
        Array.Find(Indices, i => i.Code == code).SortOrder;

    /// <summary>
    /// 判断是否在市场概览卡片区展示。
    /// </summary>
    public static bool IsDisplayed(string code) =>
        Array.Find(Indices, i => i.Code == code).Displayed;

    /// <summary>
    /// 解析 Unix 秒级时间戳字段 <c>f124</c>。
    /// </summary>
    private static DateTimeOffset? EpochSeconds(JsonElement element)
    {
        var seconds = JsonValueReader.Int(element, "f124");
        return seconds is null or <= 0
            ? null
            : DateTimeOffset.FromUnixTimeSeconds(seconds.Value).ToOffset(SaTime.Zone.GetUtcOffset(DateTimeOffset.UtcNow));
    }
}
