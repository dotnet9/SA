using System.Text.Json;
using SA.Contracts.Screener;
using SA.Domain.Common;

namespace SA.Application.Screener;

/// <summary>
/// 筛选条件与执行记录的互转。
/// </summary>
/// <remarks>
/// 条件以<b>请求体 JSON 原文</b>存储，回放时原样反序列化：
/// 逐字段建模会在新增筛选项时让旧记录静默丢字段，而策略回放的要求正是「完全复现当初的条件」。
/// </remarks>
public static class ScreenerRunCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>序列化条件。</summary>
    public static string Serialize(ScreenerRequest request) =>
        JsonSerializer.Serialize(request with { Page = 1 }, Options);

    /// <summary>反序列化条件；失败返回 null（坏数据不让整页崩掉）。</summary>
    public static ScreenerRequest? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ScreenerRequest>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 生成人类可读的条件摘要。
    /// </summary>
    /// <remarks>
    /// 摘要在<b>服务端</b>生成：界面需要它来展示历史与策略，
    /// 让每个前端都去解析一遍条件 JSON 既重复又容易与后端字段名脱节。
    /// </remarks>
    public static string Summarize(ScreenerRequest request)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Preset))
        {
            parts.Add($"预设 {request.Preset}");
        }

        foreach (var range in request.Ranges ?? [])
        {
            var min = range.Min?.ToString("0.##") ?? "不限";
            var max = range.Max?.ToString("0.##") ?? "不限";
            parts.Add($"{range.Field} ∈ [{min}, {max}]");
        }

        foreach (var item in request.Enums ?? [])
        {
            if (item.Values is { Count: > 0 })
            {
                parts.Add($"{item.Field} ∈ {string.Join('/', item.Values)}");
            }
        }

        foreach (var flag in request.Flags ?? [])
        {
            if (flag.Field == ScreenerFields.IsSt)
            {
                parts.Add(flag.Value ? "仅 ST" : "排除 ST");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.SortBy))
        {
            parts.Add($"按 {request.SortBy} {(request.SortDesc ? "降序" : "升序")}");
        }

        return parts.Count == 0 ? "无条件（全市场）" : string.Join("；", parts);
    }
}

/// <summary>
/// 分位数与直方图计算。纯函数，便于单测逐点核对。
/// </summary>
/// <remarks>
/// 分位数采用<b>线性插值</b>（与主流统计软件一致），而不是「取第 k 个元素」：
/// 后者在样本量变化时抖动明显，用它做「结果集中在哪里」的判断会不稳。
/// </remarks>
public static class DistributionStats
{
    /// <summary>按字段取值的函数签名。</summary>
    public delegate decimal? Selector(ScreenerRowDto row);

    /// <summary>字段名到「取值函数 + 中文名 + 单位」的映射。</summary>
    public static (Selector Selector, string Name, string Unit)? Resolve(string field) => field switch
    {
        ScreenerFields.Pct => (row => row.Pct, "涨跌幅", "%"),
        ScreenerFields.Turnover => (row => row.Turnover, "换手率", "%"),
        ScreenerFields.VolRatio => (row => row.VolRatio, "量比", "倍"),
        ScreenerFields.Amount => (row => row.Amount, "成交额", "亿元"),
        ScreenerFields.Cap => (row => row.Cap, "总市值", "亿元"),
        ScreenerFields.FloatCap => (row => row.FloatCap, "流通市值", "亿元"),
        ScreenerFields.PeTtm => (row => row.PeTtm, "PE(TTM)", "倍"),
        ScreenerFields.Pb => (row => row.Pb, "PB", "倍"),
        ScreenerFields.Price => (row => row.Price, "现价", "元"),
        _ => null
    };

    /// <summary>
    /// 计算分布统计。
    /// </summary>
    /// <param name="rows">结果行。</param>
    /// <param name="field">统计字段。</param>
    /// <param name="binCount">直方图分箱数（默认 12）。</param>
    public static ScreenerDistributionDto? Compute(
        IReadOnlyList<ScreenerRowDto> rows,
        string field,
        int binCount = 12)
    {
        var resolved = Resolve(field);
        if (resolved is null)
        {
            return null;
        }

        // 取出选择器：具名元组里的委托不能直接在 LINQ 里用（推断不出泛型参数），
        // 因此先取到本地变量
        var selector = resolved.Value.Selector;

        // 缺失值不参与统计：PE 为负（亏损）的标的没有「PE 高低」可言，
        // 把它们算进去会让分位数失去意义
        var values = rows
            .Select(row => selector(row))
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .OrderBy(value => value)
            .ToList();

        if (values.Count == 0)
        {
            return new ScreenerDistributionDto(
                field, resolved.Value.Name, resolved.Value.Unit,
                0, null, null, null, null, null, []);
        }

        var min = values[0];
        var max = values[^1];
        var bins = BuildBins(values, min, max, Math.Clamp(binCount, 4, 40));

        return new ScreenerDistributionDto(
            Field: field,
            FieldName: resolved.Value.Name,
            Unit: resolved.Value.Unit,
            Count: values.Count,
            Min: Math.Round(min, 2),
            P25: Quantile(values, 0.25m),
            Median: Quantile(values, 0.5m),
            P75: Quantile(values, 0.75m),
            Max: Math.Round(max, 2),
            Bins: bins);
    }

    /// <summary>线性插值分位数（<paramref name="values"/> 必须已升序）。</summary>
    internal static decimal Quantile(IReadOnlyList<decimal> values, decimal q)
    {
        if (values.Count == 1)
        {
            return Math.Round(values[0], 2);
        }

        var position = q * (values.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);

        if (lower == upper)
        {
            return Math.Round(values[lower], 2);
        }

        var weight = position - lower;
        var interpolated = values[lower] * (1 - weight) + values[upper] * weight;
        return Math.Round(interpolated, 2);
    }

    /// <summary>等宽分箱。极差为 0（所有值相同）时退化为单箱，避免除零。</summary>
    internal static List<ScreenerHistogramBinDto> BuildBins(
        IReadOnlyList<decimal> sorted,
        decimal min,
        decimal max,
        int binCount)
    {
        if (max == min)
        {
            return [new ScreenerHistogramBinDto(Math.Round(min, 2), Math.Round(max, 2), sorted.Count)];
        }

        var width = (max - min) / binCount;
        var bins = new List<ScreenerHistogramBinDto>(binCount);
        var counts = new int[binCount];

        foreach (var value in sorted)
        {
            // 最大值落在最后一箱（右端闭区间），否则它会掉到区间外
            var index = value >= max ? binCount - 1 : (int)((value - min) / width);
            counts[Math.Clamp(index, 0, binCount - 1)]++;
        }

        for (var i = 0; i < binCount; i++)
        {
            var from = min + width * i;
            var to = i == binCount - 1 ? max : min + width * (i + 1);
            bins.Add(new ScreenerHistogramBinDto(
                Math.Round(from, 2),
                Math.Round(to, 2),
                counts[i]));
        }

        return bins;
    }

    /// <summary>把累计次数换算成「小于等于该值」的占比（百分数），用于结果集的分布描述。</summary>
    public static decimal PercentileOf(IReadOnlyList<decimal> sorted, decimal value)
    {
        if (sorted.Count == 0)
        {
            return 0m;
        }

        var belowOrEqual = sorted.Count(item => item <= value);
        return Math.Round((decimal)belowOrEqual / sorted.Count * 100m, 1);
    }
}
