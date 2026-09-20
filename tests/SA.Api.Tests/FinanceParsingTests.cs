using System.Text.Json;
using SA.Infrastructure.Collect.Adapters;

namespace SA.Api.Tests;

/// <summary>
/// 财务适配器的字段映射固化。样本是 2026-09-20 抓到的真实响应（300750 2026 半年报），
/// 上游改字段名或改单位时这里会先失败（详细设计 §13.2）。
/// </summary>
public class FinanceParsingTests
{
    /// <summary>业绩报表原始响应中的一行（逐字拷贝，只删去与本测试无关的字段）。</summary>
    private const string ReportResponse = """
    {
      "version": "abc",
      "result": {
        "pages": 9,
        "count": 41,
        "data": [
          {
            "SECURITY_CODE": "300750",
            "SECURITY_NAME_ABBR": "宁德时代",
            "UPDATE_DATE": "2026-07-25 00:00:00",
            "REPORTDATE": "2026-06-30 00:00:00",
            "BASIC_EPS": 9.51,
            "DEDUCT_BASIC_EPS": 8.57,
            "TOTAL_OPERATE_INCOME": 276916580000,
            "PARENT_NETPROFIT": 43284002000,
            "WEIGHTAVG_ROE": 12.08,
            "YSTZ": 54.8003691485,
            "SJLTZ": 41.98,
            "BPS": 81.993154443679,
            "MGJYXJJE": 13.015213596184,
            "XSMLL": 23.9283938867,
            "YSHZ": 14.4462,
            "SJLHZ": 8.7212,
            "ASSIGNDSCRPT": "10派14.11元(含税,扣税后12.699元)",
            "ZXGXL": 0.363594196923,
            "NOTICE_DATE": "2026-07-25 00:00:00",
            "QDATE": "2026Q2",
            "DATATYPE": "2026年 半年报",
            "BOARD_NAME": "电池"
          }
        ]
      }
    }
    """;

    /// <summary>业绩预告原始响应中的一行。</summary>
    private const string ForecastResponse = """
    {
      "result": {
        "data": [
          {
            "SECURITY_CODE": "300750",
            "NOTICE_DATE": "2018-07-13 00:00:00",
            "REPORT_DATE": "2018-06-30 00:00:00",
            "PREDICT_FINANCE": "扣除非经常性损益后的净利润",
            "PREDICT_AMT_LOWER": 671101600,
            "PREDICT_AMT_UPPER": 712613000,
            "ADD_AMP_LOWER": 31.43,
            "ADD_AMP_UPPER": 39.56,
            "PREDICT_CONTENT": "预计2018年1-6月扣除非经常性损益后的净利润盈利67,110.16万元-71,261.30万元,同比增长31.43%-39.56%。",
            "PREDICT_TYPE": "略增"
          }
        ]
      }
    }
    """;

    [Fact]
    public void 业绩报表按字段名解析到正确字段()
    {
        var report = EastMoneyFinanceSource.ParseReports("300750", JsonDocument.Parse(ReportResponse).RootElement).Single();

        Assert.Equal("300750", report.Code);
        Assert.Equal(new DateOnly(2026, 6, 30), report.ReportDate);
        Assert.Equal("2026年 半年报", report.ReportType);
        Assert.Equal("2026Q2", report.Quarter);
        Assert.Equal("电池", report.Industry);

        // 金额是「元」，接口层再换算为亿元；这里必须原样保留元
        Assert.Equal(276_916_580_000m, report.Revenue);
        Assert.Equal(43_284_002_000m, report.NetProfit);

        // 比率是上游算好的百分数（不在本地重算）
        Assert.Equal(54.8003691485m, report.RevenueYoy);
        Assert.Equal(41.98m, report.NetProfitYoy);
        Assert.Equal(12.08m, report.Roe);
        Assert.Equal(23.9283938867m, report.GrossMargin);
        Assert.Equal(14.4462m, report.RevenueQoq);
        Assert.Equal(8.7212m, report.NetProfitQoq);

        Assert.Equal(9.51m, report.Eps);
        Assert.Equal(8.57m, report.DeductedEps);
        Assert.Equal(81.993154443679m, report.Bps);
        Assert.Equal(13.015213596184m, report.OperatingCashFlowPerShare);
        Assert.Equal(0.363594196923m, report.DividendYield);
        Assert.Equal("10派14.11元(含税,扣税后12.699元)", report.DividendPlan);

        // 公告日期只取日期部分
        Assert.Equal(new DateOnly(2026, 7, 25), report.NoticeDate);
    }

