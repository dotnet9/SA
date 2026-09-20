namespace SA.Contracts.Risk;

/// <summary>
/// 一条风险项。
/// </summary>
/// <param name="Key">风险键（稳定，便于界面按项展示与后续允许人工确认）。</param>
/// <param name="Category">类别：退市 / 财务 / 质押 / 波动 / 回撤 / 流动性 / 估值。</param>
/// <param name="Level">等级：high / medium / low。</param>
/// <param name="Title">标题。</param>
/// <param name="Detail">依据（写明触发阈值与实际值）。</param>
/// <param name="Metric">当前值（文案）。</param>
/// <param name="Threshold">触发阈值（文案）。</param>
public sealed record RiskItemDto(
    string Key,
    string Category,
    string Level,
    string Title,
    string Detail,
    string? Metric,
    string? Threshold);

/// <summary>
/// 风险监控（<c>GET /api/stocks/{code}/risk</c>）。
/// </summary>
/// <param name="Code">证券代码。</param>
/// <param name="Name">证券名称。</param>
/// <param name="AsOf">数据时间。</param>
/// <param name="Score">风险分（0–100，越高越需要关注）。</param>
/// <param name="Grade">风险等级：低 / 中 / 高。</param>
/// <param name="Items">风险清单（按等级与影响排序）。</param>
/// <param name="Metrics">关键指标（无论是否触发风险都展示，便于对照阈值）。</param>
/// <param name="Insights">结论。</param>
/// <param name="Notes">口径说明。</param>
public sealed record RiskDto(
    string Code,
    string Name,
    string? AsOf,
    int Score,
    string Grade,
    IReadOnlyList<RiskItemDto> Items,
    IReadOnlyList<RiskMetricDto> Metrics,
    IReadOnlyList<string> Insights,
    IReadOnlyList<string> Notes);

/// <summary>
/// 风险关键指标。
/// </summary>
/// <param name="Label">指标名。</param>
/// <param name="Value">值（已格式化）。</param>
/// <param name="Threshold">阈值（已格式化）。</param>
/// <param name="Triggered">是否已触发风险。</param>
public sealed record RiskMetricDto(string Label, string Value, string? Threshold, bool Triggered);
