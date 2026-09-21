using System.Text.Json;
using SA.Domain.Entities.Finance;
using SA.Infrastructure.Collect.Adapters;

namespace SA.Api.Tests;

/// <summary>
/// 基本面采集的解析与规则固化（实施计划 §2.3、§5.6）。
/// </summary>
/// <remarks>
/// 样本是 2026-09-21 真实响应的逐字拷贝。重点固化三件容易出错的事：
/// <list type="number">
/// <item><b>非 A 股标的必须过滤</b>——上游一个报告期 13,420 行里只有 5,832 行是上市 A 股，
/// 其余是新三板与 IPO 申报主体；</item>
/// <item><b>金融业的空值不是缺失</b>——银行股毛利率/流动比率/ROIC 天然为 null，
/// 必须能识别出来并显示「不适用」；</item>
/// <item><b>字段名以真实响应为准</b>——扣非每股收益是 <c>EPSKCJB</c>，
/// 不是实施计划 §2.3 写的 <c>DEDUCT_BASIC_EPS</c>。</item>
/// </list>
/// </remarks>
public class FundamentalParsingTests
{
    /// <summary>东芯股份 688110 2026 中报（真实响应，字段已裁剪为被测部分）。</summary>
    private const string DongxinInterim =
        """
        {"SECUCODE":"688110.SH","SECURITY_CODE":"688110","SECURITY_NAME_ABBR":"东芯股份","ORG_TYPE":"通用","REPORT_DATE":"2026-06-30 00:00:00","REPORT_TYPE":"中报","NOTICE_DATE":"2026-08-28 00:00:00","ROEJQ":17.36,"ROEKCJQ":16.76,"XSMLL":67.6610727409,"XSJLL":45.8000503966,"ZCFZL":9.7557315025,"LD":8.577490627315,"SD":5.692586253621,"ROIC":17.035024902417,"JYXJLYYSR":0.382434477557,"NETCASH_OPERATE_PK":1057587046.37,"NCO_NETPROFIT":0.835008857514,"NCO_OP":0.762115282064,"FCFF_FORWARD":453291462.597382,"INTEREST_DEBT_RATIO":0.9040644329,"INTSTCOVRATE":793.28678027245,"LIQUIDATION_RATIO":1020.79142432522,"CHZZTS":181.2,"YSZKZZTS":62.5,"ZZCZZTS":900.1,"TOTALOPERATEREVETZ":336.4,"PARENTNETPROFITTZ":210.5,"KCFJCXSYJLRTZ":198.3,"EPSJB":1.5,"EPSKCJB":1.45,"BPS":9.395847712389,"MGJYXJJE":2.39,"RDEXPEND":137925511.49,"RE_RATIO_PK":9.213707054805,"RDPERSONNEL":216,"PRATIO":63.53,"TOTALOPERATEREVE":1497000000,"PARENTNETPROFIT":660000000,"TOTAL_ASSETS_PK":4797806318.98,"TOTAL_EQUITY_PK":4329745216.49,"LIABILITY":468061102.49,"TOTAL_SHARE":442377391,"A_FREE_SHARE":442377391,"STAFF_NUM":null}
        """;

    /// <summary>平安银行 000001 2026 中报：金融业的四个字段为 null，负债率偏高。</summary>
    private const string PingAnBank =
        """
        {"SECUCODE":"000001.SZ","SECURITY_CODE":"000001","ORG_TYPE":"银行","REPORT_DATE":"2026-06-30 00:00:00","REPORT_TYPE":"中报","XSMLL":null,"LD":null,"SD":null,"ROIC":null,"FCFF_FORWARD":null,"ZCFZL":90.9067249869,"ROEJQ":8.5}
        """;

    /// <summary>新三板标的（<c>.NQ</c> 后缀，含老三板退市股）。</summary>
    private const string NewThirdBoard =
        """
        {"SECUCODE":"400016.NQ","SECURITY_CODE":"400016","SECURITY_NAME_ABBR":"金田A3","ORG_TYPE":"通用","REPORT_DATE":"2026-06-30 00:00:00","REPORT_TYPE":"中报","ROEJQ":1.2}
        """;

    /// <summary>IPO 申报主体：代码形如 <c>A26229</c>，尚未上市但已披露财务数据。</summary>
    private const string IpoApplicant =
        """
        {"SECUCODE":"A26229.SZ","SECURITY_CODE":"A26229","SECURITY_NAME_ABBR":"云豹智能","ORG_TYPE":"通用","REPORT_DATE":"2026-06-30 00:00:00","REPORT_TYPE":"中报","ROEJQ":5.0}
        """;

