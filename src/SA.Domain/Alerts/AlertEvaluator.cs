using SA.Domain.Entities.Alerts;
using SA.Domain.Entities.Market;

namespace SA.Domain.Alerts;

/// <summary>
/// 一次规则评估的结果。
/// </summary>
/// <param name="Triggered">是否触发。</param>
/// <param name="Reason">未触发的原因（用于排障与界面解释「为什么没响」）。</param>
/// <param name="Title">触发标题。</param>
/// <param name="Body">触发正文（含实际值）。</param>
/// <param name="Level">级别：up / down / info。</param>
public readonly record struct AlertEvaluation(bool Triggered, string? Reason, string? Title, string? Body, string? Level)
{
    /// <summary>未触发。</summary>
    public static AlertEvaluation NotTriggered(string reason) => new(false, reason, null, null, null);

    /// <summary>触发。</summary>
    public static AlertEvaluation Fired(string title, string body, string level) =>
        new(true, null, title, body, level);
}

/// <summary>
/// 提醒规则的触发判定。纯函数、无依赖，因此可以用构造数据逐条钉住边界。
/// </summary>
/// <remarks>
/// <para>
/// 判定原则：<b>只做能算准的规则</b>。所有比较都用当前快照与日线序列的确定值，
/// 不做任何预测或模糊匹配。数据不足时返回「未触发 + 原因」，而不是勉强判定——
/// 例如没有日线就不能判断「创近 N 日新高」，此时必须保持沉默而不是误报。
/// </para>
/// <para>
/// 冷却窗口：价格在阈值附近震荡时，逐笔数据会让规则反复触发，产生刷屏。
/// 因此同一规则在 <see cref="Cooldown"/> 内只触发一次。
/// </para>
/// </remarks>
public static class AlertEvaluator
{
    /// <summary>同一规则的触发冷却时间。</summary>
    public static TimeSpan Cooldown { get; } = TimeSpan.FromMinutes(30);

    /// <summary>上市首日不设涨跌幅的标的默认不参与「创新高/新低」以外的判断？这里统一按代码口径处理，不做特例。</summary>
    private const int DefaultNewHighDays = 60;

