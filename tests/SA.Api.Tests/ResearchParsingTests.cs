using System.Text.Json;
using SA.Application.Research;
using SA.Contracts.Research;
using SA.Domain.Entities.Finance;
using SA.Domain.Entities.Research;
using SA.Infrastructure.Collect.Adapters;

namespace SA.Api.Tests;

/// <summary>
/// 阶段 3 的解析固化：主营构成、股本结构、公告、研报。
/// </summary>
/// <remarks>
/// 样本是 2026-09-22 真实响应的逐字拷贝。重点固化实施计划 §2.4 点名的坑：
/// 三套口径不混算、<c>其中:</c> 子项计入父项、招股书口径无成本与毛利率、
/// <c>xsjj</c> 空是「暂无待解禁」、研报无目标价。
/// </remarks>
public class ResearchParsingTests
{
    /// <summary>东芯股份 2025 年报按产品口径的两行（真实响应）。</summary>
    private const string CompositionJson =
        """
        {"zygcfx":[
          {"SECUCODE":"688110.SH","REPORT_DATE":"2025-12-31 00:00:00","MAINOP_TYPE":"2","ITEM_NAME":"NAND","MAIN_BUSINESS_INCOME":600771100,"MBI_RATIO":0.652002,"MAIN_BUSINESS_COST":436976600,"MBC_RATIO":0.628257,"MAIN_BUSINESS_RPOFIT":163794500,"MBR_RATIO":0.725117,"GROSS_RPOFIT_RATIO":0.27264,"RANK":1},
          {"SECUCODE":"688110.SH","REPORT_DATE":"2025-12-31 00:00:00","MAINOP_TYPE":"2","ITEM_NAME":"MCP","MAIN_BUSINESS_INCOME":230782600,"MBI_RATIO":0.250463,"MAIN_BUSINESS_COST":204343400,"MBC_RATIO":0.293792,"MAIN_BUSINESS_RPOFIT":26439200,"MBR_RATIO":0.117046,"GROSS_RPOFIT_RATIO":0.114563,"RANK":2}
        ]}
        """;

    /// <summary>2021 中报：含 4 个「其中:」子项（真实响应节选）。</summary>
    private const string CompositionWithSubItemsJson =
        """
        {"zygcfx":[
          {"REPORT_DATE":"2021-06-30 00:00:00","MAINOP_TYPE":"2","ITEM_NAME":"DRAM","MAIN_BUSINESS_INCOME":31273500,"MBI_RATIO":0.068773,"GROSS_RPOFIT_RATIO":0.338213,"RANK":4},
          {"REPORT_DATE":"2021-06-30 00:00:00","MAINOP_TYPE":"2","ITEM_NAME":"其中:SDRAM","MAIN_BUSINESS_INCOME":11121900,"MBI_RATIO":0.024458,"GROSS_RPOFIT_RATIO":null,"RANK":7},
          {"REPORT_DATE":"2021-06-30 00:00:00","MAINOP_TYPE":"2","ITEM_NAME":"其中:LPDRAM","MAIN_BUSINESS_INCOME":9826400,"MBI_RATIO":0.021609,"GROSS_RPOFIT_RATIO":null,"RANK":8},
          {"REPORT_DATE":"2021-06-30 00:00:00","MAINOP_TYPE":"2","ITEM_NAME":"其中:DDR3","MAIN_BUSINESS_INCOME":8688300,"MBI_RATIO":0.019106,"GROSS_RPOFIT_RATIO":null,"RANK":9},
          {"REPORT_DATE":"2021-06-30 00:00:00","MAINOP_TYPE":"2","ITEM_NAME":"其中:PSRAM","MAIN_BUSINESS_INCOME":1636900,"MBI_RATIO":0.0036,"GROSS_RPOFIT_RATIO":null,"RANK":10}
        ]}
        """;