    private static FundamentalMetric? Parse(string json) =>
        EastMoneyFundamentalSource.Parse(JsonDocument.Parse(json).RootElement, null);

    /* ------------------------------------------------------------------
       上市 A 股过滤
       ------------------------------------------------------------------ */

    [Fact]
    public void 上市A股可解析且字段逐项映射正确()
    {
        var metric = Parse(DongxinInterim);

        Assert.NotNull(metric);
        var value = metric!;

        Assert.Equal("688110", value.Code);
        Assert.Equal(new DateOnly(2026, 6, 30), value.ReportDate);
        Assert.Equal("中报", value.ReportType);
        Assert.Equal("通用", value.OrgType);
        Assert.Equal(new DateOnly(2026, 8, 28), value.NoticeDate);

        // 与 §9 验收表逐值核对
        Assert.Equal(17.36m, value.RoeWeighted);
        Assert.Equal(16.76m, value.RoeDeducted);
        Assert.Equal(67.6610727409m, value.GrossMargin);
        Assert.Equal(9.7557315025m, value.DebtRatio);
        Assert.Equal(0.835008857514m, value.OperatingCashFlowToNetProfit);
        Assert.Equal(0.9040644329m, value.InterestDebtRatio);
        Assert.Equal(793.28678027245m, value.InterestCoverageRatio);
        Assert.Equal(137925511.49m, value.RndExpense);
        Assert.Equal(9.213707054805m, value.RndExpenseRatio);
        Assert.Equal(216m, value.RndPersonnel);

        // 字段名是 EPSKCJB 而不是 DEDUCT_BASIC_EPS（后者属于另一个报表）
        Assert.Equal(1.5m, value.Eps);
        Assert.Equal(1.45m, value.EpsDeducted);
    }

    [Fact]
    public void 员工总数为空时保持null不回退到估算值()
    {
        var metric = Parse(DongxinInterim);

        // §1.1 明确要求：实测 STAFF_NUM 为 null 时界面显示「暂无数据」，
        // 不得回退到估算值或行业均值
        Assert.Null(metric!.StaffNumber);
    }

    [Fact]
    public void PRATIO不落库因为口径未确认()
    {
        // 上游确实返回了 PRATIO=63.53，但无法证实它是不是「研发人员占比」。
        // 实施计划 §2.3 第 4 条要求核对清楚前不展示——因此实体里根本没有这个属性。
        Assert.Null(typeof(FundamentalMetric).GetProperty("PRATIO"));
        Assert.Null(typeof(FundamentalMetric).GetProperty("Pratio"));
        Assert.Null(typeof(FundamentalMetric).GetProperty("RndPersonnelRatio"));
    }

    [Fact]
    public void 新三板标的不进入基本面表()
    {
        // .NQ 后缀（含 400xxx 老三板退市股）：实测一个报告期有 6,852 行
        Assert.Null(Parse(NewThirdBoard));
    }

    [Fact]
    public void IPO申报主体不进入基本面表()
    {
        // 代码非 6 位数字（A26229）：尚未上市，实测有 736 行
        Assert.Null(Parse(IpoApplicant));
    }

    [Theory]
    // 沪 / 深 / 北三个交易所的正常标的
    [InlineData("688110", "688110.SH", true)]
    [InlineData("300750", "300750.SZ", true)]
    [InlineData("920000", "920000.BJ", true)]
    [InlineData("600519", "600519.SH", true)]
    // 新三板与老三板退市股
    [InlineData("400016", "400016.NQ", false)]
    [InlineData("874861", "874861.NQ", false)]
    // IPO 申报主体
    [InlineData("A26229", "A26229.SZ", false)]
    [InlineData("A22444", "A22444.SH", false)]
    // 缺字段
    [InlineData(null, "688110.SH", false)]
    [InlineData("688110", null, false)]
    [InlineData("688110", "688110", false)]
    public void 上市A股判据同时要求后缀与六位数字(string? code, string? secUCode, bool expected) =>
        Assert.Equal(expected, EastMoneyFundamentalSource.IsListedAShare(code, secUCode));

