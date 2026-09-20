using SA.Domain.Entities.Alerts;

namespace SA.Application.Alerts;

/// <summary>
/// 提醒规则类型的元数据（名称、阈值单位、填写提示）。
/// </summary>
/// <remarks>
/// 集中放在应用层：界面用它渲染下拉框与输入单位，评估器用它判断是否需要阈值，
/// 两处若各写一份必然会出现「界面说要填阈值、评估器却忽略了阈值」这类不一致。
/// </remarks>
public static class AlertRuleCatalog
{
    /// <summary>一条类型定义。</summary>
    /// <param name="Type">类型值。</param>
    /// <param name="Name">中文名。</param>
    /// <param name="Unit">阈值单位；null 表示不需要阈值。</param>
    /// <param name="Hint">填写提示。</param>
    public readonly record struct Definition(string Type, string Name, string? Unit, string Hint);

    /// <summary>全部规则类型。</summary>
    public static IReadOnlyList<Definition> All { get; } =
    [
        new(AlertRuleTypes.PriceAbove, "价格上穿", "元", "当最新价 ≥ 阈值时触发"),
        new(AlertRuleTypes.PriceBelow, "价格下穿", "元", "当最新价 ≤ 阈值时触发"),
        new(AlertRuleTypes.ChangeAbs, "涨跌幅绝对值", "%", "当 |涨跌幅| ≥ 阈值时触发"),
        new(AlertRuleTypes.VolRatioAbove, "量比", "倍", "当量比 ≥ 阈值时触发"),
        new(AlertRuleTypes.TurnoverAbove, "换手率", "%", "当换手率 ≥ 阈值时触发"),
        new(AlertRuleTypes.BreakMa20, "跌破 20 日均线", "元", "当最新价 ≤ 20 日均线时触发（阈值可留空，按均线自动判断）"),
        new(AlertRuleTypes.NewHigh, "创近 N 日新高", "日", "取最近 N 个交易日最高价，当最新价 ≥ 该价时触发；阈值即 N（默认 60）"),
        new(AlertRuleTypes.NewLow, "创近 N 日新低", "日", "取最近 N 个交易日最低价，当最新价 ≤ 该价时触发；阈值即 N（默认 60）"),
        new(AlertRuleTypes.EventOccurred, "事件触发", null, "当有新事件（业绩预告 / 龙虎榜 / 大宗交易等）时触发，不需要阈值")
    ];

    /// <summary>按类型取定义；未知类型返回 null。</summary>
    public static Definition? Find(string? type) =>
        type is null ? null : All.FirstOrDefault(definition => definition.Type == type);

    /// <summary>类型的中文名（未知类型回退为原值）。</summary>
    public static string NameOf(string type) => Find(type)?.Name ?? type;

    /// <summary>
    /// 是否为需要阈值的类型。
    /// </summary>
    /// <remarks>
    /// 「跌破 20 日均线」是例外：阈值留空时按均线自动判断，填了则额外要求价格 ≤ 该阈值。
    /// 因此这里只把「必须有阈值」的判为 true。
    /// </remarks>
    public static bool RequiresThreshold(string type) =>
        type switch
        {
            AlertRuleTypes.PriceAbove or AlertRuleTypes.PriceBelow or AlertRuleTypes.ChangeAbs
                or AlertRuleTypes.VolRatioAbove or AlertRuleTypes.TurnoverAbove => true,
            _ => false
        };
}
