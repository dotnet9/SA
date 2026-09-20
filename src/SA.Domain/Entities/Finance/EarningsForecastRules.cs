namespace SA.Domain.Entities.Finance;

/// <summary>
/// 业绩预告的收敛规则。
/// </summary>
/// <remarks>
/// <para>
/// 上游对同一报告期会返回多条记录：既有「首次预告 + 修正公告」的时间序列，
/// 也有同一天按不同口径（净利润 / 扣非净利润）同时给出的多条，甚至出现完全重复的行
/// （实测 600519 在 2025-01-03 即如此）。
/// </para>
/// <para>
/// 业务上页面需要的是「每个报告期的最新一次预告」，因此统一收敛为一条。
/// 这条规则同时被采集端（解析后收敛）与存储端（写入前兜底）使用——
/// <b>放在领域层而不是某一端</b>，是为了避免「适配器收敛了、存储没收敛」这类只在特定调用路径上出现的故障
/// （实测过的形态就是整批预告因为撞主键而一条都没写进去）。
/// </para>
/// </remarks>
public static class EarningsForecastRules
{
    /// <summary>
    /// 每个报告期只保留一条：先取公告日期最新的，同一公告日则优先「归母净利润」口径。
    /// </summary>
    /// <param name="forecasts">预告集合（可含重复）。</param>
    /// <returns>按报告期倒序的收敛结果。</returns>
    public static IEnumerable<EarningsForecast> LatestPerReportDate(IEnumerable<EarningsForecast> forecasts) =>
        forecasts
            .GroupBy(forecast => forecast.ReportDate)
            .Select(group => group
                .OrderByDescending(forecast => forecast.NoticeDate ?? DateOnly.MinValue)
                .ThenByDescending(forecast => IsNetProfitCaliber(forecast.Caliber) ? 1 : 0)
                .First())
            .OrderByDescending(forecast => forecast.ReportDate);

    /// <summary>
    /// 是否为「归母净利润」口径。
    /// </summary>
    /// <remarks>
    /// 扣非口径的名字里带「扣除」（「扣除非经常性损益后的净利润」），
    /// 展示成净利润预告会误导，因此同一天有多条时优先取不带「扣除」的那条。
    /// </remarks>
    public static bool IsNetProfitCaliber(string? caliber) =>
        !string.IsNullOrEmpty(caliber)
        && caliber.Contains("净利润", StringComparison.Ordinal)
        && !caliber.Contains("扣除", StringComparison.Ordinal);
}
