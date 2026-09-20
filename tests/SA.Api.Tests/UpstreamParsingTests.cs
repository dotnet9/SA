using System.Text.Json;
using SA.Infrastructure.Collect.Adapters;

namespace SA.Api.Tests;

/// <summary>
/// 上游字段映射固化（详细设计 §13.2「采集适配器：使用固定响应样本解析，字段映射正确」）。
/// </summary>
/// <remarks>
/// 样本全部是 2026-09-20 真实响应的逐字拷贝。上游一旦改字段名、改顺序或改单位，
/// 这里会先失败，避免错误数据静默流入分析结果（实施计划 §13「上游字段漂移」）。
/// </remarks>
public class UpstreamParsingTests
{
    /* ------------------------------------------------------------------
       东财全市场列表
       ------------------------------------------------------------------ */

    /// <summary>正常交易标的（万科Ａ，2026-09-18 收盘）。</summary>
    private const string ListRowNormal =
        """{"f2":3.32,"f3":9.93,"f4":0.3,"f5":4408253,"f6":1419831684.34,"f8":4.54,"f9":-1.32,"f10":5.14,"f12":"000002","f13":0,"f14":"万  科Ａ","f15":3.32,"f16":3.01,"f17":3.03,"f18":3.02,"f20":39609955444,"f21":32254175671,"f23":0.39,"f100":"房地产开发","f115":-0.43}""";

    /// <summary>停牌 / 退市标的：数值字段全部为字符串 <c>"-"</c>。</summary>
    private const string ListRowSuspended =
        """{"f2":"-","f3":"-","f4":"-","f5":"-","f6":"-","f8":0,"f9":"-","f10":"-","f12":"000003","f13":0,"f14":"PT金田A","f15":"-","f16":"-","f17":"-","f18":2.71,"f20":"-","f21":"-","f23":"-","f100":"-","f115":"-"}""";

    [Fact]
    public void 全市场列表行按字段名解析到正确字段()
    {
        var row = EastMoneyMarketListSource.Parse(JsonDocument.Parse(ListRowNormal).RootElement);

        Assert.NotNull(row);
        var value = row.Value;

        Assert.Equal("000002", value.Code);
        Assert.Equal(0, value.Market);
        Assert.Equal("房地产开发", value.Industry);
        Assert.Equal(3.32m, value.Price);
        Assert.Equal(0.3m, value.Change);
        Assert.Equal(9.93m, value.Pct);
        Assert.Equal(4408253m, value.Volume);
        Assert.Equal(1419831684.34m, value.Amount);
        Assert.Equal(4.54m, value.Turnover);
        Assert.Equal(5.14m, value.VolRatio);
        Assert.Equal(-1.32m, value.Pe);
        Assert.Equal(-0.43m, value.PeTtm);
        Assert.Equal(0.39m, value.Pb);
        Assert.Equal(39609955444m, value.MarketCap);
        Assert.Equal(32254175671m, value.FloatCap);
        Assert.Equal(3.03m, value.Open);
        Assert.Equal(3.32m, value.High);
        Assert.Equal(3.01m, value.Low);
        Assert.Equal(3.02m, value.PrevClose);
    }

    [Fact]
    public void 全角空格与全角字母的名称被规整为单空格()
    {
        var row = EastMoneyMarketListSource.Parse(JsonDocument.Parse(ListRowNormal).RootElement);

        // 「万  科Ａ」的双全角空格压成单个半角空格；全角Ａ保留（搜索时由拼音层折半）
        Assert.Equal("万 科Ａ", row!.Value.Name);
    }

    [Fact]
    public void 停牌标的的非数值字段按零处理而昨收保留()
    {
        var row = EastMoneyMarketListSource.Parse(JsonDocument.Parse(ListRowSuspended).RootElement);

        Assert.NotNull(row);
        var value = row.Value;

        Assert.Equal("000003", value.Code);
        Assert.Equal("PT金田A", value.Name);
        Assert.Equal(0m, value.Price);
        Assert.Equal(0m, value.Pct);
        Assert.Equal(0m, value.Pe);
        Assert.Equal(0m, value.MarketCap);

        // 停牌时唯一还给出的字段是昨收，必须保留：界面要显示「停牌，昨收 x」
        Assert.Equal(2.71m, value.PrevClose);

        // 行业为 "-" 视为缺失，而不是把 "-" 当成行业名
        Assert.Null(value.Industry);
    }

    /* ------------------------------------------------------------------
       东财行业板块
       ------------------------------------------------------------------ */

    /// <summary>行业板块行：本例的板块市盈率缺失（上游给 <c>"-"</c>）。</summary>
    private const string SectorRow =
        """{"f2":4471.41,"f3":6.72,"f12":"BK1599","f14":"其他医疗服务","f62":196897219.0,"f104":5,"f105":0,"f115":"-","f128":"南华生物","f140":"000504"}""";

