using System.Text.Json;
using SA.Infrastructure.Collect.Adapters;

namespace SA.Api.Tests;

/// <summary>
/// 股权适配器的字段映射固化。样本取自 2026-09-20 的真实响应。
/// </summary>
public class EquityParsingTests
{
    /// <summary>十大股东一行（宁德时代，2023-10-30 期）。</summary>
    private const string TopHolderRow = """
    {
      "SECUCODE": "300750.SZ",
      "SECURITY_CODE": "300750",
      "END_DATE": "2023-10-30 00:00:00",
      "HOLDER_NAME": "厦门瑞庭投资有限公司",
      "HOLD_NUM": 1025064949,
      "HOLD_NUM_RATIO": 23.31,
      "HOLD_NUM_CHANGE": "不变",
      "HOLDER_RANK": 1,
      "HOLDER_MARKET_CAP": 192732711710.98,
      "SHARES_TYPE": "流通A股",
      "TOTAL_SHARES_NUM": 4397223887
    }
    """;

    /// <summary>十大流通股东一行（宁德时代，2026-07-24 期）。</summary>
    private const string FreeFloatHolderRow = """
    {
      "SECUCODE": "300750.SZ",
      "END_DATE": "2026-07-24 00:00:00",
      "HOLDER_NAME": "厦门瑞庭投资有限公司",
      "HOLD_NUM": 1019704949,
      "FREE_HOLDNUM_RATIO": 22.769498092398,
      "HOLD_NUM_CHANGE": "不变",
      "HOLDER_RANK": 1,
      "HOLDER_MARKET_CAP": 390557192516.49,
      "HOLD_RATIO": 22.0398,
      "HOLDER_TYPE": "投资公司",
      "SHARES_TYPE": "A股",
      "NOTICE_DATE": "2026-07-30 00:00:00"
    }
    """;

    /// <summary>股东户数一行。</summary>
    private const string HolderCountRow = """
    {
      "SECURITY_CODE": "300750",
      "HOLDER_NUM": 269979,
      "PRE_HOLDER_NUM": 227422,
      "HOLDER_NUM_CHANGE": 42557,
      "HOLDER_NUM_RATIO": 18.712789439896,
      "END_DATE": "2026-06-30 00:00:00",
      "AVG_MARKET_CAP": 6417261.54248519,
      "AVG_HOLD_NUM": 16328.4942940006,
      "TOTAL_MARKET_CAP": 1732525853978.61,
      "TOTAL_A_SHARES": 4408350561,
      "HOLD_NOTICE_DATE": "2026-07-25 00:00:00",
      "CHANGE_REASON": "股权激励 其他事件",
      "REPORT": "2026 2季末"
    }
    """;

    /// <summary>股权质押一行（宁德时代，2026-09-18）。</summary>
    private const string PledgeRow = """
    {
      "SECURITY_CODE": "300750",
      "TRADE_DATE": "2026-09-18 00:00:00",
      "PLEDGE_RATIO": 0.57,
      "REPURCHASE_BALANCE": 2515,
      "PLEDGE_DEAL_NUM": 7,
      "PLEDGE_MARKET_CAP": 759404.25,
      "INDUSTRY": "电池",
      "Y1_CLOSE_ADJCHRATE": -18.37842359
    }
    """;

    [Fact]
    public void 十大股东按字段名解析并区分口径()
    {
        var row = EquityRows<SA.Domain.Entities.Equity.TopHolder>(
            EastMoneyEquitySource.ParseTopHolders("300750", [Row(TopHolderRow)], isFreeFloat: false));

        Assert.Equal(new DateOnly(2023, 10, 30), row.EndDate);
        Assert.Equal(1, row.Rank);
        Assert.False(row.IsFreeFloat);
        Assert.Equal("厦门瑞庭投资有限公司", row.HolderName);
        Assert.Equal(1_025_064_949m, row.HoldNum);
        Assert.Equal(23.31m, row.HoldRatio);
        Assert.Null(row.FreeHoldRatio);
        Assert.Equal("不变", row.HoldChange);
        Assert.Equal(192_732_711_710.98m, row.MarketCap);
    }

