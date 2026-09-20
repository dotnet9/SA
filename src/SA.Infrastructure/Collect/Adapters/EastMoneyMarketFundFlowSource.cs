using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using SA.Application.Abstractions;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 东方财富大盘资金流（<c>push2.eastmoney.com/api/qt/stock/fflow/kline/get</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 个股资金流端点不适用于全市场，这里用指数口径：沪市 <c>1.000001</c> + 深市 <c>0.399001</c>
/// 分别取流后按层相加，得到「两市合计」。
/// </para>
/// <para>
/// <b>字段序已用算术自洽验证</b>（2026-09-20 实测上证 2026-09-18 一行）：
/// <c>[0]</c> 日期、<c>[1]</c> 主力净额、<c>[2]</c> 小单、<c>[3]</c> 中单、<c>[4]</c> 大单、<c>[5]</c> 超大单；
/// 校验：大单 + 超大单 = 主力净额（780779520 + 13733306368 = 14514085888 ✓），
/// 小单 + 中单 = −主力净额（-2433945600 + -12080132096 = -14514077696 ≈ −14514085888 ✓，
/// 差额来自上游自身的取整）。
/// </para>
/// <para>
/// 注意指数口径的该端点只返回 6 个字段（个股端点返回 13 个），因此不能照抄个股的字段映射。
/// </para>
/// </remarks>
public sealed class EastMoneyMarketFundFlowSource(CollectHttpClient http) : IMarketFundFlowSource
{
    /// <summary>沪市与深市的指数 secid。</summary>
    private static readonly string[] Markets = ["1.000001", "0.399001"];

    private const string BaseUrl =
        "https://push2.eastmoney.com/api/qt/stock/fflow/kline/get?lmt=1&klt=101&fields1=f1,f2,f3,f7&fields2=f51,f52,f53,f54,f55,f56";

    /// <inheritdoc />
    public string Name => "东方财富 · 大盘资金流";

    /// <inheritdoc />
    public string Domains => "资金";

    /// <inheritdoc />
    public async Task<MarketFundFlowResult?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        DateOnly? date = null;
        decimal main = 0, small = 0, medium = 0, large = 0, superLarge = 0;

        foreach (var secid in Markets)
        {
            var url = $"{BaseUrl}&secid={secid}";
            using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!JsonValueReader.TryGet(document.RootElement, "data", out var data)
                || data.ValueKind != JsonValueKind.Object
                || !JsonValueReader.TryGet(data, "klines", out var klines)
                || klines.ValueKind != JsonValueKind.Array
                || klines.GetArrayLength() == 0)
            {
                throw new CollectHttpException($"{Name} 缺少 data.klines（secid={secid}）", null, null);
            }

            // 取最后一行：最新交易日
            var line = klines[klines.GetArrayLength() - 1].GetString();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parsed = ParseKline(line);
            date ??= parsed.Date;
            main += parsed.MainNet;
            small += parsed.Small;
            medium += parsed.Medium;
            large += parsed.Large;
            superLarge += parsed.SuperLarge;
        }

        return date is null
            ? null
            : new MarketFundFlowResult(date.Value, main, superLarge, large, medium, small);
    }

    /// <summary>
    /// 解析一行大盘资金流。字段序（实测自洽验证）：
    /// <c>[0]</c> 日期、<c>[1]</c> 主力净额、<c>[2]</c> 小单、<c>[3]</c> 中单、<c>[4]</c> 大单、<c>[5]</c> 超大单。
    /// </summary>
    internal static (DateOnly Date, decimal MainNet, decimal Small, decimal Medium, decimal Large, decimal SuperLarge) ParseKline(string line)
    {
        var parts = line.Split(',');
        if (parts.Length < 6)
        {
            throw new CollectHttpException($"大盘资金流字段数不足（实际 {parts.Length}，期望 ≥ 6）", null, null);
        }

        return (
            ParseDate(parts[0]) ?? default,
            Number(parts[1]),
            Number(parts[2]),
            Number(parts[3]),
            Number(parts[4]),
            Number(parts[5]));
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await GetLatestAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return result is not null
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, 2)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "未返回资金流数据");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }

    private static DateOnly? ParseDate(string value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;

    private static decimal Number(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
}
