using System.Text.Json;
using SA.Application.Risk;
using SA.Domain.History;
using SA.Infrastructure.Collect.Adapters;

namespace SA.Api.Tests;

/// <summary>
/// 机构评级的字段映射与风险规则。
/// </summary>
/// <remarks>
/// 评级样本取自 2026-09-20 的真实响应（300750）；风险规则用手工构造的序列验证阈值边界，
/// 因为阈值的意义就在于「刚好越过才算触发」。
/// </remarks>
public class RatingAndRiskTests
{
    /// <summary>机构评级原始响应（逐字拷贝，删去与本测试无关的字段）。</summary>
    private const string RatingResponse = """
    {
      "result": {
        "data": [
          {
            "SECURITY_CODE": "300750",
            "SECURITY_NAME_ABBR": "宁德时代",
            "RATING_ORG_NUM": 35,
            "RATING_BUY_NUM": 29,
            "RATING_ADD_NUM": 6,
            "RATING_NEUTRAL_NUM": null,
            "RATING_REDUCE_NUM": null,
            "RATING_SALE_NUM": null,
            "YEAR1": 2025,
            "YEAR_MARK1": "A",
            "EPS1": 15.603549269156,
            "YEAR2": 2026,
            "YEAR_MARK2": "E",
            "EPS2": 20.826542857143,
            "YEAR3": 2027,
            "YEAR_MARK3": "E",
            "EPS3": 26.007457142857,
            "YEAR4": 2028,
            "YEAR_MARK4": "E",
            "EPS4": 31.238117647059,
            "INDUSTRY_BOARD": "电池",
            "DEC_AIMPRICEMAX": 656,
            "DEC_AIMPRICEMIN": 500,
            "RATING_LONG_NUM": 35
          }
        ]
      }
    }
    """;

    [Fact]
    public void 机构评级按字段名解析并区分实际与预测()
    {
        var consensus = EastMoneyRatingSource
            .ParseConsensus("300750", JsonDocument.Parse(RatingResponse).RootElement
                .GetProperty("result").GetProperty("data").EnumerateArray().Select(row => row.Clone()))
            .Single();

        Assert.Equal(35, consensus.RatingOrgNum);
        Assert.Equal(29, consensus.BuyNum);
        Assert.Equal(6, consensus.AddNum);
        Assert.Null(consensus.NeutralNum);
        Assert.Equal(500m, consensus.AimPriceMin);
        Assert.Equal(656m, consensus.AimPriceMax);

        // 2025 是已实现（A），2026-2028 是预测（E）
        Assert.Equal("A", consensus.YearMark1);
        Assert.Equal("E", consensus.YearMark2);
        Assert.Equal(15.603549269156m, consensus.Eps1);
        Assert.Equal(31.238117647059m, consensus.Eps4);
    }

    [Fact]
    public void 机构未覆盖时返回空而不是异常()
    {
        var data = JsonDocument.Parse("""{"result":{"data":[]}}""").RootElement
            .GetProperty("result").GetProperty("data").EnumerateArray().Select(row => row.Clone());

        Assert.Empty(EastMoneyRatingSource.ParseConsensus("999999", data));
    }

    [Fact]
    public void 年化波动率按收益率标准差计算()
    {
        // 制造已知波动：日收益率交替 ±1%
        var bars = BuildBars(start: 100m, days: 61, dailyReturnPercent: 1m);

        var volatility = SA.Domain.Analysis.Indicators.AnnualizedVolatility(bars.Select(b => b.Close).ToList(), 60);

        Assert.NotNull(volatility);

        // 交替 ±1% 的样本标准差约为 1%，年化 ≈ 1% × √250 ≈ 15.8%
        Assert.InRange(volatility!.Value, 14m, 18m);
    }

    [Fact]
    public void 样本不足时不计算波动率()
    {
        var bars = BuildBars(start: 100m, days: 40, dailyReturnPercent: 1m);

        // 需要 window + 1 个样本
        Assert.Null(SA.Domain.Analysis.Indicators.AnnualizedVolatility(bars.Select(b => b.Close).ToList(), 60));
    }

    [Fact]
    public void 最大回撤取峰值到谷底的最大跌幅()
    {
        // 100 → 150（峰）→ 60（谷）→ 90：最大回撤 = (150-60)/150 = 60%
        var closes = new[] { 100m, 120m, 150m, 100m, 60m, 90m };
        var bars = closes
            .Select((close, index) => Bar(new DateOnly(2026, 1, 1).AddDays(index), close))
            .ToList();

        var drawdown = SA.Domain.Analysis.Indicators.MaxDrawdown(bars.Select(b => b.Close).ToList());

        Assert.Equal(60m, drawdown);
    }

    [Fact]
    public void 单边上涨时最大回撤为零()
    {
        var bars = Enumerable.Range(1, 10)
            .Select(index => Bar(new DateOnly(2026, 1, 1).AddDays(index), 100m + index))
            .ToList();

        Assert.Equal(0m, SA.Domain.Analysis.Indicators.MaxDrawdown(bars.Select(b => b.Close).ToList()));
    }

    [Fact]
    public void 样本不足时不计算回撤()
    {
        Assert.Null(SA.Domain.Analysis.Indicators.MaxDrawdown([100m]));
    }

    /// <summary>
    /// 构造日线序列：每天按给定幅度交替涨跌，使方差可预期。
    /// </summary>
    private static List<DailyBar> BuildBars(decimal start, int days, decimal dailyReturnPercent)
    {
        var bars = new List<DailyBar>(days);
        var price = start;

        for (var i = 0; i < days; i++)
        {
            // 交替 ±dailyReturnPercent
            var ratio = i % 2 == 0 ? 1 + dailyReturnPercent / 100m : 1 - dailyReturnPercent / 100m;
            price *= ratio;
            bars.Add(Bar(new DateOnly(2026, 1, 1).AddDays(i), price));
        }

        return bars;
    }

    private static DailyBar Bar(DateOnly date, decimal close) =>
        new(
            Date: date,
            Open: close,
            High: close,
            Low: close,
            Close: close,
            Volume: 1_000_000m,
            Amount: close * 1_000_000m,
            Turnover: 1m,
            VolRatio: 1m,
            AdjFactor: 1m);
}
