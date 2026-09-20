namespace SA.Contracts.Screener;

/// <summary>
/// 选股条件的一个区间筛选项。
/// </summary>
/// <param name="Field">字段名，取值见 <see cref="ScreenerFields"/>。</param>
/// <param name="Min">下限（含）；null 表示不限。</param>
/// <param name="Max">上限（含）；null 表示不限。</param>
public sealed record ScreenerRange(string Field, decimal? Min, decimal? Max);

/// <summary>
/// 选股条件的枚举筛选项（板块 / 行业）。
/// </summary>
/// <param name="Field">字段名：board / industry。</param>
/// <param name="Values">取值集合；空集合表示不限。</param>
public sealed record ScreenerEnum(string Field, IReadOnlyList<string> Values);

/// <summary>
/// 选股条件的布尔筛选项。
/// </summary>
/// <param name="Field">字段名：isSt。</param>
/// <param name="Value">是否保留该类标的。</param>
public sealed record ScreenerFlag(string Field, bool Value);

/// <summary>
/// 条件选股请求（<c>POST /api/screener</c>）。
/// </summary>
/// <param name="Ranges">区间条件。</param>
/// <param name="Enums">枚举条件。</param>
/// <param name="Flags">布尔条件。</param>
/// <param name="SortBy">排序字段，取值见 <see cref="ScreenerFields"/> 的可排序项。</param>
/// <param name="SortDesc">是否倒序。</param>
/// <param name="Page">页码（1 起）。</param>
/// <param name="PageSize">每页条数（上限 200）。</param>
/// <param name="Preset">预设条件名（与自定义条件二选一，用预设时忽略其余条件）。</param>
public sealed record ScreenerRequest(
    IReadOnlyList<ScreenerRange>? Ranges,
    IReadOnlyList<ScreenerEnum>? Enums,
    IReadOnlyList<ScreenerFlag>? Flags,
    string? SortBy,
    bool SortDesc,
    int Page,
    int PageSize,
    string? Preset);

/// <summary>
/// 选股结果一行。
/// </summary>
/// <param name="Code">代码。</param>
/// <param name="Name">名称。</param>
/// <param name="Board">板块。</param>
/// <param name="Industry">行业。</param>
/// <param name="Price">现价（元）。</param>
/// <param name="Pct">涨跌幅（百分数）。</param>
/// <param name="Turnover">换手率（百分数）。</param>
/// <param name="VolRatio">量比。</param>
/// <param name="Amount">成交额（亿元）。</param>
/// <param name="PeTtm">PE(TTM)。</param>
/// <param name="Pb">PB。</param>
/// <param name="Cap">总市值（亿元）。</param>
/// <param name="FloatCap">流通市值（亿元）。</param>
/// <param name="IsSt">是否 ST。</param>
public sealed record ScreenerRowDto(
    string Code,
    string Name,
    string Board,
    string? Industry,
    decimal Price,
    decimal Pct,
    decimal Turnover,
    decimal VolRatio,
    decimal Amount,
    decimal? PeTtm,
    decimal? Pb,
    decimal Cap,
    decimal FloatCap,
    bool IsSt);

/// <summary>
/// 选股结果。
/// </summary>
/// <param name="Total">命中总数。</param>
/// <param name="Page">当前页。</param>
/// <param name="PageSize">每页条数。</param>
/// <param name="Rows">结果行。</param>
/// <param name="AsOf">行情口径日。</param>
/// <param name="Applied">实际生效的条件说明（界面据此回显，避免「以为筛了其实没筛」）。</param>
/// <param name="ScopeNote">数据范围说明（受限角色）。</param>
public sealed record ScreenerResultDto(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<ScreenerRowDto> Rows,
    string? AsOf,
    IReadOnlyList<string> Applied,
    string? ScopeNote);

/// <summary>
/// 预设条件。
/// </summary>
/// <param name="Key">预设键。</param>
/// <param name="Name">名称。</param>
/// <param name="Description">说明（写清筛选口径，用户不必猜）。</param>
public sealed record ScreenerPresetDto(string Key, string Name, string Description);

/// <summary>
/// 可选字段（界面据此渲染条件编辑器，避免前端硬编码字段名）。
/// </summary>
/// <param name="Field">字段名。</param>
/// <param name="Name">中文名。</param>
/// <param name="Unit">单位。</param>
/// <param name="Min">建议下限（用于输入提示）。</param>
/// <param name="Max">建议上限。</param>
public sealed record ScreenerFieldDto(string Field, string Name, string Unit, decimal? Min, decimal? Max);

/// <summary>
/// 选股器元数据（字段、预设、额度）。
/// </summary>
/// <param name="Fields">可用字段。</param>
/// <param name="Presets">预设条件。</param>
/// <param name="Boards">可选板块。</param>
/// <param name="ExportQuota">当日导出剩余次数。</param>
public sealed record ScreenerMetaDto(
    IReadOnlyList<ScreenerFieldDto> Fields,
    IReadOnlyList<ScreenerPresetDto> Presets,
    IReadOnlyList<string> Boards,
    int ExportQuota);

/// <summary>
/// 选股器字段名常量。与前端、服务端共用同一份定义。
/// </summary>
public static class ScreenerFields
{
    /// <summary>涨跌幅（百分数）。</summary>
    public const string Pct = "pct";

    /// <summary>换手率（百分数）。</summary>
    public const string Turnover = "turnover";

    /// <summary>量比。</summary>
    public const string VolRatio = "volRatio";

    /// <summary>成交额（亿元）。</summary>
    public const string Amount = "amount";

    /// <summary>总市值（亿元）。</summary>
    public const string Cap = "cap";

    /// <summary>流通市值（亿元）。</summary>
    public const string FloatCap = "floatCap";

    /// <summary>PE(TTM)。</summary>
    public const string PeTtm = "peTtm";

    /// <summary>PB。</summary>
    public const string Pb = "pb";

    /// <summary>现价（元）。</summary>
    public const string Price = "price";

    /// <summary>板块。</summary>
    public const string Board = "board";

    /// <summary>行业。</summary>
    public const string Industry = "industry";

    /// <summary>是否 ST。</summary>
    public const string IsSt = "isSt";
}