    [Fact]
    public void 十大流通股东取流通口径比例()
    {
        var row = EquityRows<SA.Domain.Entities.Equity.TopHolder>(
            EastMoneyEquitySource.ParseTopHolders("300750", [Row(FreeFloatHolderRow)], isFreeFloat: true));

        Assert.True(row.IsFreeFloat);

        // 流通口径有两套比例：占总股本（HOLD_RATIO）与占流通股（FREE_HOLDNUM_RATIO），都要保留
        Assert.Equal(22.0398m, row.HoldRatio);
        Assert.Equal(22.769498092398m, row.FreeHoldRatio);
        Assert.Equal("投资公司", row.HolderType);
        Assert.Equal(new DateOnly(2026, 7, 30), row.NoticeDate);
    }

    [Fact]
    public void 股东户数解析保留户均与股本字段()
    {
        var row = EquityRows<SA.Domain.Entities.Equity.HolderCount>(
            EastMoneyEquitySource.ParseHolderCounts("300750", [Row(HolderCountRow)]));

        Assert.Equal(new DateOnly(2026, 6, 30), row.EndDate);
        Assert.Equal(269979, row.HolderNum);
        Assert.Equal(227422, row.PreviousHolderNum);
        Assert.Equal(42557, row.HolderNumChange);
        Assert.Equal(18.712789439896m, row.HolderNumRatio);
        Assert.Equal(16328.4942940006m, row.AvgHoldNum);
        Assert.Equal(6417261.54248519m, row.AvgMarketCap);
        Assert.Equal(4_408_350_561m, row.TotalShares);
        Assert.Equal("股权激励 其他事件", row.ChangeReason);
        Assert.Equal("2026 2季末", row.ReportName);
    }

    [Fact]
    public void 股权质押的股数与市值按实测单位保存()
    {
        var row = EquityRows<SA.Domain.Entities.Equity.PledgeStat>(
            EastMoneyEquitySource.ParsePledge("300750", [Row(PledgeRow)]));

        Assert.Equal(new DateOnly(2026, 9, 18), row.TradeDate);
        Assert.Equal(0.57m, row.PledgeRatio);
        Assert.Equal(7, row.PledgeDealNum);
        Assert.Equal("电池", row.Industry);

        // 单位按实测核对：股数为万股（2515），市值为万元（759404.25 ≈ 75.94 亿元 =
        // 2515 万股 × 301.95 元），换算在接口层做，这里保持上游单位
        Assert.Equal(2515m, row.PledgeSharesWan);
        Assert.Equal(759404.25m, row.PledgeMarketCapWan);
    }

    [Fact]
    public void 缺报告期或无股东名的行被丢弃()
    {
        var missingDate = Row("""{"HOLDER_NAME":"甲","HOLDER_RANK":1}""");
        var missingName = Row("""{"END_DATE":"2026-06-30 00:00:00","HOLDER_RANK":2}""");

        Assert.Empty(EastMoneyEquitySource.ParseTopHolders("300750", [missingDate, missingName], isFreeFloat: false));
    }

    [Fact]
    public void 带市场后缀的SECUCODE按交易所拼接()
    {
        Assert.Equal("300750.SZ", SA.Domain.Common.MarketCodes.SecUCode("300750"));
        Assert.Equal("600519.SH", SA.Domain.Common.MarketCodes.SecUCode("600519"));
        Assert.Equal("835185.BJ", SA.Domain.Common.MarketCodes.SecUCode("835185"));
        Assert.Equal("920298.BJ", SA.Domain.Common.MarketCodes.SecUCode("920298"));
    }

    [Fact]
    public void 无数据时返回空序列()
    {
        var empty = JsonDocument.Parse("""{"result":null,"success":true}""").RootElement;

        // 适配器的 QueryAsync 会把「无 result」转成空序列；解析器本身对空输入也必须安全
        Assert.Empty(EastMoneyEquitySource.ParseHolderCounts("300750", []));
    }

    private static JsonElement Row(string json) => JsonDocument.Parse(json).RootElement;

    private static T EquityRows<T>(IEnumerable<T> rows) => rows.Single();
}