    [Fact]
    public void 行业板块行解析正确且缺失市盈率按null处理()
    {
        var row = EastMoneySectorSource.Parse(JsonDocument.Parse(SectorRow).RootElement);

        Assert.NotNull(row);
        var value = row.Value;

        Assert.Equal("BK1599", value.Code);
        Assert.Equal("其他医疗服务", value.Name);
        Assert.Equal(6.72m, value.Pct);
        Assert.Equal(196897219m, value.MainNet);
        Assert.Equal(5, value.UpCount);
        Assert.Equal(0, value.DownCount);
        Assert.Equal("南华生物", value.LeaderName);
        Assert.Equal("000504", value.LeaderCode);

        // null 而不是 0：0 会被界面当成「市盈率为 0」这种真实值展示
        Assert.Null(value.Pe);
    }

    /* ------------------------------------------------------------------
       东财大盘资金流
       ------------------------------------------------------------------ */

    /// <summary>上证指数 2026-09-18 资金流（实测原始行）。</summary>
    private const string FundFlowKline = "2026-09-18,14514085888.0,-2433945600.0,-12080132096.0,780779520.0,13733306368.0";

    [Fact]
    public void 大盘资金流字段序自洽()
    {
        var row = EastMoneyMarketFundFlowSource.ParseKline(FundFlowKline);

        Assert.Equal(new DateOnly(2026, 9, 18), row.Date);
        Assert.Equal(14514085888m, row.MainNet);
        Assert.Equal(-2433945600m, row.Small);
        Assert.Equal(-12080132096m, row.Medium);
        Assert.Equal(780779520m, row.Large);
        Assert.Equal(13733306368m, row.SuperLarge);

        // 口径断言：主力 = 大单 + 超大单（实施计划 §3.1 的实测自洽结论）
        Assert.Equal(row.MainNet, row.Large + row.SuperLarge);
    }

    [Fact]
    public void 大盘资金流字段不足时抛出采集异常()
    {
        var ex = Assert.Throws<SA.Infrastructure.Collect.Http.CollectHttpException>(
            () => EastMoneyMarketFundFlowSource.ParseKline("2026-09-18,1,2"));
        Assert.Contains("字段数不足", ex.Message, StringComparison.Ordinal);
    }

    /* ------------------------------------------------------------------
       两融余额：必须取「沪深齐备」的最近日期
       ------------------------------------------------------------------ */

