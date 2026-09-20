using SA.Application.Abstractions;
using SA.Application.Common;
using SA.Contracts.Common;
using SA.Contracts.Finance;
using SA.Domain.Common;
using SA.Domain.Entities.Finance;

namespace SA.Application.Finance;

/// <summary>
/// 盈利与财务表现读模型。
/// </summary>
/// <remarks>
/// <para>
/// 三个口径约定（实施计划 §10）：
/// </para>
/// <list type="number">
/// <item>报告期是<b>累计口径</b>（半年报 = 上半年累计），不做单季拆分，与「半年报营收」的通常理解一致；</item>
/// <item>同比/环比直接用上游算好的值：上游用的是调整后同口径数据，本地两期相除会在追溯调整时算错；</item>
/// <item>金额换算成亿元，比率保持百分数，缺失一律为 null、界面显示「—」。</item>
/// </list>
/// <para>
/// 财务数据是季频的，因此不加任何实时逻辑；接口只读库，缺数据由按需采集补齐并返回 <c>1003</c>。
/// </para>
/// </remarks>
public sealed class FinanceService(
    IFinanceStore finance,
    IInstrumentStore instruments,
    IOnDemandQueue onDemand)
{
    /// <summary>趋势图展示的期数上限（20 期 ≈ 5 年）。</summary>
    public const int TrendLimit = 20;

    /// <summary>
    /// 组装财务视图。
    /// </summary>
    public async Task<ServiceResult<FinanceDto>> GetAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var instrument = await instruments.FindAsync(code, cancellationToken).ConfigureAwait(false);
        if (instrument is null)
        {
            return ServiceResult<FinanceDto>.Fail(ErrorCode.NotFound, $"未找到证券代码 {code}");
        }

        var reports = await finance.GetReportsAsync(code, 24, cancellationToken).ConfigureAwait(false);
        if (reports.Count == 0)
        {
            // 入队后由采集调度补齐；前端按 1003 重试
            onDemand.TryEnqueue(code);
            return ServiceResult<FinanceDto>.Fail(ErrorCode.DataNotReady, "财务数据正在采集，请稍后重试");
        }

        var forecasts = await finance.GetForecastsAsync(code, 8, cancellationToken).ConfigureAwait(false);
        var periods = reports
            .OrderByDescending(report => report.ReportDate)
            .Select(ToPeriod)
            .ToList();

        var trend = reports
            .TakeLast(TrendLimit)
            .Select(report => new FinanceTrendPointDto(
                Label: report.Quarter ?? SaTime.Format(report.ReportDate),
                Revenue: ToYi(report.Revenue),
                NetProfit: ToYi(report.NetProfit),
                Roe: Display.Round(report.Roe),
                GrossMargin: Display.Round(report.GrossMargin),
                RevenueYoy: Display.Round(report.RevenueYoy),
                NetProfitYoy: Display.Round(report.NetProfitYoy),
                OperatingCashFlowPerShare: Display.Round(report.OperatingCashFlowPerShare),
                Eps: Display.Round(report.Eps)))
            .ToList();

        var lastUpdated = await finance.GetLastUpdatedAtAsync(code, cancellationToken).ConfigureAwait(false);

        return ServiceResult<FinanceDto>.Success(new FinanceDto(
            Code: code,
            Name: instrument.Name,
            AsOf: lastUpdated is null ? null : SaTime.Format(lastUpdated.Value),
            Latest: periods.Count > 0 ? periods[0] : null,
            Periods: periods.Take(12).ToList(),
            Trend: trend,
            Forecasts: forecasts.Select(forecast => new EarningsForecastDto(
                ReportDate: SaTime.Format(forecast.ReportDate),
                Caliber: forecast.Caliber,
                ForecastType: forecast.ForecastType,
                Summary: forecast.Summary,
                NetProfitMin: ToYi(forecast.NetProfitMin),
                NetProfitMax: ToYi(forecast.NetProfitMax),
                ChangeMin: Display.Round(forecast.ChangeMin),
                ChangeMax: Display.Round(forecast.ChangeMax),
                NoticeDate: forecast.NoticeDate is null ? null : SaTime.Format(forecast.NoticeDate.Value))).ToList(),
            Insights: BuildInsights(reports),
            Notes:
            [
                "口径：报告期为累计口径（半年报即上半年累计），不做单季拆分。",
                "同比与环比取自上游的调整后同口径计算（不在本地用两期相除，避免追溯调整算错）。",
                "金额统一为亿元；比率为百分数；缺失的字段返回空、界面显示「—」，不以 0 代替。",
                "数据来源：东方财富公开报表接口（业绩报表 / 业绩预告）。"
            ]));
    }

    /// <summary>
    /// 财务结论。全部由可复算的规则给出（只看最近两期的方向与稳定性）。
    /// </summary>
    private static List<string> BuildInsights(IReadOnlyList<FinancialReport> reports)
    {
        var insights = new List<string>();
        var latest = reports[^1];

        if (latest.RevenueYoy is { } revenueYoy)
        {
            insights.Add(revenueYoy >= 0
                ? $"营收同比 +{revenueYoy:F2}%"
                : $"营收同比 {revenueYoy:F2}%");
        }

        if (latest.NetProfitYoy is { } profitYoy)
        {
            insights.Add(profitYoy >= 0
                ? $"净利润同比 +{profitYoy:F2}%"
                : $"净利润同比 {profitYoy:F2}%");
        }

        // 增收不增利：营收增长而净利下滑，是财务分析里最需要点出来的一种组合
        if (latest.RevenueYoy is > 0 and { } revenueUp && latest.NetProfitYoy is < 0 and { } profitDown)
        {
            insights.Add($"增收不增利（营收 +{revenueUp:F2}%，净利 {profitDown:F2}%）");
        }

        if (latest.Roe is { } roe)
        {
            insights.Add($"加权 ROE {roe:F2}%");
        }

        if (latest.GrossMargin is { } margin)
        {
            insights.Add($"毛利率 {margin:F2}%");
        }

        // 连续两期净利同比同向，才给出「连续」判断（只有一期不足以称连续）
        if (reports.Count >= 2)
        {
            var current = reports[^1].NetProfitYoy;
            var previous = reports[^2].NetProfitYoy;
            if (current is not null && previous is not null)
            {
                if (current < 0 && previous < 0)
                {
                    insights.Add("净利润连续两期同比下滑");
                }
                else if (current > 0 && previous > 0)
                {
                    insights.Add("净利润连续两期同比增长");
                }
            }
        }

        // 经营现金流为负而净利为正：盈利质量存疑
        if (latest.OperatingCashFlowPerShare is < 0 && latest.Eps is > 0)
        {
            insights.Add("每股经营现金流为负而每股收益为正，盈利质量需关注");
        }

        return insights;
    }

    private static FinancePeriodDto ToPeriod(FinancialReport report) =>
        new(
            ReportDate: SaTime.Format(report.ReportDate),
            ReportType: report.ReportType,
            Quarter: report.Quarter,
            Revenue: ToYi(report.Revenue),
            RevenueYoy: Display.Round(report.RevenueYoy),
            NetProfit: ToYi(report.NetProfit),
            NetProfitYoy: Display.Round(report.NetProfitYoy),
            Eps: Display.Round(report.Eps),
            DeductedEps: Display.Round(report.DeductedEps),
            Roe: Display.Round(report.Roe),
            Bps: Display.Round(report.Bps),
            OperatingCashFlowPerShare: Display.Round(report.OperatingCashFlowPerShare),
            GrossMargin: Display.Round(report.GrossMargin),
            RevenueQoq: Display.Round(report.RevenueQoq),
            NetProfitQoq: Display.Round(report.NetProfitQoq),
            DividendPlan: report.DividendPlan,
            DividendYield: Display.Round(report.DividendYield),
            NoticeDate: report.NoticeDate is null ? null : SaTime.Format(report.NoticeDate.Value));

    /// <summary>元转亿元；为 null 时保持 null（不以 0 代替缺失）。</summary>
    private static decimal? ToYi(decimal? yuan) => yuan is null ? null : Display.ToYi(yuan.Value);
}
