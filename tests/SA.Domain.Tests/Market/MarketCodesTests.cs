using SA.Domain.Common;

namespace SA.Domain.Tests.Market;

/// <summary>
/// 证券代码约定与交易时段判定。板块与市场标志决定了后续所有按板块的聚合口径，
/// 拼错会让个股落到错误的行业/板块统计里，因此逐条固化。
/// </summary>
public class MarketCodesTests
{
    [Theory]
    [InlineData("600519", 1)]
    [InlineData("601318", 1)]
    [InlineData("688256", 1)]
    [InlineData("000001", 0)]
    [InlineData("002594", 0)]
    [InlineData("300750", 0)]
    [InlineData("835185", 0)]
    [InlineData("920298", 0)]
    public void 市场标志与东财secid一致(string code, int expected) =>
        Assert.Equal(expected, MarketCodes.MarketOf(code));

    [Theory]
    [InlineData("600519", "1.600519")]
    [InlineData("300750", "0.300750")]
    [InlineData("835185", "0.835185")]
    public void 拼出secid(string code, string expected) =>
        Assert.Equal(expected, MarketCodes.SecId(code));

    [Theory]
    [InlineData("600519", "沪市主板")]
    [InlineData("601318", "沪市主板")]
    [InlineData("603259", "沪市主板")]
    [InlineData("688256", "科创板")]
    [InlineData("000001", "深市主板")]
    [InlineData("002594", "深市主板")]
    [InlineData("300750", "创业板")]
    [InlineData("301029", "创业板")]
    [InlineData("835185", "北交所")]
    [InlineData("920298", "北交所")]
    public void 推导板块(string code, string expected) =>
        Assert.Equal(expected, MarketCodes.BoardOf(code));

    [Theory]
    [InlineData("ST海航", true)]
    [InlineData("*ST星源", true)]
    [InlineData("国华退", true)]
    [InlineData("宁德时代", false)]
    [InlineData("PT金田A", false)]
    public void 识别ST与退市标记(string name, bool expected) =>
        Assert.Equal(expected, MarketCodes.IsSt(name));

    [Theory]
    [InlineData("300750", true)]
    [InlineData("600519", true)]
    [InlineData("835185", true)]
    [InlineData("000300", true)]
    [InlineData("BK1201", false)]
    [InlineData("30075", false)]
    public void 判断是否为股票代码(string code, bool expected) =>
        Assert.Equal(expected, MarketCodes.IsStockCode(code));
}

/// <summary>
/// 市场阶段文案。页面文案与数据新鲜度提示都取自这里，写错会让用户误判数据的实时性。
/// </summary>
public class MarketSessionTests
{
    [Fact]
    public void 非交易日一律显示非交易日() =>
        Assert.Equal("非交易日", MarketSession.Phase(isTradingDay: false, dataDate: SaTime.Today));

    [Theory]
    [InlineData(null)]
    [InlineData("2000-01-01")]
    public void 数据归属日不是今天时显示已收盘(string? dateText)
    {
        var date = dateText is null ? (DateOnly?)null : DateOnly.Parse(dateText);
        Assert.Equal("已收盘", MarketSession.Phase(isTradingDay: true, dataDate: date));
    }

    [Fact]
    public void 交易日且数据归属今天时按时段给出阶段()
    {
        var phase = MarketSession.Phase(isTradingDay: true, dataDate: SaTime.Today);

        // 具体取值取决于运行时刻，但必须落在四个合法阶段内，且不能是「非交易日」
        Assert.Contains(phase, new[] { "盘前", "交易中", "午间休市", "已收盘" });
    }
}
