using System.Text.Json;
using SA.Infrastructure.Collect.Adapters;

namespace SA.Api.Tests;

/// <summary>
/// 资金面适配器的字段映射固化。样本取自 2026-09-20 的真实响应。
/// </summary>
public class CapitalParsingTests
{
    /// <summary>个股资金流一行（宁德时代，2026-09-18，13 段）。</summary>
    private const string FundFlowLine =
        "2026-09-18,-702949664.0,712592896.0,-9643232.0,-407389824.0,-295559840.0,-6.09,6.18,-0.08,-3.53,-2.56,301.95,-0.77";

    /// <summary>龙虎榜一行（宁德时代，2020-07-07 上榜）。</summary>
    private const string BillboardRow = """
    {
      "TRADE_DATE": "2020-07-07 00:00:00",
      "SECURITY_CODE": "300750",
      "SECURITY_NAME_ABBR": "宁德时代",
      "CLOSE_PRICE": 195.58,
      "CHANGE_RATE": 10,
      "TURNOVERRATE": 2.0692,
      "BILLBOARD_NET_AMT": -148902744.65,
      "BILLBOARD_BUY_AMT": 991212330.68,
      "BILLBOARD_SELL_AMT": 1140115075.33,
      "BILLBOARD_DEAL_AMT": 2131327406.01,
      "EXPLANATION": "日涨幅偏离值达到 7% 的前五只证券",
      "EXPLAIN": "4家机构买入，成功率 43.97%",
      "D1_CLOSE_ADJCHRATE": 1.01748645,
      "D5_CLOSE_ADJCHRATE": 10.41517538,
      "D10_CLOSE_ADJCHRATE": 5.83904285
    }
    """;

    /// <summary>大宗交易一行。</summary>
    private const string BlockTradeRow = """
    {
      "TRADE_DATE": "2026-09-15 00:00:00",
      "SECURITY_CODE": "300750",
      "DEAL_PRICE": 316.36,
      "PREMIUM_RATIO": 0,
      "DEAL_VOLUME": 10000,
      "DEAL_AMT": 3163600,
      "BUYER_NAME": "机构专用",
      "SELLER_NAME": "机构专用",
      "CLOSE_PRICE": 316.36
    }
    """;

    /// <summary>两融明细一行。</summary>
    private const string MarginRow = """
    {
      "DATE": "2026-09-17 00:00:00",
      "SCODE": "300750",
      "RZYE": 24525224718,
      "RQYL": 828558,
      "RZRQYE": 24777354917,
      "RQYE": 252130199,
      "RZMRE": 785099837,
      "RZJME": 11466548,
      "RZYEZB": 1.89164695,
      "SPJ": 304.3,
      "ZDF": -0.3863
    }
    """;

    /// <summary>陆股通持股一行（季频）。</summary>
    private const string NorthboundRow = """
    {
      "HOLD_DATE": "2026-06-30 00:00:00",
      "SECURITY_CODE": "300750",
      "DATE_TYPE": "2026二季末",
      "HOLD_SHARES": 894158187,
      "HOLD_SHARES_LAST": 761315474,
      "ADD_SHARES_REPAIR": 132842713,
      "ADD_SHARES_AMP": 14.908318511437,
      "HOLD_MARKET_CAP": 351413109072.87,
      "ORG_QUANTITY": 36,
      "ORG_QUANTITY_LAST": 40,
      "FREE_SHARES_RATIO": 20.9892,
      "TOTAL_SHARES_RATIO": 19.3263,
      "INDUSTRY_NAME": "电源设备"
    }
    """;

    [Fact]
    public void 个股资金流字段序与算术自洽()
    {
        var row = EastMoneyCapitalSource.ParseFundFlow("300750", [FundFlowLine]).Single();

        Assert.Equal(new DateOnly(2026, 9, 18), row.Date);
        Assert.Equal(-702_949_664m, row.MainNet);
        Assert.Equal(712_592_896m, row.SmallNet);
        Assert.Equal(-9_643_232m, row.MediumNet);
        Assert.Equal(-407_389_824m, row.LargeNet);
        Assert.Equal(-295_559_840m, row.SuperLargeNet);
        Assert.Equal(-6.09m, row.MainRatio);
        Assert.Equal(301.95m, row.Close);
        Assert.Equal(-0.77m, row.ChangePercent);

        // 口径断言：主力 = 大单 + 超大单；五档合计为 0（实测响应即满足这两条）
        Assert.Equal(row.MainNet, row.LargeNet + row.SuperLargeNet);
        Assert.Equal(0m, row.MainNet + row.MediumNet + row.SmallNet);
    }