    [Fact]
    public void 业绩报表无数据时返回空而不是异常()
    {
        // 新股在上市初期可能没有任何财报记录：上游 result 为 null
        var empty = JsonDocument.Parse("""{"version":"x","result":null,"success":true}""").RootElement;

        Assert.Empty(EastMoneyFinanceSource.ParseReports("999999", empty));
    }

    [Fact]
    public void 缺报告期的行被丢弃()
    {
        var data = JsonDocument.Parse(
            """{"result":{"data":[{"SECURITY_CODE":"300750","REPORTDATE":null,"TOTAL_OPERATE_INCOME":1}]}}""").RootElement;

        Assert.Empty(EastMoneyFinanceSource.ParseReports("300750", data));
    }

    /// <summary>
    /// 上游对同一报告期会给出多条记录：既可能是「首次预告 + 修正公告」，
    /// 也可能是同一天按不同口径给出的多条（含完全重复的行）。
    /// 必须收敛成「每报告期一条」，否则会撞主键导致整批写不进去（实测过的故障形态）。
    /// </summary>
    private const string DuplicateForecastResponse = """
    {
      "result": {
        "data": [
          {
            "NOTICE_DATE": "2025-01-03 00:00:00",
            "REPORT_DATE": "2024-12-31 00:00:00",
            "PREDICT_FINANCE": "扣除非经常性损益后的净利润",
            "PREDICT_AMT_LOWER": 100, "PREDICT_AMT_UPPER": 200,
            "ADD_AMP_LOWER": 1.0, "ADD_AMP_UPPER": 2.0,
            "PREDICT_TYPE": "略增"
          },
          {
            "NOTICE_DATE": "2025-01-03 00:00:00",
            "REPORT_DATE": "2024-12-31 00:00:00",
            "PREDICT_FINANCE": "归属于母公司股东的净利润",
            "PREDICT_AMT_LOWER": 300, "PREDICT_AMT_UPPER": 400,
            "ADD_AMP_LOWER": 3.0, "ADD_AMP_UPPER": 4.0,
            "PREDICT_TYPE": "略增"
          },
          {
            "NOTICE_DATE": "2023-12-30 00:00:00",
            "REPORT_DATE": "2023-12-31 00:00:00",
            "PREDICT_FINANCE": "归属于母公司股东的净利润",
            "PREDICT_AMT_LOWER": 900, "PREDICT_AMT_UPPER": 1000,
            "ADD_AMP_LOWER": 9.0, "ADD_AMP_UPPER": 10.0,
            "PREDICT_TYPE": "预增"
          }
        ]
      }
    }
    """;

    [Fact]
    public void 业绩预告按报告期去重并优先归母净利润口径()
    {
        var forecasts = SA.Domain.Entities.Finance.EarningsForecastRules
            .LatestPerReportDate(EastMoneyFinanceSource.ParseForecasts("600519", JsonDocument.Parse(DuplicateForecastResponse).RootElement))
            .ToList();

        // 两个报告期 → 两条
        Assert.Equal(2, forecasts.Count);

        // 按报告期倒序
        Assert.Equal(new DateOnly(2024, 12, 31), forecasts[0].ReportDate);
        Assert.Equal(new DateOnly(2023, 12, 31), forecasts[1].ReportDate);

        // 同日两条里取「归母净利润」口径（数值 300~400，而不是扣非的 100~200）
        Assert.Contains("归属于母公司股东的净利润", forecasts[0].Caliber!, StringComparison.Ordinal);
        Assert.Equal(300m, forecasts[0].NetProfitMin);
        Assert.Equal(400m, forecasts[0].NetProfitMax);
    }

    [Fact]
    public void 业绩预告解析正确()
    {
        var forecast = EastMoneyFinanceSource.ParseForecasts("300750", JsonDocument.Parse(ForecastResponse).RootElement).Single();

        Assert.Equal(new DateOnly(2018, 6, 30), forecast.ReportDate);
        Assert.Equal(new DateOnly(2018, 7, 13), forecast.NoticeDate);
        Assert.Equal("略增", forecast.ForecastType);
        Assert.Equal(671_101_600m, forecast.NetProfitMin);
        Assert.Equal(712_613_000m, forecast.NetProfitMax);
        Assert.Equal(31.43m, forecast.ChangeMin);
        Assert.Equal(39.56m, forecast.ChangeMax);
        Assert.Contains("67,110.16万元", forecast.Summary!, StringComparison.Ordinal);
    }
}
