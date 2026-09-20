using System.Diagnostics;
using System.Text.Json;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Rating;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 东方财富机构评级与预测（数据中心 <c>RPT_WEB_RESPREDICT</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 字段口径由 2026-09-20 的真实响应核对（300750）：
/// <c>RATING_ORG_NUM</c> 评级机构数、<c>RATING_BUY_NUM</c>/<c>RATING_ADD_NUM</c>/<c>RATING_NEUTRAL_NUM</c>/
/// <c>RATING_REDUCE_NUM</c>/<c>RATING_SALE_NUM</c> 各档家数、
/// <c>DEC_AIMPRICEMIN</c>/<c>DEC_AIMPRICEMAX</c> 目标价区间、
/// <c>YEAR1..YEAR4</c> + <c>YEAR_MARK1..4</c> + <c>EPS1..EPS4</c> 未来年度预测。
/// </para>
/// <para>
/// <c>YEAR_MARK</c> 的 <c>A</c> 表示实际值、<c>E</c> 表示预测值：展示时必须区分，
/// 否则会把已实现的 EPS 当成预测值（300750 的 2025 年就是 <c>A</c>）。
/// </para>
/// </remarks>
public sealed class EastMoneyRatingSource(CollectHttpClient http) : IRatingSource
{
    private const string BaseUrl = "https://datacenter-web.eastmoney.com/api/data/v1/get";

    /// <inheritdoc />
    public string Name => "东方财富 · 机构评级";

    /// <inheritdoc />
    public string Domains => "评级";

    /// <inheritdoc />
    public async Task<RatingConsensus?> GetConsensusAsync(string code, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}?reportName=RPT_WEB_RESPREDICT&columns=ALL" +
                  $"&filter=(SECURITY_CODE%3D%22{Uri.EscapeDataString(code)}%22)&pageSize=1&pageNumber=1";

        using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!JsonValueReader.TryGet(document.RootElement, "result", out var result)
            || result.ValueKind != JsonValueKind.Object
            || !JsonValueReader.TryGet(result, "data", out var rows)
            || rows.ValueKind != JsonValueKind.Array)
        {
            // 机构未覆盖的标的会返回空：这是合法状态，不是错误
            return null;
        }

        return ParseConsensus(code, rows.EnumerateArray().Select(row => row.Clone())).FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var consensus = await GetConsensusAsync("300750", cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return consensus is not null
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, 1)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "未返回评级共识");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }

    /// <summary>解析评级共识（internal 供测试用真实样本固化字段映射）。</summary>
    internal static IEnumerable<RatingConsensus> ParseConsensus(string code, IEnumerable<JsonElement> rows)
    {
        foreach (var row in rows)
        {
            yield return new RatingConsensus
            {
                Code = code,
                RatingOrgNum = JsonValueReader.IntOrZero(row, "RATING_ORG_NUM"),
                BuyNum = JsonValueReader.IntOrZero(row, "RATING_BUY_NUM"),
                AddNum = JsonValueReader.IntOrZero(row, "RATING_ADD_NUM"),
                NeutralNum = JsonValueReader.Int(row, "RATING_NEUTRAL_NUM"),
                ReduceNum = JsonValueReader.Int(row, "RATING_REDUCE_NUM"),
                SaleNum = JsonValueReader.Int(row, "RATING_SALE_NUM"),
                AimPriceMax = JsonValueReader.Decimal(row, "DEC_AIMPRICEMAX"),
                AimPriceMin = JsonValueReader.Decimal(row, "DEC_AIMPRICEMIN"),
                LongTermNum = JsonValueReader.Int(row, "RATING_LONG_NUM"),
                Year1 = JsonValueReader.Int(row, "YEAR1"),
                Eps1 = JsonValueReader.Decimal(row, "EPS1"),
                YearMark1 = JsonValueReader.Text(row, "YEAR_MARK1"),
                Year2 = JsonValueReader.Int(row, "YEAR2"),
                Eps2 = JsonValueReader.Decimal(row, "EPS2"),
                YearMark2 = JsonValueReader.Text(row, "YEAR_MARK2"),
                Year3 = JsonValueReader.Int(row, "YEAR3"),
                Eps3 = JsonValueReader.Decimal(row, "EPS3"),
                YearMark3 = JsonValueReader.Text(row, "YEAR_MARK3"),
                Year4 = JsonValueReader.Int(row, "YEAR4"),
                Eps4 = JsonValueReader.Decimal(row, "EPS4"),
                YearMark4 = JsonValueReader.Text(row, "YEAR_MARK4"),
                IndustryBoard = JsonValueReader.Text(row, "INDUSTRY_BOARD"),
                UpdatedAt = SaTime.Now
            };
        }
    }
}