    [Fact]
    public void 资金流字段数不足时跳过该行()
    {
        Assert.Empty(EastMoneyCapitalSource.ParseFundFlow("300750", ["2026-09-18,-1,2"]));
    }

    [Fact]
    public void 龙虎榜解析保留上游原文与后续涨跌幅()
    {
        var row = EastMoneyCapitalSource.ParseBillboards("300750", [Row(BillboardRow)]).Single();

        Assert.Equal(new DateOnly(2020, 7, 7), row.TradeDate);
        Assert.Equal(-148_902_744.65m, row.NetAmount);
        Assert.Equal(991_212_330.68m, row.BuyAmount);
        Assert.Equal(1_140_115_075.33m, row.SellAmount);
        Assert.Equal(10m, row.ChangePercent);
        Assert.Equal(2.0692m, row.TurnoverRate);

        // 上榜原因与解读按上游原文透传（不自行归类）
        Assert.Contains("偏离值", row.Reason!, StringComparison.Ordinal);
        Assert.Contains("机构买入", row.Explain!, StringComparison.Ordinal);

        // 后续涨跌幅由上游回填
        Assert.Equal(1.01748645m, row.Next1Change);
        Assert.Equal(10.41517538m, row.Next5Change);
    }

    [Fact]
    public void 大宗交易解析成交量与折溢价()
    {
        var row = EastMoneyCapitalSource.ParseBlockTrades("300750", [Row(BlockTradeRow)]).Single();

        Assert.Equal(new DateOnly(2026, 9, 15), row.TradeDate);
        Assert.Equal(316.36m, row.DealPrice);
        Assert.Equal(0m, row.PremiumRatio);
        Assert.Equal(10_000m, row.DealVolume);
        Assert.Equal(3_163_600m, row.DealAmount);
        Assert.Equal("机构专用", row.BuyerName);
        Assert.Equal(316.36m, row.Close);
    }

    [Fact]
    public void 两融明细解析余额与净买入()
    {
        var row = EastMoneyCapitalSource.ParseMarginDetails("300750", [Row(MarginRow)]).Single();

        Assert.Equal(new DateOnly(2026, 9, 17), row.Date);
        Assert.Equal(24_525_224_718m, row.FinanceBalance);
        Assert.Equal(785_099_837m, row.FinanceBuy);
        Assert.Equal(11_466_548m, row.FinanceNetBuy);
        Assert.Equal(252_130_199m, row.LoanBalance);
        Assert.Equal(828_558m, row.LoanVolume);
        Assert.Equal(24_777_354_917m, row.TotalBalance);
        Assert.Equal(1.89164695m, row.FinanceBalanceRatio);
        Assert.Equal(304.3m, row.Close);
    }

    [Fact]
    public void 陆股通持股解析季度披露字段()
    {
        var row = EastMoneyCapitalSource.ParseNorthbound("300750", [Row(NorthboundRow)]).Single();

        Assert.Equal(new DateOnly(2026, 6, 30), row.HoldDate);
        Assert.Equal("2026二季末", row.DateType);
        Assert.Equal(894_158_187m, row.HoldShares);
        Assert.Equal(761_315_474m, row.PreviousHoldShares);
        Assert.Equal(132_842_713m, row.AddShares);
        Assert.Equal(14.908318511437m, row.AddSharesAmp);
        Assert.Equal(351_413_109_072.87m, row.HoldMarketCap);
        Assert.Equal(36, row.OrgQuantity);
        Assert.Equal(40, row.PreviousOrgQuantity);
        Assert.Equal(20.9892m, row.FreeSharesRatio);
        Assert.Equal("电源设备", row.Industry);
    }

    [Fact]
    public void 缺日期的行一律丢弃()
    {
        Assert.Empty(EastMoneyCapitalSource.ParseBillboards("300750", [Row("""{"EXPLANATION":"x"}""")]));
        Assert.Empty(EastMoneyCapitalSource.ParseBlockTrades("300750", [Row("""{"DEAL_PRICE":1}""")]));
        Assert.Empty(EastMoneyCapitalSource.ParseMarginDetails("300750", [Row("""{"RZYE":1}""")]));
        Assert.Empty(EastMoneyCapitalSource.ParseNorthbound("300750", [Row("""{"HOLD_SHARES":1}""")]));
    }

    private static JsonElement Row(string json) => JsonDocument.Parse(json).RootElement;
}
