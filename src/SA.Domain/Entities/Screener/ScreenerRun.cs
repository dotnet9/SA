namespace SA.Domain.Entities.Screener;

/// <summary>
/// 一次筛选的执行记录。对应新增表 <c>ScreenerRun</c>。
/// </summary>
/// <remarks>
/// <para>
/// 同一张表同时承担两件事，因为它们本质是同一条数据的不同用法：
/// <list type="bullet">
/// <item><b>筛选日志</b>：每次筛选都写一条（条件 + 命中数 + 时间）；</item>
/// <item><b>我的策略</b>：用户给某条记录起名并标记为策略（<see cref="Name"/> 非空即视为策略）。</item>
/// </list>
/// 分成两张表会带来「策略与历史重复存同一份条件」的问题，且用户「把这次筛选存为策略」时还要跨表搬运。
/// </para>
/// <para>
/// <see cref="RequestJson"/> 存请求体的原始 JSON：策略回放要求<b>完全复现</b>当初的条件，
/// 逐字段建模会在新增筛选字段时让旧记录静默丢字段。
/// </para>
/// </remarks>
public sealed class ScreenerRun
{
    /// <summary>自增主键。</summary>
    public long Id { get; set; }

    /// <summary>执行者。</summary>
    public required string UserId { get; set; }

    /// <summary>筛选条件（请求体 JSON 原文）。</summary>
    public required string RequestJson { get; set; }

    /// <summary>命中数量。</summary>
    public int Total { get; set; }

    /// <summary>人类可读的条件摘要（由服务端生成，界面直接展示，避免前端重解析 JSON）。</summary>
    public string? Summary { get; set; }

    /// <summary>策略名；非空即表示这是一条被用户保存的策略。</summary>
    public string? Name { get; set; }

    /// <summary>是否由预设生成（保存策略时便于标注来源）。</summary>
    public string? PresetKey { get; set; }

    /// <summary>执行时间。</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