    /// <summary>2021 三季报：招股书口径，成本与毛利率均为 null（真实响应）。</summary>
    private const string ProspectusJson =
        """
        {"zygcfx":[
          {"REPORT_DATE":"2021-09-30 00:00:00","MAINOP_TYPE":"2","ITEM_NAME":"客户合同产生的收入","MAIN_BUSINESS_INCOME":785105518.58,"MBI_RATIO":1,"MAIN_BUSINESS_COST":null,"MBC_RATIO":null,"MAIN_BUSINESS_RPOFIT":null,"MBR_RATIO":null,"GROSS_RPOFIT_RATIO":null,"RANK":1}
        ]}
        """;

    private static List<BusinessComposition> ParseComposition(string json) =>
        EastMoneyBusinessSource.ParseCompositions("688110", JsonDocument.Parse(json).RootElement).ToList();

    /* ------------------------------------------------------------------
       主营构成
       ------------------------------------------------------------------ */

    [Fact]
    public void 主营构成比率按上游小数口径解析()
    {
        var rows = ParseComposition(CompositionJson);

        var nand = rows.Single(row => row.ItemName == "NAND");
        // 上游 0.652002 表示 65.20%，**原样存小数**
        Assert.Equal(0.652002m, nand.IncomeRatio);
        Assert.Equal(0.27264m, nand.GrossProfitRatio);
        Assert.Equal(600771100m, nand.Income);
        Assert.Equal(2, nand.MainOpType);
        Assert.Equal(1, nand.Rank);
        Assert.False(nand.IsSubItem);
    }

    [Fact]
    public void 上游把PROFIT拼成RPOFIT要照它读()
    {
        var rows = ParseComposition(CompositionJson);

        // MAIN_BUSINESS_RPOFIT / GROSS_RPOFIT_RATIO：写成 PROFIT 会读到 null
        Assert.Equal(163794500m, rows.Single(row => row.ItemName == "NAND").Profit);
        Assert.NotNull(rows.Single(row => row.ItemName == "NAND").GrossProfitRatio);
    }

    [Fact]
    public void 其中前缀的项被标记为子项()
    {
        var rows = ParseComposition(CompositionWithSubItemsJson);

        var subItems = rows.Where(row => row.IsSubItem).ToList();
        Assert.Equal(4, subItems.Count);
        Assert.Contains(subItems, row => row.ItemName.Contains("SDRAM", StringComparison.Ordinal));
        Assert.Contains(subItems, row => row.ItemName.Contains("LPDRAM", StringComparison.Ordinal));

        // 父项 DRAM 不是子项
        var dram = rows.Single(row => row.ItemName == "DRAM");
        Assert.False(dram.IsSubItem);

        // 子项收入之和**恰好等于**父项收入（实测 DRAM 31,273,500 =
        // SDRAM 11,121,900 + LPDRAM 9,826,400 + DDR3 8,688,300 + PSRAM 1,636,900）。
        // 也就是说子项就是父项的明细分解：若与父项并列展示，同一笔收入会被算两次。
        var subTotal = subItems.Sum(row => row.Income ?? 0m);
        Assert.Equal(dram.Income, subTotal);
    }

    [Fact]
    public void 招股书口径只有收入没有成本与毛利率()
    {
        var rows = ParseComposition(ProspectusJson);

        var row = Assert.Single(rows);
        Assert.Equal("客户合同产生的收入", row.ItemName);
        Assert.Equal(785105518.58m, row.Income);
        // 这两项必须保持 null：填 0 会让毛利率显示成 0%
        Assert.Null(row.Cost);
        Assert.Null(row.GrossProfitRatio);
        Assert.Null(row.Profit);
    }

    [Fact]
    public void 业务范围与经营评述按原文解析()
    {
        const string json =
            """
            {"zyfw":[{"BUSINESS_SCOPE":"一般项目:集成电路设计;集成电路销售。"}],"jyps":[{"BUSINESS_REVIEW":"报告期内，公司实现营业收入…"}]}
            """;

        var root = JsonDocument.Parse(json).RootElement;
        Assert.Equal("一般项目:集成电路设计;集成电路销售。", EastMoneyBusinessSource.ParseBusinessScope(root));
        Assert.Equal("报告期内，公司实现营业收入…", EastMoneyBusinessSource.ParseBusinessReview(root));
    }

