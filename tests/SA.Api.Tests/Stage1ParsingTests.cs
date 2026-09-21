using System.Text.Json;
using SA.Infrastructure.Collect.Adapters;

namespace SA.Api.Tests;

/// <summary>
/// 阶段 1 新增数据源的字段映射固化：腾讯 K 线、新浪全市场列表。
/// </summary>
/// <remarks>
/// 样本是 2026-09-21 真实响应的逐字拷贝。两条最容易出错的断言：
/// <list type="bullet">
/// <item>腾讯 K 线的 <b>Amount / Turnover 必须是 null</b>——该源每行只有 6 段，
/// 写成 0 就是数据失真（实施计划 §2.2 明确点名参考实现的这个 bug）。</item>
/// <item>新浪的 <b>volume 要 ÷100（股→手）、mktcap/nmc 要 ×10000（万元→元）</b>，
/// 单位错了会让成交额与市值整体偏差两个数量级。</item>
/// </list>
/// </remarks>
public class Stage1ParsingTests
{
    /* ------------------------------------------------------------------
       腾讯 K 线
       ------------------------------------------------------------------ */

    /// <summary>真实响应片段（<c>sz300750,day,,,10,qfq</c>）。每行 6 段，无成交额与换手率。</summary>
    private const string TencentKlineJson =
        """
        {"code":0,"msg":"","data":{"sz300750":{"qfqday":[["2026-09-07","351.050","348.200","351.800","345.500","246788.000"],["2026-09-08","345.670","335.490","346.600","326.000","519551.000"]],"qt":{}}}}
        """;

    /// <summary>不复权时响应键不带前缀（实测：<c>bfq</c> → <c>day</c>）。</summary>
    private const string TencentKlineBfqJson =
        """
        {"code":0,"msg":"","data":{"sz300750":{"day":[["2026-09-08","345.670","335.490","346.600","326.000","519551.000"]]}}}
        """;

    [Fact]
    public void 腾讯K线按序解析且成交额与换手率必须为null()
    {
        var node = JsonDocument.Parse(TencentKlineJson).RootElement
            .GetProperty("data").GetProperty("sz300750").GetProperty("qfqday");

        var bar = TencentKlineSource.ParseRow(node[0].Clone());

        Assert.NotNull(bar);
        var value = bar.Value;
        Assert.Equal(new DateOnly(2026, 9, 7), value.Date);
        // 实测序：1=开、2=收、3=高、4=低（不是 OHLC）
        Assert.Equal(351.050m, value.Open);
        Assert.Equal(348.200m, value.Close);
        Assert.Equal(351.800m, value.High);
        Assert.Equal(345.500m, value.Low);
        Assert.Equal(246788.000m, value.Volume);

        // 这两条是本轮的关键契约：该源不提供，就必须是 null，不能是 0
        Assert.Null(value.Amount);
        Assert.Null(value.Turnover);
    }

    [Fact]
    public void 腾讯K线字段不足六段时跳过该行而不是补零()
    {
        var row = JsonDocument.Parse("""["2026-09-07","351.050","348.200","351.800"]""").RootElement;

        Assert.Null(TencentKlineSource.ParseRow(row));
    }

    [Fact]
    public void 腾讯K线复权口径决定响应键名()
    {
        // 键名与复权口径必须成对：用 qfq 请求却读 day 键会静默拿到空数组
        Assert.Equal(("day", "qfq", "qfqday"),
            TencentKlineSource.Parameters(TencentKlineSource.PeriodDaily, TencentKlineSource.AdjustFront, "300750"));
        Assert.Equal(("day", "bfq", "day"),
            TencentKlineSource.Parameters(TencentKlineSource.PeriodDaily, TencentKlineSource.AdjustNone, "300750"));
        Assert.Equal(("day", "hfq", "hfqday"),
            TencentKlineSource.Parameters(TencentKlineSource.PeriodDaily, TencentKlineSource.AdjustBack, "300750"));
        Assert.Equal(("week", "qfq", "qfqweek"),
            TencentKlineSource.Parameters(TencentKlineSource.PeriodWeekly, TencentKlineSource.AdjustFront, "300750"));
        Assert.Equal(("month", "qfq", "qfqmonth"),
            TencentKlineSource.Parameters(TencentKlineSource.PeriodMonthly, TencentKlineSource.AdjustFront, "300750"));
    }

    [Fact]
    public void 不复权响应的键名无前缀()
    {
        var node = JsonDocument.Parse(TencentKlineBfqJson).RootElement
            .GetProperty("data").GetProperty("sz300750");

        // 用 bfq 的键去读 qfq 的键会拿不到
        Assert.True(node.TryGetProperty("day", out var rows));
        Assert.False(node.TryGetProperty("qfqday", out _));

        var bar = TencentKlineSource.ParseRow(rows[0].Clone());
        Assert.Equal(335.490m, bar!.Value.Close);
    }

