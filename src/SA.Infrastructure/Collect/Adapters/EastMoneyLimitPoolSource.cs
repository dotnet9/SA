using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using SA.Application.Abstractions;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 东方财富涨跌停池（<c>push2ex.eastmoney.com/getTopicZTPool</c> / <c>getTopicDTPool</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 涨跌停家数必须取专用口径，不能按「涨跌幅 ≥ 9.8%」反推：主板 10%、创业板与科创板 20%、
/// 北交所 30%、ST 5%，阈值反推在这几类上都算错。
/// </para>
/// <para>
/// <b>实测坑</b>：<c>date</c> 参数在早于最新交易日时会被上游忽略，响应固定返回最新交易日的
/// <c>qdate</c>（2026-09-20 请求 20260917 仍返回 <c>qdate=20260918</c>）。
/// 因此以响应里的 <c>qdate</c> 作为实际口径日期，避免把最新数据标成历史日期。
/// </para>
/// <para><c>ut</c> 为公开固定值，若上游更换需从行情页脚本重新提取。</para>
/// </remarks>
public sealed class EastMoneyLimitPoolSource(CollectHttpClient http) : ILimitPoolSource
{
    private const string Ut = "7eea3edcaed734bea9cbfc24409ed989";

    private const string LimitUpUrl =
        "https://push2ex.eastmoney.com/getTopicZTPool?ut=" + Ut + "&dpt=wz.ztzt&Pageindex=0&pagesize=1&sort=fbt%3Aasc&date=";

    private const string LimitDownUrl =
        "https://push2ex.eastmoney.com/getTopicDTPool?ut=" + Ut + "&dpt=wz.ztzt&Pageindex=0&pagesize=1&sort=fund%3Aasc&date=";

    /// <inheritdoc />
    public string Name => "东方财富 · 涨跌停池";

    /// <inheritdoc />
    public string Domains => "行情,市场情绪";

    /// <inheritdoc />
    public async Task<LimitPoolResult> GetLimitPoolsAsync(
        DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        var day = date ?? SA.Domain.Common.SaTime.Today;
        var query = day.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var limitUp = await GetCountAsync(LimitUpUrl + query, cancellationToken).ConfigureAwait(false);
        var limitDown = await GetCountAsync(LimitDownUrl + query, cancellationToken).ConfigureAwait(false);

        // 两个端点都返回 qdate；以涨停池为准（涨跌停池的 qdate 不会不一致）
        return new LimitPoolResult(limitUp.Date, limitUp.Count, limitDown.Count);
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await GetLimitPoolsAsync(null, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            // 涨跌停同时为 0 是可能的（极端平静的交易日），因此不以数值大小判成败，只要求拿到了日期
            return result.Date != default
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, result.LimitUp + result.LimitDown)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "未返回 qdate");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 读取一个池的家数与口径日期。
    /// </summary>
    private async Task<(DateOnly Date, int Count)> GetCountAsync(string url, CancellationToken cancellationToken)
    {
        using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!JsonValueReader.TryGet(document.RootElement, "data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            throw new CollectHttpException($"{Name} 缺少 data 对象", null, null);
        }

        return ParsePool(data);
    }

    /// <summary>
    /// 解析一个涨跌停池。<c>qdate</c> 在上游是<b>数字</b>（<c>20260918</c>），
    /// 必须按紧凑日期解析；按字符串读会把日期当成缺失，进而把家数静默丢成 0。
    /// </summary>
    internal static (DateOnly Date, int Count) ParsePool(JsonElement data) =>
        (JsonValueReader.CompactDate(data, "qdate") ?? default, JsonValueReader.IntOrZero(data, "tc"));
}
