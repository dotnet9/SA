namespace SA.Domain.Entities.Events;

/// <summary>
/// 人工事件标注。对应新增表 <c>EventAnnotation</c>（实施计划 §5.6 的「事件影响方向与强度可人工修正」）。
/// </summary>
/// <remarks>
/// <para>
/// 派生事件（业绩预告、龙虎榜、大宗交易等）的默认影响方向由规则给出，但同一类事件在不同情境下
/// 含义可能相反（例如大宗交易溢价成交偏积极、折价成交偏消极）。因此允许人工覆盖方向与强度，
/// 并保留备注与修改人，便于复核——<b>覆盖的是判读，不是原始数据</b>。
/// </para>
/// <para>
/// 主键是（代码, 事件键）：事件键由「类型 + 日期 + 关键字段」拼成，稳定且可复算，因此重复标注即更新。
/// </para>
/// </remarks>
public sealed class EventAnnotation
{
    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>事件稳定键。</summary>
    public required string EventKey { get; set; }

    /// <summary>人工判定的影响方向：up / down / neutral。</summary>
    public required string Tone { get; set; }

    /// <summary>人工判定的影响强度：1–5。</summary>
    public int Impact { get; set; }

    /// <summary>备注（为什么这样判读）。</summary>
    public string? Note { get; set; }

    /// <summary>修改人用户 Id。</summary>
    public required string UserId { get; set; }

    /// <summary>更新时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