    /// <summary>
    /// 核心判定。
    /// </summary>
    /// <param name="rule">规则。</param>
    /// <param name="quote">最新快照。</param>
    /// <param name="closes">日线收盘（升序）。</param>
    /// <param name="highs">日线最高（升序）。</param>
    /// <param name="lows">日线最低（升序）。</param>
    /// <param name="fundFlow">个股逐日资金流主力净额（单位：元，升序）；资金类规则需要它。</param>
    public static AlertEvaluation Evaluate(
        AlertRule rule,
        QuoteSnapshot quote,
        IReadOnlyList<decimal> closes,
        IReadOnlyList<decimal> highs,
        IReadOnlyList<decimal> lows,
        IReadOnlyList<decimal>? fundFlow = null)
    {
        var name = quote.Code;

        switch (rule.RuleType)
        {
            case AlertRuleTypes.PriceAbove:
                if (rule.Threshold is not { } above)
                {
                    return AlertEvaluation.NotTriggered("未设置阈值");
                }

                return quote.Price >= above
                    ? AlertEvaluation.Fired(
                        $"价格上穿 {above:F2} 元",
                        $"最新价 {quote.Price:F2} 元，已达到阈值 {above:F2} 元。",
                        "up")
                    : AlertEvaluation.NotTriggered($"最新价 {quote.Price:F2} < 阈值 {above:F2}");

            case AlertRuleTypes.PriceBelow:
                if (rule.Threshold is not { } below)
                {
                    return AlertEvaluation.NotTriggered("未设置阈值");
                }

                return quote.Price <= below
                    ? AlertEvaluation.Fired(
                        $"价格下穿 {below:F2} 元",
                        $"最新价 {quote.Price:F2} 元，已跌破阈值 {below:F2} 元。",
                        "down")
                    : AlertEvaluation.NotTriggered($"最新价 {quote.Price:F2} > 阈值 {below:F2}");

            case AlertRuleTypes.ChangeAbs:
                if (rule.Threshold is not { } changeThreshold)
                {
                    return AlertEvaluation.NotTriggered("未设置阈值");
                }

                var absChange = Math.Abs(quote.Pct);
                return absChange >= changeThreshold
                    ? AlertEvaluation.Fired(
                        $"涨跌幅达到 {absChange:F2}%",
                        $"最新涨跌幅 {quote.Pct:+0.00;-0.00}%，绝对値已达到阈值 {changeThreshold:F2}%。",
                        quote.Pct >= 0 ? "up" : "down")
                    : AlertEvaluation.NotTriggered($"|涨跌幅| {absChange:F2}% < 阈值 {changeThreshold:F2}%");

            case AlertRuleTypes.VolRatioAbove:
                if (rule.Threshold is not { } volRatio)
                {
                    return AlertEvaluation.NotTriggered("未设置阈值");
                }

                return quote.VolRatio >= volRatio
                    ? AlertEvaluation.Fired(
                        $"量比达到 {quote.VolRatio:F2}",
                        $"最新量比 {quote.VolRatio:F2}，已达到阈值 {volRatio:F2}。",
                        quote.Pct >= 0 ? "up" : "down")
                    : AlertEvaluation.NotTriggered($"量比 {quote.VolRatio:F2} < 阈值 {volRatio:F2}");

            case AlertRuleTypes.TurnoverAbove:
                if (rule.Threshold is not { } turnover)
                {
                    return AlertEvaluation.NotTriggered("未设置阈值");
                }

                return quote.Turnover >= turnover
                    ? AlertEvaluation.Fired(
                        $"换手率达到 {quote.Turnover:F2}%",
                        $"最新换手率 {quote.Turnover:F2}%，已达到阈值 {turnover:F2}%。",
                        quote.Pct >= 0 ? "up" : "down")
                    : AlertEvaluation.NotTriggered($"换手率 {quote.Turnover:F2}% < 阈值 {turnover:F2}%");

            case AlertRuleTypes.AmountAbove:
                if (rule.Threshold is not { } amountYi)
                {
                    return AlertEvaluation.NotTriggered("未设置阈值");
                }

                // 快照的成交额以元存储，阈值以亿元填写：比较前必须换算到同一单位
                var amount = quote.Amount / 100_000_000m;
                return amount >= amountYi
                    ? AlertEvaluation.Fired(
                        $"成交额达到 {amount:F2} 亿",
                        $"当前成交额 {amount:F2} 亿元，已达到阈值 {amountYi:F2} 亿元。",
                        quote.Pct >= 0 ? "up" : "down")
                    : AlertEvaluation.NotTriggered($"成交额 {amount:F2} 亿 < 阈值 {amountYi:F2} 亿");

            case AlertRuleTypes.FundFlowMainAbs:
            {
                if (rule.Threshold is not { } flowThreshold)
                {
                    return AlertEvaluation.NotTriggered("未设置阈值");
                }

                if (fundFlow is null || fundFlow.Count == 0)
                {
                    // 没有资金流数据时保持沉默：资金流按日发布，当日可能尚未生成
                    return AlertEvaluation.NotTriggered("暂无资金流数据（按日发布，当日可能尚未生成）");
                }

                var mainNet = fundFlow[^1] / 100_000_000m;
                var abs = Math.Abs(mainNet);

                return abs >= flowThreshold
                    ? AlertEvaluation.Fired(
                        mainNet >= 0 ? $"主力净流入 {mainNet:F2} 亿" : $"主力净流出 {abs:F2} 亿",
                        $"最近一日主力净额 {mainNet:+0.00;-0.00} 亿元，绝对值已达到阈值 {flowThreshold:F2} 亿元。",
                        mainNet >= 0 ? "up" : "down")
                    : AlertEvaluation.NotTriggered($"主力净额绝对值 {abs:F2} 亿 < 阈值 {flowThreshold:F2} 亿");
            }

            case AlertRuleTypes.FundFlowStreak:
            {
                if (rule.Threshold is not { } streakDays)
                {
                    return AlertEvaluation.NotTriggered("未设置阈值");
                }

                if (fundFlow is null || fundFlow.Count == 0)
                {
                    return AlertEvaluation.NotTriggered("暂无资金流数据");
                }

                var required = (int)Math.Clamp(streakDays, 2, 20);
                var direction = fundFlow[^1] >= 0;
                var streak = 0;

                // 从最后一天往前数同向天数；0 视作中断（既不算流入也不算流出）
                for (var i = fundFlow.Count - 1; i >= 0; i--)
                {
                    var current = fundFlow[i];
                    if (current == 0 || (current >= 0) != direction)
                    {
                        break;
                    }

                    streak++;
                }

                return streak >= required
                    ? AlertEvaluation.Fired(
                        $"主力连续 {streak} 日{(direction ? "净流入" : "净流出")}",
                        $"最近 {streak} 个交易日主力资金持续{(direction ? "净流入" : "净流出")}，已达到阈值 {required} 天。",
                        direction ? "up" : "down")
                    : AlertEvaluation.NotTriggered($"主力连续同向仅 {streak} 天 < 阈值 {required} 天");
            }

            case AlertRuleTypes.BreakMa20:
                if (closes.Count < 20)
                {
                    // 日线不足 20 根时算不出均线，保持沉默而不是误报
                    return AlertEvaluation.NotTriggered($"日线样本不足（{closes.Count}/20）");
                }

                var ma20 = closes.TakeLast(20).Average();
                var extra = rule.Threshold;

                if (quote.Price <= ma20 && (extra is null || quote.Price <= extra))
                {
                    return AlertEvaluation.Fired(
                        "跌破 20 日均线",
                        $"最新价 {quote.Price:F2} 元，20 日均线 {ma20:F2} 元"
                        + (extra is null ? "。" : $"，阈值 {extra:F2} 元。"),
                        "down");
                }

                return AlertEvaluation.NotTriggered($"最新价 {quote.Price:F2} 未跌破 20 日均线 {ma20:F2}");

            case AlertRuleTypes.NewHigh:
            {
                var days = (int)Math.Clamp(rule.Threshold ?? DefaultNewHighDays, 5, 250);
                if (highs.Count < days)
                {
                    return AlertEvaluation.NotTriggered($"日线样本不足（{highs.Count}/{days}）");
                }

                // 不含当日：比较对象是「此前 N 日」的最高价，否则当日创新高永远无法判定
                var previousHigh = highs.TakeLast(days + 1).Take(days).Max();
                return quote.Price >= previousHigh
                    ? AlertEvaluation.Fired(
                        $"创近 {days} 日新高",
                        $"最新价 {quote.Price:F2} 元，超过此前 {days} 日最高价 {previousHigh:F2} 元。",
                        "up")
                    : AlertEvaluation.NotTriggered($"最新价 {quote.Price:F2} < 近 {days} 日最高 {previousHigh:F2}");
            }

            case AlertRuleTypes.NewLow:
            {
                var days = (int)Math.Clamp(rule.Threshold ?? DefaultNewHighDays, 5, 250);
                if (lows.Count < days)
                {
                    return AlertEvaluation.NotTriggered($"日线样本不足（{lows.Count}/{days}）");
                }

                var previousLow = lows.TakeLast(days + 1).Take(days).Min();
                return quote.Price <= previousLow
                    ? AlertEvaluation.Fired(
                        $"创近 {days} 日新低",
                        $"最新价 {quote.Price:F2} 元，低于此前 {days} 日最低价 {previousLow:F2} 元。",
                        "down")
                    : AlertEvaluation.NotTriggered($"最新价 {quote.Price:F2} > 近 {days} 日最低 {previousLow:F2}");
            }

            case AlertRuleTypes.EventOccurred:
                // 事件类规则由事件服务在写入新事件时直接产生通知（这里不做二次判定），
                // 因此评估器对它保持沉默，避免与事件链路重复通知
                return AlertEvaluation.NotTriggered("事件类规则由事件链路直接通知");

            default:
                return AlertEvaluation.NotTriggered($"未知规则类型：{rule.RuleType}");
        }
    }
}
