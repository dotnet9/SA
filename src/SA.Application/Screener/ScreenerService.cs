using System.Text;
using SA.Application.Abstractions;
using SA.Application.Authorization;
using SA.Application.Common;
using SA.Application.Market;
using SA.Contracts.Common;
using SA.Contracts.Screener;
using SA.Domain.Common;
using SA.Domain.Entities.Market;

namespace SA.Application.Screener;

/// <summary>
/// 条件选股器。
/// </summary>
/// <remarks>
/// <para>
/// 全部基于内存中的全市场快照做筛选：横截面条件（市值、PE、涨跌幅…）只有在同一时点的
/// 全市场数据上才有意义，而快照恰好就是这个口径。因此本模块<b>不需要新的采集源</b>，
/// 也不做任何跨期计算（那是趋势模块的职责）。
/// </para>
/// <para>
/// 三条约定：
/// </para>
/// <list type="number">
/// <item>缺失值（PE 为负、无行情）<b>不参与</b>区间筛选：把亏损股当成「PE 很低」筛出来会得到反直觉的结果；</item>
/// <item>返回体带 <c>Applied</c>，逐条回显实际生效的条件，避免用户以为筛了其实字段名写错被忽略；</item>
/// <item>数据范围受限的角色只能选到自选内的标的（复用 <see cref="DataScopeFilter"/>）。</item>
/// </list>
/// </remarks>
public sealed class ScreenerService(
    MarketSnapshotCache cache,
    IQuoteSnapshotStore quotes,
    IInstrumentStore instruments,
    DataScopeFilter scopeFilter)
{
    /// <summary>每页上限（与详细设计 §1.3 一致）。</summary>
    private const int MaxPageSize = 200;

    /// <summary>
    /// 预设条件。
    /// </summary>
    /// <remarks>
    /// 每个预设的说明都写清口径，用户不必猜「低估值」到底是 PE 低于多少。
    /// </remarks>
    private static readonly (string Key, string Name, string Description, Func<ScreenerRowDto, bool> Filter)[] Presets =
    [
        ("large-value", "大盘低估值", "总市值 ≥ 500 亿，PE(TTM) 在 0–15 之间，排除 ST 与停牌",
            row => row.Cap >= 500m && row.PeTtm is > 0 and <= 15m && !row.IsSt && row.Price > 0),
        ("active-turnover", "交投活跃", "换手率 ≥ 5% 且成交额 ≥ 5 亿，排除 ST 与停牌",
            row => row.Turnover >= 5m && row.Amount >= 5m && !row.IsSt && row.Price > 0),
        ("strong-today", "今日强势", "涨幅 ≥ 3% 且 量比 ≥ 1.5，排除 ST 与停牌",
            row => row.Pct >= 3m && row.VolRatio >= 1.5m && !row.IsSt && row.Price > 0),
        ("pullback", "今日回调", "跌幅 ≤ -3% 且换手率 ≥ 2%，排除 ST 与停牌（用于找错杀）",
            row => row.Pct <= -3m && row.Turnover >= 2m && !row.IsSt && row.Price > 0),
        ("small-cap", "小市值", "总市值 ≤ 100 亿、成交额 ≥ 1 亿、PE(TTM) > 0，排除 ST 与停牌",
            row => row.Cap <= 100m && row.Amount >= 1m && row.PeTtm is > 0 && !row.IsSt && row.Price > 0),
        ("volume-spike", "放量异动", "量比 ≥ 2 且换手率 ≥ 3%，排除 ST 与停牌",
            row => row.VolRatio >= 2m && row.Turnover >= 3m && !row.IsSt && row.Price > 0)
    ];

    /// <summary>可筛选字段。</summary>
    private static readonly ScreenerFieldDto[] Fields =
    [
        new(ScreenerFields.Pct, "涨跌幅", "%", -20m, 20m),
        new(ScreenerFields.Turnover, "换手率", "%", 0m, 30m),
        new(ScreenerFields.VolRatio, "量比", "倍", 0m, 10m),
        new(ScreenerFields.Amount, "成交额", "亿元", 0m, 100m),
        new(ScreenerFields.Cap, "总市值", "亿元", 0m, 5000m),
        new(ScreenerFields.FloatCap, "流通市值", "亿元", 0m, 5000m),
        new(ScreenerFields.PeTtm, "PE(TTM)", "倍", 0m, 100m),
        new(ScreenerFields.Pb, "PB", "倍", 0m, 20m),
        new(ScreenerFields.Price, "现价", "元", 0m, 1000m)
    ];

    /// <summary>
    /// 取选股器元数据（字段、预设、板块、导出额度）。
    /// </summary>
    /// <param name="exportQuota">今日剩余导出次数；<c>-1</c> 表示未配置上限（不限）。</param>
    /// <param name="exportRowLimit">单次导出行数上限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<ServiceResult<ScreenerMetaDto>> GetMetaAsync(
        int exportQuota,
        int exportRowLimit,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var boards = cache.Current.Instruments.Values
            .Select(instrument => instrument.Board)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(board => board, StringComparer.Ordinal)
            .ToList();

        return ServiceResult<ScreenerMetaDto>.Success(new ScreenerMetaDto(
            Fields: Fields,
            Presets: Presets.Select(preset => new ScreenerPresetDto(preset.Key, preset.Name, preset.Description)).ToList(),
            Boards: boards,
            ExportQuota: exportQuota,
            ExportRowLimit: exportRowLimit));
    }

    /// <summary>
    /// 执行筛选。
    /// </summary>
    public async Task<ServiceResult<ScreenerResultDto>> RunAsync(
        ScreenerRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        var snapshot = cache.Current;
        if (snapshot.Rows.Count == 0)
        {
            return ServiceResult<ScreenerResultDto>.Fail(ErrorCode.DataNotReady, "全市场快照正在采集，请稍后重试");
        }

        var allowed = await scopeFilter.AllowedAsync(cancellationToken).ConfigureAwait(false);

        var rows = snapshot.Rows
            .Where(row => DataScopeFilter.IsVisible(allowed, row.Code))
            .Select(row => ToRow(row, snapshot))
            .Where(row => row is not null)
            .Select(row => row!)
            .ToList();

        var applied = new List<string>();

        // 预设优先：用预设时忽略自定义条件（否则两者叠加的结果无法解释）
        if (!string.IsNullOrWhiteSpace(request.Preset))
        {
            var preset = Presets.FirstOrDefault(item => item.Key == request.Preset);
            if (preset.Filter is null)
            {
                return ServiceResult<ScreenerResultDto>.Fail(
                    ErrorCode.InvalidParameter,
                    $"未知的预设条件：{request.Preset}");
            }

            rows = rows.Where(preset.Filter).ToList();
            applied.Add($"预设「{preset.Name}」：{preset.Description}");
        }
        else
        {
            rows = ApplyRanges(rows, request.Ranges, applied);
            rows = ApplyEnums(rows, request.Enums, applied);
            rows = ApplyFlags(rows, request.Flags, applied);
        }

        var sortBy = string.IsNullOrWhiteSpace(request.SortBy) ? ScreenerFields.Amount : request.SortBy;
        rows = Sort(rows, sortBy, request.SortDesc);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 50 : request.PageSize, 1, MaxPageSize);
        var total = rows.Count;

        return ServiceResult<ScreenerResultDto>.Success(new ScreenerResultDto(
            Total: total,
            Page: page,
            PageSize: pageSize,
            Rows: rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            AsOf: snapshot.AsOf == DateOnly.MinValue ? null : SaTime.Format(snapshot.AsOf),
            Applied: applied.Count == 0 ? ["未设置条件：返回全市场按成交额倒序的结果"] : applied,
            ScopeNote: allowed is null ? null : "当前账号的数据范围为「仅自选股」，结果已按自选范围过滤。"));
    }

    /// <summary>
    /// 导出命中结果为 CSV（受 <c>export.data</c> 权限与每日配额约束）。
    /// </summary>
    /// <remarks>
    /// CSV 用 UTF-8 BOM：Excel 打开无 BOM 的 UTF-8 CSV 会把中文显示成乱码。
    /// 导出条数上限同样由调用方（端点）按配额传入，本方法只负责生成内容。
    /// </remarks>
    public async Task<ServiceResult<(byte[] Content, string FileName, int Rows)>> ExportAsync(
        ScreenerRequest request,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(
            request with { Page = 1, PageSize = Math.Clamp(maxRows, 1, MaxPageSize) },
            cancellationToken).ConfigureAwait(false);

        if (!result.Ok || result.Value is null)
        {
            return ServiceResult<(byte[], string, int)>.Fail(result.Error, result.Message ?? "导出失败");
        }

        var rows = result.Value.Rows;
        var builder = new StringBuilder();

        // 表头与筛选条件一并写入：导出的文件离开系统后仍能自证口径
        builder.AppendLine("# 股析 SA 选股结果导出");
        builder.AppendLine($"# 数据时间：{result.Value.AsOf ?? "—"}");
        foreach (var note in result.Value.Applied)
        {
            builder.AppendLine($"# 条件：{note}");
        }

        if (result.Value.ScopeNote is not null)
        {
            builder.AppendLine($"# 范围：{result.Value.ScopeNote}");
        }

        builder.AppendLine("代码,名称,板块,行业,现价,涨跌幅%,换手率%,量比,成交额(亿),PE(TTM),PB,总市值(亿),流通市值(亿),ST");

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',',
                Csv(row.Code),
                Csv(row.Name),
                Csv(row.Board),
                Csv(row.Industry ?? string.Empty),
                row.Price,
                row.Pct,
                row.Turnover,
                row.VolRatio,
                row.Amount,
                row.PeTtm?.ToString() ?? string.Empty,
                row.Pb?.ToString() ?? string.Empty,
                row.Cap,
                row.FloatCap,
                row.IsSt ? "是" : "否"));
        }

        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
        var fileName = $"screener-{SaTime.Now:yyyyMMdd-HHmmss}.csv";

        return ServiceResult<(byte[], string, int)>.Success((content, fileName, rows.Count));
    }

    /* ------------------------------------------------------------------
       条件应用
       ------------------------------------------------------------------ */

    private static List<ScreenerRowDto> ApplyRanges(
        List<ScreenerRowDto> rows,
        IReadOnlyList<ScreenerRange>? ranges,
        List<string> applied)
    {
        if (ranges is null || ranges.Count == 0)
        {
            return rows;
        }

        foreach (var range in ranges)
        {
            if (!IsRangeField(range.Field))
            {
                // 未知字段不静默忽略：写进 Applied 让人看到「这条没生效」
                applied.Add($"忽略未知字段：{range.Field}");
                continue;
            }

            var before = rows.Count;
            rows = rows.Where(row => InRange(Value(row, range.Field), range.Min, range.Max)).ToList();

            applied.Add(
                $"{FieldName(range.Field)} ∈ [{Format(range.Min)}, {Format(range.Max)}]"
                + $"（命中 {rows.Count}，筛掉 {before - rows.Count}）");
        }

        return rows;
    }

    private static List<ScreenerRowDto> ApplyEnums(
        List<ScreenerRowDto> rows,
        IReadOnlyList<ScreenerEnum>? enums,
        List<string> applied)
    {
        if (enums is null || enums.Count == 0)
        {
            return rows;
        }

        foreach (var item in enums)
        {
            if (item.Values is null || item.Values.Count == 0)
            {
                continue;
            }

            var values = item.Values.ToHashSet(StringComparer.Ordinal);

            switch (item.Field)
            {
                case ScreenerFields.Board:
                    rows = rows.Where(row => values.Contains(row.Board)).ToList();
                    applied.Add($"板块 ∈ [{string.Join('/', values)}]（命中 {rows.Count}）");
                    break;

                case ScreenerFields.Industry:
                    rows = rows.Where(row => row.Industry is not null && values.Contains(row.Industry)).ToList();
                    applied.Add($"行业 ∈ [{string.Join('/', values)}]（命中 {rows.Count}）");
                    break;

                default:
                    applied.Add($"忽略未知枚举字段：{item.Field}");
                    break;
            }
        }

        return rows;
    }

    private static List<ScreenerRowDto> ApplyFlags(
        List<ScreenerRowDto> rows,
        IReadOnlyList<ScreenerFlag>? flags,
        List<string> applied)
    {
        if (flags is null || flags.Count == 0)
        {
            return rows;
        }

        foreach (var flag in flags)
        {
            if (flag.Field != ScreenerFields.IsSt)
            {
                applied.Add($"忽略未知布尔字段：{flag.Field}");
                continue;
            }

            // 默认行为是「排除 ST」：绝大多数选股场景都不想要 ST，因此勾选语义是「保留 ST」
            rows = flag.Value
                ? rows.Where(row => row.IsSt).ToList()
                : rows.Where(row => !row.IsSt).ToList();

            applied.Add(flag.Value ? "仅保留 ST 标的" : "排除 ST 标的");
        }

        return rows;
    }

    private static List<ScreenerRowDto> Sort(List<ScreenerRowDto> rows, string sortBy, bool desc)
    {
        Func<ScreenerRowDto, decimal> selector = sortBy switch
        {
            ScreenerFields.Pct => row => row.Pct,
            ScreenerFields.Turnover => row => row.Turnover,
            ScreenerFields.VolRatio => row => row.VolRatio,
            ScreenerFields.Cap => row => row.Cap,
            ScreenerFields.FloatCap => row => row.FloatCap,
            // 缺失估值的标的排在最后：用 -1 占位，倒序时自然落到末尾
            ScreenerFields.PeTtm => row => row.PeTtm ?? -1m,
            ScreenerFields.Pb => row => row.Pb ?? -1m,
            ScreenerFields.Price => row => row.Price,
            _ => row => row.Amount
        };

        return desc
            ? rows.OrderByDescending(selector).ThenBy(row => row.Code, StringComparer.Ordinal).ToList()
            : rows.OrderBy(selector).ThenBy(row => row.Code, StringComparer.Ordinal).ToList();
    }

    private static bool IsRangeField(string field) =>
        field is ScreenerFields.Pct or ScreenerFields.Turnover or ScreenerFields.VolRatio
            or ScreenerFields.Amount or ScreenerFields.Cap or ScreenerFields.FloatCap
            or ScreenerFields.PeTtm or ScreenerFields.Pb or ScreenerFields.Price;

    /// <summary>
    /// 取字段值。估值返回 null 而不是 0：亏损股的 PE 必须能区分于「PE = 0」。
    /// </summary>
    private static decimal? Value(ScreenerRowDto row, string field) => field switch
    {
        ScreenerFields.Pct => row.Pct,
        ScreenerFields.Turnover => row.Turnover,
        ScreenerFields.VolRatio => row.VolRatio,
        ScreenerFields.Amount => row.Amount,
        ScreenerFields.Cap => row.Cap,
        ScreenerFields.FloatCap => row.FloatCap,
        ScreenerFields.PeTtm => row.PeTtm,
        ScreenerFields.Pb => row.Pb,
        ScreenerFields.Price => row.Price,
        _ => null
    };

    /// <summary>
    /// 区间判定：值为 null（缺失）时一律不命中，避免把亏损股当作「低 PE」筛出来。
    /// </summary>
    private static bool InRange(decimal? value, decimal? min, decimal? max)
    {
        if (value is null)
        {
            return false;
        }

        if (min is not null && value < min)
        {
            return false;
        }

        return max is null || value <= max;
    }

    private static string FieldName(string field) =>
        Fields.FirstOrDefault(item => item.Field == field)?.Name ?? field;

    private static string Format(decimal? value) => value?.ToString("0.##") ?? "不限";

    /// <summary>CSV 字段转义：含逗号、引号或换行时用双引号包裹并转义内部引号。</summary>
    private static string Csv(string value)
    {
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static ScreenerRowDto? ToRow(QuoteSnapshot row, MarketSnapshotCache.Snapshot snapshot)
    {
        // 无价格的标的（停牌 / 退市）不进入选股结果：它们的比率字段都是 0，会污染排序与统计
        if (row.Price <= 0)
        {
            return null;
        }

        snapshot.Instruments.TryGetValue(row.Code, out var instrument);

        return new ScreenerRowDto(
            Code: row.Code,
            Name: instrument?.Name ?? row.Code,
            Board: instrument?.Board ?? MarketCodes.BoardOf(row.Code),
            Industry: instrument?.Industry,
            Price: Display.Round(row.Price),
            Pct: Display.Round(row.Pct),
            Turnover: Display.Round(row.Turnover),
            VolRatio: Display.Round(row.VolRatio),
            Amount: Display.ToYi(row.Amount),
            PeTtm: row.PeTtm > 0 ? Display.Round(row.PeTtm) : null,
            Pb: row.Pb > 0 ? Display.Round(row.Pb) : null,
            Cap: Display.ToYi(row.MarketCap),
            FloatCap: Display.ToYi(row.FloatCap),
            IsSt: instrument?.IsSt ?? false);
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (!cache.IsEmpty)
        {
            return;
        }

        var rows = await quotes.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return;
        }

        var instrumentRows = await instruments.GetAllAsync(cancellationToken).ConfigureAwait(false);
        cache.Replace(rows, instrumentRows.ToDictionary(item => item.Code, StringComparer.Ordinal));
    }
}
