using System.Diagnostics;
using System.Text.Json;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Research;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 东方财富 F10 经营分析（<c>PC_HSF10/BusinessAnalysis/PageAjax</c>）：业务范围、主营构成、经营评述。
/// </summary>
/// <remarks>
/// <para>
/// 顶层三个键（实测 2026-09-21）：<c>zyfw</c> 业务范围、<c>zygcfx</c> 主营构成、<c>jyps</c> 经营评述。
/// </para>
/// <para>
/// <b>主营构成的四个坑（全部实测复现，实施计划 §2.4.1）</b>：
/// </para>
/// <list type="number">
/// <item><c>MAINOP_TYPE</c> <b>1=按行业/大类、2=按产品、3=按地区是三套并列口径，不能相加</b>：
/// 实测东芯股份 2025 年报三套各自合计 100%，混算会得到翻倍的收入。</item>
/// <item><c>ITEM_NAME</c> 中形如 <c>其中:SDRAM</c> 的是<b>子项</b>（实测 2021 中报有 4 个），
/// 会计入父项（DRAM），不能当作同级产品并列展示。这里标记 <c>IsSubItem</c>，由展示层过滤。</item>
/// <item>早期报告期是招股书口径：实测 <c>2021-09-30</c> 的 <c>ITEM_NAME</c> 为「客户合同产生的收入」，
/// <c>MAIN_BUSINESS_COST</c> 与 <c>GROSS_RPOFIT_RATIO</c> 均为 null（共 43 行）→ 只展示收入。</item>
/// <item><b>比率字段是小数不是百分数</b>：<c>MBI_RATIO</c> 0.652002 = 65.20%、
/// <c>GROSS_RPOFIT_RATIO</c> 0.245146 = 24.51%。本适配器<b>原样存小数</b>。</item>
/// </list>
/// <para>
/// <b>经营评述只做原文展示</b>（实测东芯 2026 中报约 4,500 字）：不做任何解析（实施计划 §1.3）。
/// </para>
/// </remarks>
public sealed class EastMoneyBusinessSource(CollectHttpClient http) : IBusinessCompositionSource
{
    private const string BaseUrl = "https://emweb.securities.eastmoney.com/PC_HSF10/BusinessAnalysis/PageAjax";

    /// <summary><c>其中:</c> 子项前缀（用码点拼出，避免源码里的中文常量被误改）。</summary>
    private static readonly string SubItemMarker = string.Concat((char)0x5176, (char)0x4E2D, ':');

    /// <inheritdoc />
    public string Name => "东方财富 · 主营构成";

    /// <inheritdoc />
    public string Domains => "个股研究,股权";

    /// <inheritdoc />
    public async Task<BusinessCompositionResult> GetAsync(string code, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}?code={F10Codes.Of(code)}";
        using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

        var items = ParseCompositions(code, document.RootElement).ToList();

        return new BusinessCompositionResult(
            Items: items,
            BusinessScope: ParseBusinessScope(document.RootElement),
            BusinessReview: ParseBusinessReview(document.RootElement));
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await GetAsync("688110", cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return result.Items.Count > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, result.Items.Count)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "zygcfx 为空");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 解析主营构成（<c>zygcfx</c>）。
    /// </summary>
    /// <param name="code">证券代码。</param>
    /// <param name="root">响应根节点。</param>
    internal static IEnumerable<BusinessComposition> ParseCompositions(string code, JsonElement root)
    {
        if (!JsonValueReader.TryGet(root, "zygcfx", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var row in rows.EnumerateArray())
        {
            var itemName = JsonValueReader.Text(row, "ITEM_NAME");
            if (string.IsNullOrWhiteSpace(itemName))
            {
                continue;
            }

            if (JsonValueReader.Date(row, "REPORT_DATE") is not { } reportDate)
            {
                continue;
            }

            yield return new BusinessComposition
            {
                Code = code,
                ReportDate = reportDate,
                MainOpType = JsonValueReader.Int(row, "MAINOP_TYPE") ?? 0,
                ItemName = itemName.Trim(),
                Rank = JsonValueReader.Int(row, "RANK") ?? 0,

                Income = JsonValueReader.Decimal(row, "MAIN_BUSINESS_INCOME"),
                // 原样存小数：上游 0.652002 表示 65.20%
                IncomeRatio = JsonValueReader.Decimal(row, "MBI_RATIO"),
                Cost = JsonValueReader.Decimal(row, "MAIN_BUSINESS_COST"),
                CostRatio = JsonValueReader.Decimal(row, "MBC_RATIO"),
                // 上游把 PROFIT 拼成了 RPOFIT，照它读
                Profit = JsonValueReader.Decimal(row, "MAIN_BUSINESS_RPOFIT"),
                ProfitRatio = JsonValueReader.Decimal(row, "MBR_RATIO"),
                GrossProfitRatio = JsonValueReader.Decimal(row, "GROSS_RPOFIT_RATIO"),

                IsSubItem = itemName.Trim().StartsWith(SubItemMarker, StringComparison.Ordinal),
                UpdatedAt = SaTime.Now
            };
        }
    }

    /// <summary>解析业务范围（<c>zyfw[0].BUSINESS_SCOPE</c>）。</summary>
    internal static string? ParseBusinessScope(JsonElement root)
    {
        if (!JsonValueReader.TryGet(root, "zyfw", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var row in rows.EnumerateArray())
        {
            if (JsonValueReader.Text(row, "BUSINESS_SCOPE") is { } scope)
            {
                return scope;
            }
        }

        return null;
    }

    /// <summary>
    /// 解析经营评述原文（<c>jyps</c> 里第一条非空的 <c>BUSINESS_REVIEW</c>）。
    /// </summary>
    /// <remarks>
    /// 只取原文，不解析（实施计划 §1.3 明确不做正文解析）。
    /// 多条时取第一条：实测同一标的只有一条，多条的形态未验证，取首条是保守做法。
    /// </remarks>
    internal static string? ParseBusinessReview(JsonElement root)
    {
        if (!JsonValueReader.TryGet(root, "jyps", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var row in rows.EnumerateArray())
        {
            if (JsonValueReader.Text(row, "BUSINESS_REVIEW") is { } review)
            {
                return review;
            }
        }

        return null;
    }
}
