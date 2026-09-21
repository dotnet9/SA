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

    /// <summary>
    /// 指数与板块的 secid 不能按代码首位推断。
    /// </summary>
    /// <remarks>
    /// 这是一条<b>回归测试</b>：沪深300 的代码是 000300，按首位推会得到 <c>0.000300</c>（深市），
    /// 而它实际在沪市（<c>1.000300</c>）。用错前缀不会报错，只会返回空 data，
    /// 实测表现为「基准指数日线一直采不到、文件根本不生成」。
    /// </remarks>
    [Theory]
    [InlineData("000300", "1.000300")]
    [InlineData("000001", "1.000001")]
    [InlineData("000905", "1.000905")]
    [InlineData("399001", "0.399001")]
    [InlineData("399006", "0.399006")]
    [InlineData("899050", "0.899050")]
    [InlineData("BK1033", "90.BK1033")]
    [InlineData("600519", "1.600519")]
    [InlineData("300750", "0.300750")]
    public void 解析secid区分指数板块与个股(string code, string expected) =>
        Assert.Equal(expected, MarketCodes.ResolveSecId(code));

    [Fact]
    public void 已登记的指数不被当作个股()
    {
        Assert.True(MarketCodes.IsIndexCode("000300"));

        // 同时注意：000001 既是上证指数也是平安银行，按代码无法区分，
        // 因此指数用哪一侧由调用方语境决定——采集指数时走 ResolveSecId（取沪市 1.000001）
        Assert.True(MarketCodes.IsIndexCode("000001"));
        Assert.False(MarketCodes.IsIndexCode("300750"));
    }

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