    [Fact]
    public void 缺少zygcfx时返回空而不抛异常()
    {
        var rows = ParseComposition("""{"zyfw":[]}""");
        Assert.Empty(rows);
    }

    /* ------------------------------------------------------------------
       股本结构
       ------------------------------------------------------------------ */

    [Fact]
    public void xsjj为空表示暂无待解禁且已全流通()
    {
        const string json =
            """
            {"xsjj":[],"gbjg":[{"TOTAL_SHARES":442377391,"UNLIMITED_SHARES":442377391,"LIMITED_SHARES":null,"LISTED_A_SHARES":442377391}],
             "lngbbd":[{"END_DATE":"2026-06-05 00:00:00","TOTAL_SHARES":442377391,"LIMITED_SHARES":null,"UNLIMITED_SHARES":442377391,"LISTED_A_SHARES":442377391,"CHANGE_REASON":"限制性股票"}]}
            """;

        var root = JsonDocument.Parse(json).RootElement;
        var unlocks = EastMoneyCapitalStructureSource.ParseUpcomingUnlocks("688110", root).ToList();
        var changes = EastMoneyCapitalStructureSource.ParseShareChanges("688110", root).ToList();

        // 空列表：由聚合层映射为 noUpcoming（「暂无待解禁」），绝不与「没查到」混同
        Assert.Empty(unlocks);

        var latest = Assert.Single(changes);
        Assert.Equal(new DateOnly(2026, 6, 5), latest.EndDate);
        Assert.Equal(442377391m, latest.TotalShares);
        Assert.Equal(442377391m, latest.UnlimitedShares);
        // LIMITED_SHARES 为 null：表示没有限售股，展示层按 0 处理而不是「暂无数据」
        Assert.Null(latest.LimitedShares);
        Assert.Equal("限制性股票", latest.ChangeReason);
    }

    [Fact]
    public void 有待解禁时解析解禁日期股数比例与类型()
    {
        const string json =
            """
            {"xsjj":[{"LIFT_DATE":"2027-06-23 00:00:00","LIFT_NUM":547182073,"LIFT_TYPE":"定向增发机构配售股份","TOTAL_SHARES_RATIO":6.39,"UNLIMITED_A_SHARES_RATIO":27.35}],"lngbbd":[]}
            """;

        var unlock = Assert.Single(
            EastMoneyCapitalStructureSource.ParseUpcomingUnlocks("688981", JsonDocument.Parse(json).RootElement));

        Assert.Equal(new DateOnly(2027, 6, 23), unlock.LiftDate);
        Assert.Equal(547182073m, unlock.LiftShares);
        // 这两个比例上游给的是百分数（6.39 表示 6.39%）
        Assert.Equal(6.39m, unlock.TotalSharesRatio);
        Assert.Equal(27.35m, unlock.UnlimitedASharesRatio);
        Assert.Equal("定向增发机构配售股份", unlock.LiftType);
    }

    [Fact]
    public void 解禁类型缺失时用占位而不是丢弃整行()
    {
        const string json =
            """
            {"xsjj":[{"LIFT_DATE":"2027-06-23 00:00:00","LIFT_NUM":100,"LIFT_TYPE":null}],"lngbbd":[]}
            """;

        // 类型是主键的一部分，缺失时占位「未知」：丢弃整行会静默少一条待解禁
        var unlock = Assert.Single(
            EastMoneyCapitalStructureSource.ParseUpcomingUnlocks("688981", JsonDocument.Parse(json).RootElement));
        Assert.Equal("未知", unlock.LiftType);
    }

    /* ------------------------------------------------------------------
       公告
       ------------------------------------------------------------------ */

