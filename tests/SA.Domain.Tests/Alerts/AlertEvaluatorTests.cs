using SA.Domain.Alerts;
using SA.Domain.Entities.Alerts;
using SA.Domain.Entities.Market;

namespace SA.Domain.Tests.Alerts;

/// <summary>
/// 提醒规则的触发判定。这是纯函数，因此逐条钉住阈值边界与「数据不足时必须沉默」。
/// </summary>
public class AlertEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 10, 0, 0, TimeSpan.FromHours(8));

    /* ------------------------------------------------------------------
       价格类
       ------------------------------------------------------------------ */

    [Fact]
    public void 价格上穿达到阈值即触发()
    {
        var rule = Rule(AlertRuleTypes.PriceAbove, 300m);
        var quote = Quote(price: 300m);

        var result = AlertEvaluator.Evaluate(rule, quote, [], [], []);

        Assert.True(result.Triggered);
        Assert.Equal("up", result.Level);
    }

    [Fact]
    public void 价格上穿差一分钱不触发()
    {
        var rule = Rule(AlertRuleTypes.PriceAbove, 300m);
        var quote = Quote(price: 299.99m);

        var result = AlertEvaluator.Evaluate(rule, quote, [], [], []);

        Assert.False(result.Triggered);
        Assert.Contains("299.99", result.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void 价格下穿即触发且级别为利空()
    {
        var rule = Rule(AlertRuleTypes.PriceBelow, 250m);
        var quote = Quote(price: 249.5m);

        var result = AlertEvaluator.Evaluate(rule, quote, [], [], []);

        Assert.True(result.Triggered);
        Assert.Equal("down", result.Level);
    }

    [Fact]
    public void 涨跌幅取绝对值因此下跌同样触发()
    {
        var rule = Rule(AlertRuleTypes.ChangeAbs, 5m);
        var quote = Quote(pct: -6.2m);

        var result = AlertEvaluator.Evaluate(rule, quote, [], [], []);

        Assert.True(result.Triggered);
        Assert.Equal("down", result.Level);
    }

    [Fact]
    public void 量比与换手率按阈值判定()
    {
        var volRatio = AlertEvaluator.Evaluate(Rule(AlertRuleTypes.VolRatioAbove, 2m), Quote(volRatio: 2.5m), [], [], []);
        Assert.True(volRatio.Triggered);

        var turnover = AlertEvaluator.Evaluate(Rule(AlertRuleTypes.TurnoverAbove, 10m), Quote(turnover: 9.9m), [], [], []);
        Assert.False(turnover.Triggered);
    }

    [Fact]
    public void 未设置阈值时不触发并说明原因()
    {
        var result = AlertEvaluator.Evaluate(Rule(AlertRuleTypes.PriceAbove, null), Quote(), [], [], []);

        Assert.False(result.Triggered);
        Assert.Contains("阈值", result.Reason!, StringComparison.Ordinal);
    }

    /* ------------------------------------------------------------------
       均线
       ------------------------------------------------------------------ */

    [Fact]
    public void 跌破二十日均线触发()
    {
        // 前 19 天 100 元，第 20 天 100 元 → 均线 100
        var closes = Enumerable.Repeat(100m, 20).ToList();
        var quote = Quote(price: 99m);

        var result = AlertEvaluator.Evaluate(Rule(AlertRuleTypes.BreakMa20, null), quote, closes, [], []);

        Assert.True(result.Triggered);
        Assert.Equal("down", result.Level);
    }

    [Fact]
    public void 未跌破均线不触发()
    {
        var closes = Enumerable.Repeat(100m, 20).ToList();
        var quote = Quote(price: 101m);

        var result = AlertEvaluator.Evaluate(Rule(AlertRuleTypes.BreakMa20, null), quote, closes, [], []);

        Assert.False(result.Triggered);
    }

    [Fact]
    public void 日线不足二十根时保持沉默而不是误报()
    {
        var closes = Enumerable.Repeat(100m, 10).ToList();
        var quote = Quote(price: 50m);

        var result = AlertEvaluator.Evaluate(Rule(AlertRuleTypes.BreakMa20, null), quote, closes, [], []);

        Assert.False(result.Triggered);
        Assert.Contains("样本不足", result.Reason!, StringComparison.Ordinal);
    }

    /* ------------------------------------------------------------------
       新高 / 新低
       ------------------------------------------------------------------ */

    [Fact]
    public void 创近N日新高按此前N日最高价判定()
    {
        // 阈值 5 天：此前 5 天最高 110，今天价格 112 → 触发
        var highs = new List<decimal> { 110m, 105m, 108m, 102m, 109m, 112m };

        var result = AlertEvaluator.Evaluate(Rule(AlertRuleTypes.NewHigh, 5m), Quote(price: 112m), [], highs, []);

        Assert.True(result.Triggered);
        Assert.Contains("此前 5 日", result.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public void 当日价未超过此前最高价则不触发()
    {
        var highs = new List<decimal> { 110m, 105m, 108m, 102m, 109m, 109.5m };

        var result = AlertEvaluator.Evaluate(Rule(AlertRuleTypes.NewHigh, 5m), Quote(price: 109.5m), [], highs, []);

        Assert.False(result.Triggered);
    }

    [Fact]
    public void 创近N日新低按此前N日最低价判定()
    {
        var lows = new List<decimal> { 90m, 95m, 92m, 98m, 91m, 88m };

        var result = AlertEvaluator.Evaluate(Rule(AlertRuleTypes.NewLow, 5m), Quote(price: 88m), [], [], lows);

        Assert.True(result.Triggered);
        Assert.Equal("down", result.Level);
    }

    [Fact]
    public void 新高新低的日线样本不足时保持沉默()
    {
        var highs = new List<decimal> { 100m, 101m };

        var result = AlertEvaluator.Evaluate(Rule(AlertRuleTypes.NewHigh, 60m), Quote(price: 200m), [], highs, []);

        Assert.False(result.Triggered);
        Assert.Contains("样本不足", result.Reason!, StringComparison.Ordinal);
    }

    /* ------------------------------------------------------------------
       事件类型与未知类型
       ------------------------------------------------------------------ */

    [Fact]
    public void 事件类规则不参与评估以免与事件链路重复通知()
    {
        var result = AlertEvaluator.Evaluate(Rule(AlertRuleTypes.EventOccurred, null), Quote(), [], [], []);

        Assert.False(result.Triggered);
        Assert.Contains("事件链路", result.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void 未知规则类型不触发且给出原因()
    {
        var result = AlertEvaluator.Evaluate(Rule("unknown.type", 1m), Quote(), [], [], []);

        Assert.False(result.Triggered);
        Assert.Contains("未知规则类型", result.Reason!, StringComparison.Ordinal);
    }

    /* ------------------------------------------------------------------
       构造工具
       ------------------------------------------------------------------ */

    private static AlertRule Rule(string type, decimal? threshold) =>
        new()
        {
            Id = "rule-1",
            UserId = "user-1",
            Code = "300750",
            RuleType = type,
            Threshold = threshold,
            Enabled = true,
            CreatedAt = Now.AddDays(-1)
        };

    private static QuoteSnapshot Quote(
        decimal price = 300m,
        decimal pct = 1m,
        decimal volRatio = 1m,
        decimal turnover = 1m) =>
        new()
        {
            Code = "300750",
            Price = price,
            Change = 3m,
            Pct = pct,
            Volume = 1_000_000m,
            Amount = 300_000_000m,
            Turnover = turnover,
            VolRatio = volRatio,
            Open = price,
            High = price,
            Low = price,
            PrevClose = price,
            MarketCap = 1_000_000_000m,
            FloatCap = 1_000_000_000m,
            Pe = 20m,
            PeTtm = 20m,
            Pb = 3m,
            AsOf = DateOnly.FromDateTime(Now.DateTime),
            UpdatedAt = Now
        };
}