    [Theory]
    // 沪市
    [InlineData("600519", "sh600519")]
    [InlineData("688110", "sh688110")]
    // 深市
    [InlineData("300750", "sz300750")]
    [InlineData("000002", "sz000002")]
    // 北交所（前缀是 bj，不是 sz）
    [InlineData("920000", "bj920000")]
    // 指数按登记表判定，不按代码首位：
    // 000300 沪深300 在沪市（按首位推会得到 sz000300，错）；
    // 000001 既是上证指数也是平安银行，登记表优先，因此是 sh000001
    [InlineData("000300", "sh000300")]
    [InlineData("000001", "sh000001")]
    [InlineData("399001", "sz399001")]
    public void 腾讯代码前缀按市场而非按首位推断(string code, string expected) =>
        Assert.Equal(expected, TencentKlineSource.TencentSymbol(code));

    [Fact]
    public void 腾讯源不支持板块码()
    {
        // 返回 null 而不是硬拼一个前缀：调用方据此降级到东财
        Assert.Null(TencentKlineSource.TencentSymbol("BK1033"));
    }

    /* ------------------------------------------------------------------
       新浪全市场列表
       ------------------------------------------------------------------ */

    /// <summary>真实响应片段（<c>node=hs_a</c>，按 symbol 升序首页）。</summary>
    private const string SinaListJson =
        """
        [{"symbol":"bj920000","code":"920000","name":"\u5b89\u5fbd\u51e4\u51f0","trade":"13.880","pricechange":0.2,"changepercent":1.462,"buy":"13.870","sell":"13.880","settlement":"13.680","open":"13.680","high":"14.000","low":"13.550","volume":380109,"amount":5252723,"ticktime":"15:30:00","per":19.278,"pb":1.863,"mktcap":127251.84,"nmc":79940.3679,"turnoverratio":0.65998}]
        """;

    [Fact]
    public void 新浪列表单位换算为东财口径()
    {
        var rows = SinaMarketListSource.ParsePage(SinaListJson);

        var row = Assert.Single(rows);
        Assert.Equal("920000", row.Code);
        Assert.Equal("安徽凤凰", row.Name);
        Assert.Equal(13.880m, row.Price);
        Assert.Equal(0.2m, row.Change);
        Assert.Equal(1.462m, row.Pct);
        Assert.Equal(1.863m, row.Pb);
        Assert.Equal(19.278m, row.Pe);
        Assert.Equal(0.65998m, row.Turnover);

        // 股 → 手：380109 股 = 3801.09 手
        Assert.Equal(3801.09m, row.Volume);

        // 万元 → 元：127251.84 万元 = 12.725184 亿元
        Assert.Equal(1_272_518_400m, row.MarketCap);
        Assert.Equal(799_403_679m, row.FloatCap);
    }

    [Fact]
    public void 新浪列表不提供行业与量比时留空而不是猜()
    {
        var row = Assert.Single(SinaMarketListSource.ParsePage(SinaListJson));

        // 该源完全没有行业字段：留 null，界面按「无行业」显示
        Assert.Null(row.Industry);
        // 量比与 PE(TTM) 该源不提供
        Assert.Equal(0m, row.VolRatio);
        Assert.Equal(0m, row.PeTtm);
    }

    [Fact]
    public void 新浪列表北交所代码的市场标志为0()
    {
        var row = Assert.Single(SinaMarketListSource.ParsePage(SinaListJson));

        // 与东财口径一致：北交所走 0.
        Assert.Equal(0, row.Market);
    }

    [Fact]
    public void 新浪列表停牌标的数值字段为null时按零处理且保留昨收()
    {
        const string json =
            """
            [{"symbol":"sh600001","code":"600001","name":"\u9000\u5e02\u80a1","trade":null,"pricechange":null,"changepercent":null,"settlement":"2.71","open":null,"high":null,"low":null,"volume":null,"amount":null,"per":null,"pb":null,"mktcap":null,"nmc":null,"turnoverratio":null}]
            """;

        var row = Assert.Single(SinaMarketListSource.ParsePage(json));

        Assert.Equal(0m, row.Price);
        Assert.Equal(2.71m, row.PrevClose);
        Assert.Equal(0m, row.MarketCap);
    }

    [Fact]
    public void 新浪列表非数组响应返回空而不抛异常()
    {
        // 上游异常时可能返回 HTML 错误页或 {"error":...}
        Assert.Empty(SinaMarketListSource.ParsePage("<html>403</html>"));
        Assert.Empty(SinaMarketListSource.ParsePage("""{"error":"forbidden"}"""));
        Assert.Empty(SinaMarketListSource.ParsePage(string.Empty));
    }
}
