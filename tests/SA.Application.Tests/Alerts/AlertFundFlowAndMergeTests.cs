using SA.Application.Alerts;
using SA.Domain.Alerts;
using SA.Domain.Common;
using SA.Domain.Entities.Alerts;
using SA.Domain.Entities.Market;

namespace SA.Application.Tests.Alerts;

/// <summary>
/// 资金类提醒规则、合并窗口与免打扰。
/// </summary>
/// <remarks>
/// 这三块都是「不加测就会静默出错」的逻辑：资金类规则的阈值单位（亿元 vs 元）、
/// 合并后的级别取舍、跨零点的免打扰窗口，任何一个写反都不会抛异常，只会让提醒失效或刷屏。
/// </remarks>
public class AlertFundFlowAndMergeTests
{
    /* ------------------------------------------------------------------
       资金类规则
       ------------------------------------------------------------------ */

    [Fact]
    public void 主力资金净额按亿元阈值判定且方向体现在文案()
    {
        // 主力净流入 2.5 亿元（以元存储）
        var fundFlow = new List<decimal> { 250_000_000m };

        var fired = AlertEvaluator.Evaluate(
            Rule(AlertRuleTypes.FundFlowMainAbs, 2m), Quote(), [], [], [], fundFlow);

        Assert.True(fired.Triggered);
        Assert.Equal("up", fired.Level);
        Assert.Contains("净流入", fired.Title!, StringComparison.Ordinal);

        var quiet = AlertEvaluator.Evaluate(
            Rule(AlertRuleTypes.FundFlowMainAbs, 3m), Quote(), [], [], [], fundFlow);

        Assert.False(quiet.Triggered);
    }

    [Fact]
    public void 主力资金净流出同样触发且级别为利空()
    {
        var fundFlow = new List<decimal> { -180_000_000m };

        var result = AlertEvaluator.Evaluate(
            Rule(AlertRuleTypes.FundFlowMainAbs, 1m), Quote(), [], [], [], fundFlow);

        Assert.True(result.Triggered);
        Assert.Equal("down", result.Level);
        Assert.Contains("净流出", result.Title!, StringComparison.Ordinal);
    }