    [Fact]
    public void 两融取沪深齐备的最近交易日而不是最新日期()
    {
        // 实测形态：2026-09-18 只有沪证，09-17 三个市场都有
        var rows = new[]
        {
            new EastMoneyMarginMarketSource.MarginRow(new DateOnly(2026, 9, 18), "007", 1334561902108m, 18713016200m),
            new EastMoneyMarginMarketSource.MarginRow(new DateOnly(2026, 9, 17), "007", 1334104866205m, 18578720695m),
            new EastMoneyMarginMarketSource.MarginRow(new DateOnly(2026, 9, 17), "001", 1267421565286m, 10655601697m),
            new EastMoneyMarginMarketSource.MarginRow(new DateOnly(2026, 9, 17), "002", 8337192170m, 51999m)
        };

        var result = EastMoneyMarginMarketSource.SelectLatestComplete(rows);

        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 9, 17), result.Value.Date);

        // 沪 + 深，不含京证
        Assert.Equal(1334104866205m + 1267421565286m, result.Value.FinanceBalance);
        Assert.Equal(18578720695m + 10655601697m, result.Value.LoanBalance);
    }

    [Fact]
    public void 两融只有单一市场时返回null而不是少算一半()
    {
        var rows = new[]
        {
            new EastMoneyMarginMarketSource.MarginRow(new DateOnly(2026, 9, 18), "007", 1334561902108m, 18713016200m)
        };

        Assert.Null(EastMoneyMarginMarketSource.SelectLatestComplete(rows));
    }

    /* ------------------------------------------------------------------
       涨跌停池：qdate 在上游是数字
       ------------------------------------------------------------------ */

    [Fact]
    public void 涨跌停池的数字型qdate能正确解析()
    {
        // 实测原始响应：{"rc":0,...,"data":{"tc":47,"qdate":20260918,"pool":[...]}}
        var data = JsonDocument.Parse("""{"tc":47,"qdate":20260918,"pool":[]}""").RootElement;

        var parsed = EastMoneyLimitPoolSource.ParsePool(data);

        Assert.Equal(new DateOnly(2026, 9, 18), parsed.Date);
        Assert.Equal(47, parsed.Count);
    }

    [Fact]
    public void 涨跌停池的字符串型qdate同样能解析()
    {
        // 上游表示不统一，字符串形态也要认，否则日期缺失会让家数被静默丢弃
        var data = JsonDocument.Parse("""{"tc":1,"qdate":"20260918"}""").RootElement;

        var parsed = EastMoneyLimitPoolSource.ParsePool(data);

        Assert.Equal(new DateOnly(2026, 9, 18), parsed.Date);
        Assert.Equal(1, parsed.Count);
    }

    [Fact]
    public void 涨跌停池为零家数时仍然给出日期()
    {
        // 极端平静的交易日 0 跌停是合法值，不能与「解析失败」混淆
        var data = JsonDocument.Parse("""{"tc":0,"qdate":20260918,"pool":[]}""").RootElement;

        var parsed = EastMoneyLimitPoolSource.ParsePool(data);

        Assert.Equal(new DateOnly(2026, 9, 18), parsed.Date);
        Assert.Equal(0, parsed.Count);
    }

    /* ------------------------------------------------------------------
       腾讯备源：逐位核对的字段索引
       ------------------------------------------------------------------ */

    /// <summary>
    /// 腾讯行情原始行（宁德时代，2026-09-18 收盘，88 个字段逐字拷贝）。
    /// 与东财交叉验证一致：价 301.95 / 昨收 304.30 / 开 309.77 / 高 310.00 / 低 300.27 /
    /// 量 380713 手 / 额 11,537,664,648 元 / PE(TTM) 16.44 / PB 3.75。
    /// </summary>
    private const string TencentLine =
        "v_sz300750=\"51~宁德时代~300750~301.95~304.30~309.77~380713~189212~191501~301.95~1~301.94~12~301.93~6~301.92~21~301.91~47~301.96~5~301.97~9~301.98~15~301.99~29~302.00~200~~20260918161412~-2.35~-0.77~310.00~300.27~301.95/380713/11537664648~380713~1153766~0.89~16.44~~310.00~300.27~3.20~12864.89~13971.93~3.75~365.16~243.44~0.88~-171~303.05~16.14~19.35~~~~1.07~1153766.4648~431.7885~143~ A A~GP-A-CYB~-15.88~-8.64~2.72~22.41~8.03~467.35~299.00~-13.97~-22.80~-20.46~4260601929~4627234532~-49.57~-18.35~4260601929~~~~-15.79~0.17~~~CNY~0~~301.90~73~~\"";

    [Fact]
    public void 腾讯快照按定长索引解析并换算单位()
    {
        var row = TencentQuoteSource.Parse(TencentLine);

        Assert.NotNull(row);
        var value = row.Value;

        Assert.Equal("300750", value.Code);
        Assert.Equal("宁德时代", value.Name);
        Assert.Equal(0, value.Market);

        Assert.Equal(301.95m, value.Price);
        Assert.Equal(304.30m, value.PrevClose);
        Assert.Equal(309.77m, value.Open);
        Assert.Equal(310.00m, value.High);
        Assert.Equal(300.27m, value.Low);
        Assert.Equal(-2.35m, value.Change);
        Assert.Equal(-0.77m, value.Pct);
        Assert.Equal(380713m, value.Volume);
        Assert.Equal(0.89m, value.Turnover);
        Assert.Equal(0.88m, value.VolRatio);
        Assert.Equal(16.44m, value.PeTtm);
        Assert.Equal(16.14m, value.Pe);
        Assert.Equal(3.75m, value.Pb);

        // 单位换算：腾讯给「万元」与「亿元」，与东财口径（元）对齐后才能入库。
        // 交叉验证：腾讯 1153766 万元 = 115.3766 亿，与东财同日 11,537,664,647.91 元一致（腾讯取整到万元）；
        // 腾讯总市值 13971.93 亿 = 1,397,193,000,000 元，与东财 f20 = 1,397,193,466,937 一致。
        Assert.Equal(11_537_660_000m, value.Amount);
        Assert.Equal(1_397_193_000_000m, value.MarketCap);
        Assert.Equal(1_286_489_000_000m, value.FloatCap);

        // 时间戳按业务时区解释：20260918161412 → 2026-09-18 16:14:12 +08:00
        Assert.NotNull(value.AsOf);
        Assert.Equal(new DateOnly(2026, 9, 18), DateOnly.FromDateTime(value.AsOf!.Value.DateTime));
        Assert.Equal(new TimeOnly(16, 14, 12), TimeOnly.FromDateTime(value.AsOf.Value.DateTime));
    }

    [Theory]
    [InlineData("300750", "sz300750")]
    [InlineData("600519", "sh600519")]
    [InlineData("835185", "bj835185")]
    [InlineData("920298", "bj920298")]
    public void 腾讯代码前缀按交易所拼接(string code, string expected) =>
        Assert.Equal(expected, TencentQuoteSource.TencentSymbol(code));

    [Fact]
    public void 腾讯字段数不足时放弃该行() =>
        Assert.Null(TencentQuoteSource.Parse("v_sz300750=\"51~宁德时代~300750~301.95\""));
}
