using System.Diagnostics;
using System.Text.Json;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Research;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 东方财富 F10 股本结构（<c>PC_HSF10/CapitalStockStructure/PageAjax</c>）：股本变动历史与限售解禁。
/// </summary>
/// <remarks>
/// <para>
/// 顶层四个键（实测 2026-09-21）：<c>xsjj</c> 限售解禁、<c>gbjg</c> 股本结构最新、
/// <c>lngbbd</c> 历年股本变动、<c>gbgc</c> 股本构成序列。
/// </para>
/// <para>
/// <b><c>xsjj</c> 为空是「暂无待解禁」而不是「暂无数据」</b>（实施计划 §2.4.2）：
/// 实测东芯股份 <c>xsjj: []</c>，且 <c>TOTAL_SHARES == UNLIMITED_SHARES == LISTED_A_SHARES
/// == 442,377,391</c>、<c>LIMITED_SHARES</c> 为 <c>null</c> —— 确已全流通。
/// 这个区别对判断筹码压力很关键，因此本适配器<b>原样返回空列表</b>，
/// 由聚合层把它映射为「暂无待解禁」状态，绝不与「没查到」混同。
/// </para>
/// <para>
/// <b><c>xsjj</c> 给的是「当前剩余的待解禁」</b>：实测中芯国际 <c>688981</c> 只有 1 条
/// （2027-06-23 解禁 547,182,073 股，占总股本 6.39%，类型「定向增发机构配售股份」），
/// 重庆赛力斯 <c>601127</c> 1 条。因此存储侧用整体替换而不是 upsert，
/// 否则已解禁完的历史条目会一直留着被当成未来待解禁。
/// </para>
/// <para>
/// <b><c>LIMITED_SHARES</c> 的 null 与 0 都表示「没有限售股」</b>：
/// 东芯为 <c>null</c>、贵州茅台为 <c>0</c>，两者含义相同。
/// </para>
/// </remarks>
public sealed class EastMoneyCapitalStructureSource(CollectHttpClient http) : ICapitalStructureSource
{
    private const string BaseUrl = "https://emweb.securities.eastmoney.com/PC_HSF10/CapitalStockStructure/PageAjax";

    /// <inheritdoc />
    public string Name => "东方财富 · 股本结构";

    /// <inheritdoc />
    public string Domains => "个股研究,股权";

    /// <inheritdoc />
    public async Task<CapitalStructureResult> GetAsync(string code, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}?code={F10Codes.Of(code)}";
        using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

        var changes = ParseShareChanges(code, document.RootElement)
            .OrderByDescending(row => row.EndDate)
            .ToList();

        var unlocks = ParseUpcomingUnlocks(code, document.RootElement)
            .OrderBy(row => row.LiftDate)
            .ToList();

        return new CapitalStructureResult(changes, unlocks);
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // 探针用中芯国际：它有 1 条 xsjj，能同时验证两个数组都被解析到
            var result = await GetAsync("688981", cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return result.ShareChanges.Count > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, result.ShareChanges.Count)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "lngbbd 为空");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }

    /// <summary>解析股本变动历史（<c>lngbbd</c>）。</summary>
    internal static IEnumerable<ShareChange> ParseShareChanges(string code, JsonElement root)
    {
        if (!JsonValueReader.TryGet(root, "lngbbd", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var row in rows.EnumerateArray())
        {
            if (JsonValueReader.Date(row, "END_DATE") is not { } endDate)
            {
                continue;
            }

            yield return new ShareChange
            {
                Code = code,
                EndDate = endDate,
                TotalShares = JsonValueReader.Decimal(row, "TOTAL_SHARES"),
                LimitedShares = JsonValueReader.Decimal(row, "LIMITED_SHARES"),
                UnlimitedShares = JsonValueReader.Decimal(row, "UNLIMITED_SHARES"),
                ListedAShares = JsonValueReader.Decimal(row, "LISTED_A_SHARES"),
                ChangeReason = JsonValueReader.Text(row, "CHANGE_REASON"),
                UpdatedAt = SaTime.Now
            };
        }
    }

    /// <summary>解析限售解禁（<c>xsjj</c>）；空数组表示「暂无待解禁」。</summary>
    internal static IEnumerable<UpcomingUnlock> ParseUpcomingUnlocks(string code, JsonElement root)
    {
        if (!JsonValueReader.TryGet(root, "xsjj", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var row in rows.EnumerateArray())
        {
            if (JsonValueReader.Date(row, "LIFT_DATE") is not { } liftDate)
            {
                continue;
            }

            // 解禁类型是主键的一部分，缺失时用「未知」占位而不是丢弃整行
            var liftType = JsonValueReader.Text(row, "LIFT_TYPE") ?? "未知";

            yield return new UpcomingUnlock
            {
                Code = code,
                LiftDate = liftDate,
                LiftType = liftType,
                LiftShares = JsonValueReader.Decimal(row, "LIFT_NUM"),
                // 这两个上游是百分数（6.39 表示 6.39%），与本实体注释一致
                TotalSharesRatio = JsonValueReader.Decimal(row, "TOTAL_SHARES_RATIO"),
                UnlimitedASharesRatio = JsonValueReader.Decimal(row, "UNLIMITED_A_SHARES_RATIO"),
                UpdatedAt = SaTime.Now
            };
        }
    }
}
