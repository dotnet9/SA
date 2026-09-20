using Microsoft.EntityFrameworkCore;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Finance;

namespace SA.Infrastructure.Persistence.Stores;

/// <summary>
/// 财务报表存储。按（代码, 报告期）upsert，重复采集覆盖同一期（业绩快报→正式报告会更新）。
/// </summary>
public sealed class FinanceStore(SaDbContext db) : IFinanceStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<FinancialReport>> GetReportsAsync(
        string code,
        int limit = 24,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.FinancialReports
            .AsNoTracking()
            .Where(report => report.Code == code)
            .OrderByDescending(report => report.ReportDate)
            .Take(Math.Clamp(limit, 1, 80))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        rows.Reverse();
        return rows;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EarningsForecast>> GetForecastsAsync(
        string code,
        int limit = 8,
        CancellationToken cancellationToken = default) =>
        await _db.EarningsForecasts
            .AsNoTracking()
            .Where(forecast => forecast.Code == code)
            .OrderByDescending(forecast => forecast.ReportDate)
            .ThenByDescending(forecast => forecast.NoticeDate)
            .Take(Math.Clamp(limit, 1, 40))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> UpsertReportsAsync(
        IReadOnlyList<FinancialReport> reports,
        CancellationToken cancellationToken = default)
    {
        if (reports.Count == 0)
        {
            return 0;
        }

        var code = reports[0].Code;
        var dates = reports.Select(report => report.ReportDate).Distinct().ToList();

        var existing = await _db.FinancialReports
            .Where(report => report.Code == code && dates.Contains(report.ReportDate))
            .ToDictionaryAsync(report => report.ReportDate, cancellationToken).ConfigureAwait(false);

        foreach (var report in reports)
        {
            if (existing.TryGetValue(report.ReportDate, out var row))
            {
                Copy(report, row);
            }
            else
            {
                await _db.FinancialReports.AddAsync(report, cancellationToken).ConfigureAwait(false);
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return reports.Count;
    }

    /// <inheritdoc />
    public async Task<int> UpsertForecastsAsync(
        IReadOnlyList<EarningsForecast> forecasts,
        CancellationToken cancellationToken = default)
    {
        if (forecasts.Count == 0)
        {
            return 0;
        }

        var code = forecasts[0].Code;
        var dates = forecasts.Select(forecast => forecast.ReportDate).Distinct().ToList();

        // 主键是（代码, 报告期）：上游同一期可能给出多条，这里用与采集端相同的领域规则兜底，
        // 避免因重复行撞主键而让整批写入失败（实测过的故障形态）
        var rows = EarningsForecastRules.LatestPerReportDate(forecasts).ToList();

        var existing = await _db.EarningsForecasts
            .Where(forecast => forecast.Code == code && dates.Contains(forecast.ReportDate))
            .ToDictionaryAsync(forecast => forecast.ReportDate, cancellationToken).ConfigureAwait(false);

        foreach (var forecast in rows)
        {
            if (existing.TryGetValue(forecast.ReportDate, out var row))
            {
                row.Caliber = forecast.Caliber;
                row.ForecastType = forecast.ForecastType;
                row.Summary = forecast.Summary;
                row.NetProfitMin = forecast.NetProfitMin;
                row.NetProfitMax = forecast.NetProfitMax;
                row.ChangeMin = forecast.ChangeMin;
                row.ChangeMax = forecast.ChangeMax;
                row.NoticeDate = forecast.NoticeDate;
                row.UpdatedAt = forecast.UpdatedAt;
            }
            else
            {
                await _db.EarningsForecasts.AddAsync(forecast, cancellationToken).ConfigureAwait(false);
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rows.Count;
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastUpdatedAtAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        await _db.FinancialReports
            .AsNoTracking()
            .Where(report => report.Code == code)
            .OrderByDescending(report => report.UpdatedAt)
            .Select(report => (DateTimeOffset?)report.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>把新数据覆盖到已有行上（不新建实例，保持 EF 跟踪关系）。</summary>
    private static void Copy(FinancialReport source, FinancialReport target)
    {
        target.ReportType = source.ReportType;
        target.Quarter = source.Quarter;
        target.Revenue = source.Revenue;
        target.RevenueYoy = source.RevenueYoy;
        target.NetProfit = source.NetProfit;
        target.NetProfitYoy = source.NetProfitYoy;
        target.Eps = source.Eps;
        target.DeductedEps = source.DeductedEps;
        target.Roe = source.Roe;
        target.Bps = source.Bps;
        target.OperatingCashFlowPerShare = source.OperatingCashFlowPerShare;
        target.GrossMargin = source.GrossMargin;
        target.RevenueQoq = source.RevenueQoq;
        target.NetProfitQoq = source.NetProfitQoq;
        target.DividendPlan = source.DividendPlan;
        target.DividendYield = source.DividendYield;
        target.NoticeDate = source.NoticeDate;
        target.Industry = source.Industry;
        target.UpdatedAt = source.UpdatedAt;
    }
}