    [Fact]
    public void 四百开头的退市股不会被误判为深市主板()
    {
        // MarketCodes.IsStockCode 的兜底分支会把 400016 判成「深市主板」，
        // 因此这里不能用它——这是实测踩到的坑
        Assert.True(SA.Domain.Common.MarketCodes.IsStockCode("400016"));
        Assert.False(EastMoneyFundamentalSource.IsListedAShare("400016", "400016.NQ"));
    }

    /* ------------------------------------------------------------------
       金融业
       ------------------------------------------------------------------ */

    [Fact]
    public void 金融业的空字段照原样落null而不是填零()
    {
        var metric = Parse(PingAnBank);

        Assert.NotNull(metric);
        // 这四个字段对银行天然不适用（行业口径不同，不是数据缺失）
        Assert.Null(metric!.GrossMargin);
        Assert.Null(metric.CurrentRatio);
        Assert.Null(metric.Roic);
        Assert.Null(metric.FreeCashFlow);
        // 但负债率是有的，且天然偏高
        Assert.Equal(90.9067249869m, metric.DebtRatio);
    }

    [Theory]
    [InlineData("银行", true)]
    [InlineData("保险", true)]
    [InlineData("证券", true)]
    [InlineData("通用", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void 金融业判别按公司类型(string? orgType, bool expected) =>
        Assert.Equal(expected, FundamentalOrgTypes.IsFinancial(orgType));

    [Fact]
    public void 金融业不适用字段清单包含实测为空的四项()
    {
        // 清单用于界面把空值显示成「不适用」而不是「暂无数据」
        Assert.Contains(nameof(FundamentalMetric.GrossMargin), FundamentalOrgTypes.NotApplicableFields);
        Assert.Contains(nameof(FundamentalMetric.CurrentRatio), FundamentalOrgTypes.NotApplicableFields);
        Assert.Contains(nameof(FundamentalMetric.QuickRatio), FundamentalOrgTypes.NotApplicableFields);
        Assert.Contains(nameof(FundamentalMetric.Roic), FundamentalOrgTypes.NotApplicableFields);
        Assert.Contains(nameof(FundamentalMetric.FreeCashFlow), FundamentalOrgTypes.NotApplicableFields);

        // 负债率是有的，不该进清单
        Assert.DoesNotContain(nameof(FundamentalMetric.DebtRatio), FundamentalOrgTypes.NotApplicableFields);
    }

    /* ------------------------------------------------------------------
       报告期类型
       ------------------------------------------------------------------ */

    [Theory]
    [InlineData("年报", true)]
    [InlineData("中报", false)]
    [InlineData("一季报", false)]
    [InlineData("三季报", false)]
    [InlineData(null, false)]
    public void 年报判别按报告期类型(string? reportType, bool expected) =>
        Assert.Equal(expected, FundamentalReportTypes.IsAnnual(reportType));

    /* ------------------------------------------------------------------
       连续性条件
       ------------------------------------------------------------------ */

    private static FundamentalMetric Annual(int year, string type, decimal? roe) =>
        new()
        {
            Code = "688110",
            ReportDate = new DateOnly(year, 12, 31),
            ReportType = type,
            RoeWeighted = roe
        };

    private static ContinuousEvaluation Evaluate(
        IEnumerable<FundamentalMetric> series,
        decimal? min,
        int years) =>
        ContinuousConditionRules.Evaluate(series, metric => metric.RoeWeighted, min, null, years);

    [Fact]
    public void 连续五年达标时判定通过()
    {
        var series = new[]
        {
            Annual(2021, "年报", 18m), Annual(2022, "年报", 17m), Annual(2023, "年报", 16m),
            Annual(2024, "年报", 20m), Annual(2025, "年报", 19m)
        };

        var result = Evaluate(series, 15m, 5);

        Assert.True(result.Satisfied);
        Assert.Equal(5, result.Streak);
        Assert.Equal(5, result.Available);
    }

    [Fact]
    public void 中间某年不达标即中断且从最新年往回数()
    {
        var series = new[]
        {
            Annual(2021, "年报", 20m), Annual(2022, "年报", 20m),
            // 2023 只有 8%：不达标
            Annual(2023, "年报", 8m),
            Annual(2024, "年报", 20m), Annual(2025, "年报", 20m)
        };

        var result = Evaluate(series, 15m, 3);

        // 从最新年往回只连续了 2 年（2025、2024），2023 中断
        Assert.False(result.Satisfied);
        Assert.Equal(2, result.Streak);
        Assert.Equal(2023, result.FailedYear);

        // 但数据是齐的：5 年都有年报。可用年数不受「是否满足」影响，
        // 否则界面会把「某年不达标」误报成「历史数据不足」
        Assert.Equal(5, result.Available);
        Assert.False(result.InsufficientHistory(5));
    }

    [Fact]
    public void 跳年不得被判为连续()
    {
        // 「2019 与 2025 都 >15%」中间缺 2020–2024 的年报：
        // 若按「满足的年份计数」会误判为连续
        var series = new[]
        {
            Annual(2019, "年报", 20m),
            Annual(2025, "年报", 20m)
        };

        var result = Evaluate(series, 15m, 2);

        Assert.False(result.Satisfied);
        Assert.Equal(1, result.Streak);
        Assert.Equal(1, result.Available);
        // 只有 1 年年报，确实不足以判定「连续 2 年」
        Assert.True(result.InsufficientHistory(2));
    }

    [Fact]
    public void 某年缺该字段值时该年不算满足但计入可用年数()
    {
        var series = new[]
        {
            Annual(2023, "年报", null),   // 有年报但 ROE 为空
            Annual(2024, "年报", 20m),
            Annual(2025, "年报", 20m)
        };

        var result = Evaluate(series, 15m, 3);

        Assert.False(result.Satisfied);
        Assert.Equal(2, result.Streak);
        // 三年都有年报，只是 2023 的值缺失：这是「不满足」而不是「数据不足」
        Assert.Equal(3, result.Available);
        Assert.False(result.InsufficientHistory(3));
        Assert.Equal(2023, result.FailedYear);
    }

    [Fact]
    public void 历史数据不足与不满足要能区分()
    {
        // 只有 2 年年报，要求 5 年：这是「还没采到」而不是「确实没做到」
        var series = new[] { Annual(2024, "年报", 20m), Annual(2025, "年报", 20m) };

        var result = Evaluate(series, 15m, 5);

        Assert.False(result.Satisfied);
        Assert.True(result.InsufficientHistory(5));
        Assert.Equal(2, result.Available);
    }

    [Fact]
    public void 季报不参与连续性判定()
    {
        // 三期都落在 12 月，但类型明确写着不是年报：
        // 若按「类型或日期」判定，它们会被当成三条年报，可用年数变成 3
        var series = new[]
        {
            Annual(2025, "中报", 20m),
            Annual(2024, "一季报", 20m),
            Annual(2023, "三季报", 20m)
        };

        var result = Evaluate(series, 15m, 2);

        Assert.Equal(0, result.Available);
        Assert.False(result.Satisfied);
    }

    [Fact]
    public void 报告期类型缺失时按十二月兜底判为年报()
    {
        var series = new[]
        {
            Annual(2024, null!, 20m),
            Annual(2025, null!, 20m)
        };

        var result = Evaluate(series, 15m, 2);

        Assert.True(result.Satisfied);
    }

    [Fact]
    public void 空序列不抛异常()
    {
        var result = Evaluate([], 15m, 3);

        Assert.False(result.Satisfied);
        Assert.Equal(0, result.Available);
        Assert.True(result.InsufficientHistory(3));
    }

    [Fact]
    public void 同一自然年度的多期只算一年()
    {
        // 上游同一年度可能返回多行（更正披露）。若按「行数」计数，
        // 同一年会被算成两年，连续性判定会虚高。
        // 精确同报告期的重复行由存储层的 (Code, ReportDate) 主键去重，
        // 这里防的是「同一年、不同报告期」的多行。
        var series = new[]
        {
            Annual(2024, "年报", 20m),
            Annual(2025, "年报", 20m),
            new FundamentalMetric
            {
                Code = "688110",
                ReportDate = new DateOnly(2025, 12, 31),
                ReportType = "年报",
                RoeWeighted = 20m,
                UpdatedAt = DateTimeOffset.Now
            }
        };

        var result = Evaluate(series, 15m, 2);

        // 2025 的两行只算一年，因此可用年数是 2（2024、2025）而不是 3
        Assert.Equal(2, result.Available);
        Assert.Equal(2, result.Streak);
        Assert.True(result.Satisfied);
    }

    [Fact]
    public void 上限条件同样生效()
    {
        var series = new[] { Annual(2024, "年报", 30m), Annual(2025, "年报", 40m) };

        var result = ContinuousConditionRules.Evaluate(
            series, metric => metric.RoeWeighted, null, 50m, 2);

        Assert.True(result.Satisfied);
    }
}
