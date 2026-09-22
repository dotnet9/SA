using System.Diagnostics;
using System.Text.Json;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Research;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 东方财富研报列表（<c>reportapi.eastmoney.com/report/list</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 实测东芯股份（2025-01-01 ~ 2026-09-21）<c>hits: 5</c>。字段：<c>title</c>、<c>orgSName</c>、
/// <c>researcher</c>、<c>publishDate</c>、<c>emRatingName</c>（买入/增持）、<c>ratingChange</c>、
/// <c>predictThisYearEps</c>/<c>Pe</c>、<c>predictNextYearEps</c>/<c>Pe</c>、
/// <c>predictNextTwoYearEps</c>/<c>Pe</c>、<c>indvInduName</c>（半导体）、<c>infoCode</c>、
/// <c>encodeUrl</c>、<c>attachPages</c>。
/// </para>
/// <para>
/// <b>目标价不可用</b>：<c>indvAimPriceT</c> / <c>indvAimPriceL</c> 实测全为空字符串
/// （5 篇全部为 <c>""</c>）。按实施计划 §1.3 <b>不展示目标价、不造目标价、不拿别处的价凑</b>，
/// 因此本适配器根本不读这两个字段。
/// </para>
/// <para>
/// <b>三年 PE 预测原样展示、不做解读</b>：实测东芯股份为 127 / 114.2 / 99.8，
/// 高 PE 本身是有效信息（当期利润低、周期位置）。注意实测中有些研报的 PE 是
/// 负数或极大值（<c>-1186.76</c>、<c>16566.9</c>），那是「预测利润接近 0」的真实表达，
/// 不是数据错误，因此<b>原样落库</b>而不是过滤掉。
/// </para>
/// </remarks>
public sealed class EastMoneyResearchSource(CollectHttpClient http) : IResearchReportSource
{
    private const string BaseUrl = "https://reportapi.eastmoney.com/report/list";

    /// <summary>取多少年的研报（实测东芯近两年 5 篇，3 年足够覆盖）。</summary>
    private const int LookbackYears = 3;

    /// <inheritdoc />
    public string Name => "东方财富 · 研报列表";

    /// <inheritdoc />
    public string Domains => "个股研究,评级";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ResearchReport>> GetAsync(
        string code,
        int limit = 30,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 100);
        var today = SaTime.Today;
        var begin = today.AddYears(-LookbackYears);

        var url = $"{BaseUrl}?pageSize={take}&pageNo=1&qType=0&code={Uri.EscapeDataString(code)}" +
                  "&industryCode=*&rating=*&ratingChange=*" +
                  $"&beginTime={begin:yyyy-MM-dd}&endTime={today:yyyy-MM-dd}&fields=";

        using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

        return Parse(code, document.RootElement).ToList();
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var rows = await GetAsync("688110", 5, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return rows.Count > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, rows.Count)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "研报列表为空");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 解析研报列表（<c>data[]</c>）。
    /// </summary>
    /// <remarks>
    /// 刻意<b>不读</b> <c>indvAimPriceT</c> / <c>indvAimPriceL</c>：实测全为空字符串，
    /// 读了也只能得到 null，留在代码里会让人以为有目标价可用。
    /// </remarks>
    internal static IEnumerable<ResearchReport> Parse(string code, JsonElement root)
    {
        if (!JsonValueReader.TryGet(root, "data", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var row in rows.EnumerateArray())
        {
            var infoCode = JsonValueReader.Text(row, "infoCode");
            var title = JsonValueReader.Text(row, "title");
            if (string.IsNullOrWhiteSpace(infoCode) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            if (JsonValueReader.Date(row, "publishDate") is not { } publishDate)
            {
                continue;
            }

            var encodeUrl = JsonValueReader.Text(row, "encodeUrl");

            yield return new ResearchReport
            {
                InfoCode = infoCode.Trim(),
                Code = code,
                Title = title.Trim(),
                OrgName = JsonValueReader.Text(row, "orgName"),
                OrgShortName = JsonValueReader.Text(row, "orgSName"),
                Researcher = JsonValueReader.Text(row, "researcher"),
                PublishDate = publishDate,
                RatingName = JsonValueReader.Text(row, "emRatingName"),
                RatingChange = JsonValueReader.Int(row, "ratingChange"),
                IndustryName = JsonValueReader.Text(row, "indvInduName"),

                PredictThisYearEps = JsonValueReader.Decimal(row, "predictThisYearEps"),
                PredictThisYearPe = JsonValueReader.Decimal(row, "predictThisYearPe"),
                PredictNextYearEps = JsonValueReader.Decimal(row, "predictNextYearEps"),
                PredictNextYearPe = JsonValueReader.Decimal(row, "predictNextYearPe"),
                PredictNextTwoYearEps = JsonValueReader.Decimal(row, "predictNextTwoYearEps"),
                PredictNextTwoYearPe = JsonValueReader.Decimal(row, "predictNextTwoYearPe"),

                EncodeUrl = encodeUrl,
                Url = BuildUrl(encodeUrl),
                UpdatedAt = SaTime.Now
            };
        }
    }

    /// <summary>由 <c>encodeUrl</c> 拼原文链接（实测该地址返回 200）。</summary>
    internal static string? BuildUrl(string? encodeUrl) =>
        string.IsNullOrWhiteSpace(encodeUrl)
            ? null
            : $"https://data.eastmoney.com/report/zw_stock.jshtml?encodeUrl={Uri.EscapeDataString(encodeUrl)}";
}