    [Fact]
    public void 无资金流数据时保持沉默而不是误判为未触发()
    {
        var result = AlertEvaluator.Evaluate(
            Rule(AlertRuleTypes.FundFlowMainAbs, 1m), Quote(), [], [], [], null);

        Assert.False(result.Triggered);
        // 原因必须能区分「数据没有」与「数值不够」
        Assert.Contains("暂无资金流", result.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void 连续同向天数从最后一天往前数()
    {
        // 最近 3 天连续净流入
        var inflow = new List<decimal> { -100m, -50m, 30m, 40m, 50m };

        var fired = AlertEvaluator.Evaluate(
            Rule(AlertRuleTypes.FundFlowStreak, 3m), Quote(), [], [], [], inflow);

        Assert.True(fired.Triggered);
        Assert.Contains("连续 3 日", fired.Title!, StringComparison.Ordinal);

        var notEnough = AlertEvaluator.Evaluate(
            Rule(AlertRuleTypes.FundFlowStreak, 4m), Quote(), [], [], [], inflow);

        Assert.False(notEnough.Triggered);
    }

    [Fact]
    public void 连续同向遇到零值即中断()
    {
        // 最后一天为 0：既不是流入也不是流出，连续计数应当中断
        var withZero = new List<decimal> { 100m, 100m, 0m };

        var result = AlertEvaluator.Evaluate(
            Rule(AlertRuleTypes.FundFlowStreak, 2m), Quote(), [], [], [], withZero);

        Assert.False(result.Triggered);
    }

    [Fact]
    public void 成交额阈值按亿元换算()
    {
        // 快照成交额以元存储：12 亿元
        var result = AlertEvaluator.Evaluate(
            Rule(AlertRuleTypes.AmountAbove, 10m), Quote(amount: 1_200_000_000m), [], [], [], null);

        Assert.True(result.Triggered);
        Assert.Contains("12.00 亿", result.Title!, StringComparison.Ordinal);
    }

    [Fact]
    public void 资金类规则需要阈值()
    {
        Assert.True(AlertRuleCatalog.RequiresThreshold(AlertRuleTypes.FundFlowMainAbs));
        Assert.True(AlertRuleCatalog.RequiresThreshold(AlertRuleTypes.FundFlowStreak));
        Assert.True(AlertRuleCatalog.RequiresThreshold(AlertRuleTypes.AmountAbove));
    }

    [Fact]
    public void 资金类规则被识别为需要资金流数据()
    {
        Assert.True(AlertService.NeedsFundFlow(AlertRuleTypes.FundFlowMainAbs));
        Assert.True(AlertService.NeedsFundFlow(AlertRuleTypes.FundFlowStreak));
        Assert.False(AlertService.NeedsFundFlow(AlertRuleTypes.PriceAbove));
    }

    /* ------------------------------------------------------------------
       合并窗口
       ------------------------------------------------------------------ */

    [Fact]
    public void 单条命中不做合并装饰()
    {
        var now = DateTimeOffset.Now;
        var hit = new MergedHit("r1", "300750", "宁德时代", "价格上穿 300.00 元", "最新价 301.95 元", "up");

        var notification = AlertNotificationFactory.CreateMerged("u1", [hit], now);

        // 单条时标题应当就是「名称 + 触发标题」，不该出现「1 条提醒」这种噪音
        Assert.Equal("宁德时代 价格上穿 300.00 元", notification.Title);
        Assert.Equal("最新价 301.95 元", notification.Body);
        Assert.Equal("up", notification.Level);
        Assert.Equal("300750", notification.Code);
    }

    [Fact]
    public void 多条命中合并为一条并逐条列出()
    {
        var now = DateTimeOffset.Now;
        var hits = new List<MergedHit>
        {
            new("r1", "300750", "宁德时代", "价格上穿 300.00 元", "最新价 301.95 元", "up"),
            new("r2", "300750", "宁德时代", "涨幅达到 5.00%", "涨跌幅 +5.20%", "up"),
            new("r3", "300750", "宁德时代", "量比达到 2.00", "量比 2.40", "up")
        };

        var notification = AlertNotificationFactory.CreateMerged("u1", hits, now);

        Assert.Equal("3 条提醒同时触发", notification.Title);
        Assert.Equal(3, notification.Body.Split('\n').Length);
        Assert.Contains("价格上穿", notification.Body, StringComparison.Ordinal);
        Assert.Contains("量比达到", notification.Body, StringComparison.Ordinal);

        // 同标的时保留代码，便于界面「点进去看」
        Assert.Equal("300750", notification.Code);
    }

    [Fact]
    public void 合并后级别取最强的一条且利空优先()
    {
        var now = DateTimeOffset.Now;
        var hits = new List<MergedHit>
        {
            new("r1", "300750", "宁德时代", "价格上穿", "x", "up"),
            new("r2", "300750", "宁德时代", "主力净流出", "y", "down")
        };

        var notification = AlertNotificationFactory.CreateMerged("u1", hits, now);

        // 一批里只要有一条利空就该先看到它，而不是按多数票取 up
        Assert.Equal("down", notification.Level);
    }

    [Fact]
    public void 合并涉及多标的时不指向单一代码()
    {
        var now = DateTimeOffset.Now;
        var hits = new List<MergedHit>
        {
            new("r1", "300750", "宁德时代", "价格上穿", "x", "up"),
            new("r2", "600519", "贵州茅台", "价格上穿", "y", "up")
        };

        var notification = AlertNotificationFactory.CreateMerged("u1", hits, now);

        Assert.Null(notification.Code);
    }

    [Fact]
    public void 合并超出上限时只计数不列出()
    {
        var now = DateTimeOffset.Now;
        var hits = Enumerable.Range(1, 12)
            .Select(i => new MergedHit($"r{i}", "300750", "宁德时代", $"条件{i}", "x", "up"))
            .ToList();

        var notification = AlertNotificationFactory.CreateMerged("u1", hits, now, maxListed: 8);

        var lines = notification.Body.Split('\n');

        // 8 条列出 + 1 行「另有 N 条未列出」
        Assert.Equal(9, lines.Length);
        Assert.Contains("另有 4 条未列出", lines[^1], StringComparison.Ordinal);
    }

    /* ------------------------------------------------------------------
       免打扰（跨零点）
       ------------------------------------------------------------------ */

    [Fact]
    public void 免打扰窗口跨零点时按区间并集判定()
    {
        var settings = new UserNotifySettings { DndEnabled = true, DndFrom = "23:00", DndTo = "08:30" };

        Assert.True(settings.InDndWindow(At("23:30")));
        Assert.True(settings.InDndWindow(At("02:00")));
        Assert.True(settings.InDndWindow(At("08:29")));
        Assert.False(settings.InDndWindow(At("08:30")));
        Assert.False(settings.InDndWindow(At("12:00")));
        Assert.False(settings.InDndWindow(At("22:59")));
    }

    [Fact]
    public void 免打扰窗口不跨零点时按普通区间判定()
    {
        var settings = new UserNotifySettings { DndEnabled = true, DndFrom = "12:00", DndTo = "13:00" };

        Assert.True(settings.InDndWindow(At("12:30")));
        Assert.False(settings.InDndWindow(At("11:59")));
        Assert.False(settings.InDndWindow(At("13:00")));
    }

    [Fact]
    public void 未启用或时刻非法时不免打扰()
    {
        var disabled = new UserNotifySettings { DndEnabled = false, DndFrom = "23:00", DndTo = "08:30" };
        Assert.False(disabled.InDndWindow(At("23:30")));

        // 起止相同视为没设置窗口：否则会变成「全天免打扰」这种静默失效
        var same = new UserNotifySettings { DndEnabled = true, DndFrom = "09:00", DndTo = "09:00" };
        Assert.False(same.InDndWindow(At("09:00")));

        var broken = new UserNotifySettings { DndEnabled = true, DndFrom = "25:00", DndTo = "08:00" };
        Assert.False(broken.InDndWindow(At("23:00")));
    }

    /* ------------------------------------------------------------------
       构造工具
       ------------------------------------------------------------------ */

    private static DateTimeOffset At(string time)
    {
        var parsed = TimeOnly.Parse(time);
        var date = SaTime.Today.ToDateTime(parsed);
        return new DateTimeOffset(date, SaTime.Zone.GetUtcOffset(date));
    }

    private static AlertRule Rule(string type, decimal? threshold) =>
        new()
        {
            Id = "rule-1",
            UserId = "user-1",
            Code = "300750",
            RuleType = type,
            Threshold = threshold,
            Enabled = true,
            CreatedAt = DateTimeOffset.Now.AddDays(-1)
        };

    private static QuoteSnapshot Quote(decimal price = 300m, decimal pct = 1m, decimal amount = 300_000_000m) =>
        new()
        {
            Code = "300750",
            Price = price,
            Change = 3m,
            Pct = pct,
            Volume = 1_000_000m,
            Amount = amount,
            Turnover = 1m,
            VolRatio = 1m,
            Open = price,
            High = price,
            Low = price,
            PrevClose = price,
            MarketCap = 1_000_000_000m,
            FloatCap = 1_000_000_000m,
            Pe = 20m,
            PeTtm = 20m,
            Pb = 3m,
            AsOf = SaTime.Today,
            UpdatedAt = DateTimeOffset.Now
        };
}