    [Fact]
    public void 公告解析标题日期类型与原文链接()
    {
        const string json =
            """
            {"data":{"list":[{"art_code":"AN202609171829526277","codes":[{"ann_type":"A,KCB,SHA","short_name":"东芯股份","stock_code":"688110"}],"columns":[{"column_code":"001002006005","column_name":"签订协议"}],"display_time":"2026-09-17 19:01:36:224","notice_date":"2026-09-18 00:00:00","source_type":"313","title":"东芯股份:关于开设募集资金专项账户的公告"}]}}
            """;

        var row = Assert.Single(
            EastMoneyAnnouncementSource.Parse("688110", JsonDocument.Parse(json).RootElement));

        Assert.Equal("AN202609171829526277", row.ArtCode);
        Assert.Equal(new DateOnly(2026, 9, 18), row.NoticeDate);
        Assert.Equal("签订协议", row.ColumnName);
        Assert.Equal("A,KCB,SHA", row.AnnType);
        Assert.Equal("313", row.SourceType);
        // 原文链接由 art_code 拼出（实测该地址返回 200）
        Assert.Equal(
            "https://data.eastmoney.com/notices/detail/688110/AN202609171829526277.html",
            row.Url);
    }

    [Fact]
    public void 一个公告有多个类型时保留全部并取第一个为主类型()
    {
        const string json =
            """
            {"data":{"list":[{"art_code":"AN1","title":"标题","notice_date":"2026-09-18 00:00:00","columns":[{"column_name":"签订协议"},{"column_name":"调研活动"}]}]}}
            """;

        var row = Assert.Single(
            EastMoneyAnnouncementSource.Parse("688110", JsonDocument.Parse(json).RootElement));

        Assert.Equal("签订协议", row.ColumnName);
        Assert.Equal("签订协议,调研活动", row.ColumnNames);
    }

    [Fact]
    public void 公告日期缺失时退回展示时间()
    {
        const string json =
            """
            {"data":{"list":[{"art_code":"AN1","title":"标题","display_time":"2026-09-17 19:01:36:224"}]}}
            """;

        var row = Assert.Single(
            EastMoneyAnnouncementSource.Parse("688110", JsonDocument.Parse(json).RootElement));

        Assert.Equal(new DateOnly(2026, 9, 17), row.NoticeDate);
    }

    [Fact]
    public void 公告缺少art_code或标题时跳过该行()
    {
        const string json =
            """
            {"data":{"list":[{"title":"无 Id","notice_date":"2026-09-18 00:00:00"},{"art_code":"AN2","notice_date":"2026-09-18 00:00:00"}]}}
            """;

        Assert.Empty(EastMoneyAnnouncementSource.Parse("688110", JsonDocument.Parse(json).RootElement));
    }

    /* ------------------------------------------------------------------
       研报
       ------------------------------------------------------------------ */

    [Fact]
    public void 研报解析评级与三年盈利预测()
    {
        const string json =
            """
            {"hits":1,"data":[{"title":"点评报告：利基芯片高景气核心标的","orgName":"华龙证券股份有限公司","orgSName":"华龙证券","researcher":"李浩洋","publishDate":"2026-06-30 00:00:00.000","infoCode":"AP202606301826595520","predictNextTwoYearEps":"1.94","predictNextTwoYearPe":"99.8","predictNextYearEps":"1.69","predictNextYearPe":"114.2","predictThisYearEps":"1.52","predictThisYearPe":"127","indvInduName":"半导体","emRatingName":"增持","ratingChange":2,"encodeUrl":"pbgGimF6GIUsJi/NFvqdc+apen5ULlsHyK7TSF4FHrI=","indvAimPriceT":"","indvAimPriceL":""}]}
            """;

        var row = Assert.Single(
            EastMoneyResearchSource.Parse("688110", JsonDocument.Parse(json).RootElement));

        Assert.Equal("AP202606301826595520", row.InfoCode);
        Assert.Equal(new DateOnly(2026, 6, 30), row.PublishDate);
        Assert.Equal("华龙证券", row.OrgShortName);
        Assert.Equal("李浩洋", row.Researcher);
        Assert.Equal("增持", row.RatingName);
        Assert.Equal("半导体", row.IndustryName);
        Assert.Equal(2, row.RatingChange);

        // 三年 PE 预测：原样展示、不做解读
        Assert.Equal(127m, row.PredictThisYearPe);
        Assert.Equal(114.2m, row.PredictNextYearPe);
        Assert.Equal(99.8m, row.PredictNextTwoYearPe);

        // encodeUrl 里含 + / =，必须转义后才拼进查询串
        Assert.Contains("encodeUrl=", row.Url!);
        Assert.Contains("%2F", row.Url!);
    }

