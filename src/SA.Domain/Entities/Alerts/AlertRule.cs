namespace SA.Domain.Entities.Alerts;

/// <summary>
/// 提醒规则类型。取值与详细设计 §8.1 一致。
/// </summary>
public static class AlertRuleTypes
{
    /// <summary>价格上穿。</summary>
    public const string PriceAbove = "price.above";

    /// <summary>价格下穿。</summary>
    public const string PriceBelow = "price.below";

    /// <summary>涨跌幅超过阈值（绝对值）。</summary>
    public const string ChangeAbs = "change.abs";

    /// <summary>量比超过阈值。</summary>
    public const string VolRatioAbove = "volratio.above";

    /// <summary>换手率超过阈值。</summary>
    public const string TurnoverAbove = "turnover.above";

    /// <summary>成交额超过阈值（亿元）。</summary>
    public const string AmountAbove = "amount.above";

    /// <summary>
    /// 主力资金净额触发（亿元，阈值取绝对值）。
    /// </summary>
    /// <remarks>
    /// 资金类规则的语义是「主力净流入或净流出达到阈值」：阈值存正数、判定看绝对值，
    /// 方向体现在通知文案上（净流入 / 净流出）。用户只需填一个数，
    /// 不必为了分辨方向再建两条规则。
    /// </remarks>
    public const string FundFlowMainAbs = "fundflow.main.abs";

    /// <summary>主力资金连续同向天数达到阈值。</summary>
    public const string FundFlowStreak = "fundflow.streak";

    /// <summary>跌破均线（MA20）。</summary>
    public const string BreakMa20 = "ma20.break";

    /// <summary>创近 N 日新高。</summary>
    public const string NewHigh = "high.new";

    /// <summary>创近 N 日新低。</summary>
    public const string NewLow = "low.new";

    /// <summary>事件触发（业绩预告 / 龙虎榜 / 大宗交易等）。</summary>
    public const string EventOccurred = "event.occurred";
}

/// <summary>
/// 提醒规则。对应详细设计 §3.5 的 <c>AlertRule</c>。
/// </summary>
/// <remarks>
/// 每条规则只服务一个用户与一个标的，且必须带阈值（事件类规则除外）。
/// 「触发过就不再重复触发」由 <see cref="LastTriggeredAt"/> 与冷却窗口控制，
/// 否则盘中价格在阈值附近震荡会产生大量重复通知。
/// </remarks>
public sealed class AlertRule
{
    /// <summary>规则 Id。</summary>
    public required string Id { get; set; }

    /// <summary>所属用户。</summary>
    public required string UserId { get; set; }

    /// <summary>证券代码。</summary>
    public required string Code { get; set; }

    /// <summary>规则类型，取值见 <see cref="AlertRuleTypes"/>。</summary>
    public required string RuleType { get; set; }

    /// <summary>阈值（价格规则为元、比率为百分数、新高新低为天数）；事件类可为空。</summary>
    public decimal? Threshold { get; set; }

    /// <summary>备注（用户自己写的触发理由）。</summary>
    public string? Note { get; set; }

    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>创建时间。</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>最近一次触发时间。</summary>
    public DateTimeOffset? LastTriggeredAt { get; set; }

    /// <summary>累计触发次数。</summary>
    public int TriggerCount { get; set; }
}

/// <summary>
/// 站内通知。对应详细设计 §3.5 的 <c>Notification</c>。
/// </summary>
/// <remarks>
/// 通知与提醒规则是多对一：一条规则可以触发多次通知，通知保留历史以便回溯。
/// 规则被删除时通知<b>不删除</b>（OnDelete 设为级联删除的话历史会消失），
/// 因此只保留规则 Id 的可空引用。
/// </remarks>
public sealed class Notification
{
    /// <summary>自增主键。</summary>
    public long Id { get; set; }

    /// <summary>接收用户。</summary>
    public required string UserId { get; set; }

    /// <summary>标题。</summary>
    public required string Title { get; set; }

    /// <summary>正文。</summary>
    public required string Body { get; set; }

    /// <summary>级别：up / down / info / warn。</summary>
    public required string Level { get; set; }

    /// <summary>相关证券代码。</summary>
    public string? Code { get; set; }

    /// <summary>触发它的规则 Id；规则删除后仍保留该值以便追溯。</summary>
    public string? RuleId { get; set; }

    /// <summary>是否已读。</summary>
    public bool IsRead { get; set; }

    /// <summary>创建时间。</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