    [Fact]
    public void 研报实体没有目标价字段()
    {
        // 实测 indvAimPriceT / indvAimPriceL 全为空字符串，按 §1.3 不展示目标价。
        // 实体里没有这个属性，代码就不可能把它渲染出来。
        Assert.Null(typeof(ResearchReport).GetProperty("AimPriceHigh"));
        Assert.Null(typeof(ResearchReport).GetProperty("AimPriceLow"));
        Assert.Null(typeof(ResearchReport).GetProperty("TargetPrice"));
    }

    [Fact]
    public void 预测PE为负数或极大值时原样保留()
    {
        // 实测有些研报的预测 PE 是 -1186.76 / 16566.9：那是「预测利润接近 0」的真实表达，
        // 不是数据错误，过滤掉会丢失有效信息
        const string json =
            """
            {"data":[{"title":"t","infoCode":"AP1","publishDate":"2025-11-30 00:00:00.000","predictThisYearPe":"-1186.7600000000"}]}
            """;

        var row = Assert.Single(
            EastMoneyResearchSource.Parse("688110", JsonDocument.Parse(json).RootElement));

        Assert.Equal(-1186.76m, row.PredictThisYearPe);
    }

    /* ------------------------------------------------------------------
       F10 代码形态
       ------------------------------------------------------------------ */

    [Theory]
    [InlineData("688110", "SH688110")]
    [InlineData("300750", "SZ300750")]
    [InlineData("920000", "BJ920000")]
    [InlineData("600519", "SH600519")]
    public void F10代码是后缀在前(string code, string expected) =>
        Assert.Equal(expected, F10Codes.Of(code));

    /* ------------------------------------------------------------------
       价值研究聚合的三态判定
       ------------------------------------------------------------------ */

    [Fact]
    public void 状态常量覆盖四态()
    {
        // 四态必须都在：把「不适用」显示成「暂无数据」会误导用户去等一个永远不会来的数据；
        // 「暂无待解禁」是「确实没有」而不是「没查到」
        Assert.Equal("ok", ResearchStatuses.Ok);
        Assert.Equal("noData", ResearchStatuses.NoData);
        Assert.Equal("notApplicable", ResearchStatuses.NotApplicable);
        Assert.Equal("noUpcoming", ResearchStatuses.NoUpcoming);
    }

    [Fact]
    public void 金融业不适用字段清单与实测一致()
    {
        // 平安银行实测这四个字段为 null，而资产负债率有值
        Assert.Contains(nameof(FundamentalMetric.GrossMargin), FundamentalOrgTypes.NotApplicableFields);
        Assert.Contains(nameof(FundamentalMetric.CurrentRatio), FundamentalOrgTypes.NotApplicableFields);
        Assert.Contains(nameof(FundamentalMetric.QuickRatio), FundamentalOrgTypes.NotApplicableFields);
        Assert.Contains(nameof(FundamentalMetric.Roic), FundamentalOrgTypes.NotApplicableFields);
        Assert.Contains(nameof(FundamentalMetric.FreeCashFlow), FundamentalOrgTypes.NotApplicableFields);
        Assert.DoesNotContain(nameof(FundamentalMetric.DebtRatio), FundamentalOrgTypes.NotApplicableFields);
    }
}
