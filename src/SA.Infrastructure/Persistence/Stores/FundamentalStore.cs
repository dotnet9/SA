using Microsoft.EntityFrameworkCore;
using SA.Application.Abstractions;
using SA.Domain.Entities.Finance;

namespace SA.Infrastructure.Persistence.Stores;

/// <summary>
/// 基本面指标存储。按（代码, 报告期）upsert，重复采集即覆盖（实施计划 §9.2）。
/// </summary>
/// <remarks>
/// 全市场快照（每代码一行）与单只历史序列（每代码多行）共用本表，
/// 因此「取最新一期」必须按代码取最大报告期，见 <see cref="GetLatestPerCodeAsync"/>。
/// </remarks>
public sealed class FundamentalStore(SaDbContext db) : IFundamentalStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<int> UpsertAsync(
        IReadOnlyList<FundamentalMetric> metrics,
        CancellationToken cancellationToken = default)
    {
        if (metrics.Count == 0)
        {
            return 0;
        }

        // 一次调用可能同时含多个代码（全市场分页）或多个报告期（单只历史），
        // 因此按代码分组逐组取既有行，避免一次 IN 查询把参数列表撑爆
        var written = 0;
        foreach (var group in metrics.GroupBy(metric => metric.Code, StringComparer.Ordinal))
        {
            var dates = group.Select(metric => metric.ReportDate).Distinct().ToList();

            var existing = await _db.FundamentalMetrics
                .Where(metric => metric.Code == group.Key && dates.Contains(metric.ReportDate))
                .ToDictionaryAsync(metric => metric.ReportDate, cancellationToken).ConfigureAwait(false);

            foreach (var metric in group)
            {
                if (existing.TryGetValue(metric.ReportDate, out var row))
                {
                    Copy(metric, row);
                }
                else
                {
                    await _db.FundamentalMetrics.AddAsync(metric, cancellationToken).ConfigureAwait(false);
                }

                written++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return written;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FundamentalMetric>> GetHistoryAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        await _db.FundamentalMetrics
            .AsNoTracking()
            .Where(metric => metric.Code == code)
            .OrderBy(metric => metric.ReportDate)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, FundamentalMetric>> GetLatestPerCodeAsync(
        CancellationToken cancellationToken = default)
    {
        // 先在库里按代码取最大报告期，再取这些行。
        // 直接在内存里 GroupBy 会把全表（万级）拉出来，而选股器每次筛选都要用。
        var latestDates = _db.FundamentalMetrics
            .GroupBy(metric => metric.Code)
            .Select(group => new { Code = group.Key, ReportDate = group.Max(metric => metric.ReportDate) });

        var rows = await _db.FundamentalMetrics
            .AsNoTracking()
            .Join(latestDates, metric => new { metric.Code, metric.ReportDate }, latest => latest, (metric, _) => metric)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows.ToDictionary(metric => metric.Code, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<FundamentalMetric>>> GetAnnualByCodeAsync(
        IReadOnlyCollection<string>? codes = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.FundamentalMetrics
            .AsNoTracking()
            .Where(metric => metric.ReportType == FundamentalReportTypes.Annual);

        if (codes is { Count: > 0 })
        {
            var wanted = codes.ToList();
            query = query.Where(metric => wanted.Contains(metric.Code));
        }

        var rows = await query
            .OrderBy(metric => metric.Code)
            .ThenBy(metric => metric.ReportDate)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows
            .GroupBy(metric => metric.Code, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<FundamentalMetric>)group.ToList(),
                StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public Task<bool> HasReportDateAsync(DateOnly reportDate, CancellationToken cancellationToken = default) =>
        _db.FundamentalMetrics.AnyAsync(metric => metric.ReportDate == reportDate, cancellationToken);

    /// <inheritdoc />
    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        _db.FundamentalMetrics.CountAsync(cancellationToken);

    /// <summary>把新数据覆盖到既有行上（新建实例会触发 EF 的跟踪冲突）。</summary>
    private static void Copy(FundamentalMetric source, FundamentalMetric target)
    {
        target.ReportType = source.ReportType;
        target.OrgType = source.OrgType;
        target.NoticeDate = source.NoticeDate;

        target.RoeWeighted = source.RoeWeighted;
        target.RoeDeducted = source.RoeDeducted;
        target.GrossMargin = source.GrossMargin;
        target.NetMargin = source.NetMargin;
        target.Roic = source.Roic;
        target.OperatingCashFlowToRevenue = source.OperatingCashFlowToRevenue;
        target.OperatingCashFlow = source.OperatingCashFlow;
        target.OperatingCashFlowToNetProfit = source.OperatingCashFlowToNetProfit;
        target.OperatingCashFlowToOperatingProfit = source.OperatingCashFlowToOperatingProfit;
        target.FreeCashFlow = source.FreeCashFlow;

        target.DebtRatio = source.DebtRatio;
        target.CurrentRatio = source.CurrentRatio;
        target.QuickRatio = source.QuickRatio;
        target.InterestDebtRatio = source.InterestDebtRatio;
        target.InterestCoverageRatio = source.InterestCoverageRatio;
        target.LiquidationRatio = source.LiquidationRatio;

        target.InventoryTurnoverDays = source.InventoryTurnoverDays;
        target.ReceivableTurnoverDays = source.ReceivableTurnoverDays;
        target.AssetTurnoverDays = source.AssetTurnoverDays;

        target.RevenueYoy = source.RevenueYoy;
        target.NetProfitYoy = source.NetProfitYoy;
        target.DeductedNetProfitYoy = source.DeductedNetProfitYoy;

        target.Eps = source.Eps;
        target.EpsDeducted = source.EpsDeducted;
        target.Bps = source.Bps;
        target.OperatingCashFlowPerShare = source.OperatingCashFlowPerShare;

        target.RndExpense = source.RndExpense;
        target.RndExpenseRatio = source.RndExpenseRatio;
        target.RndPersonnel = source.RndPersonnel;

        target.Revenue = source.Revenue;
        target.NetProfit = source.NetProfit;
        target.TotalAssets = source.TotalAssets;
        target.TotalEquity = source.TotalEquity;
        target.Liability = source.Liability;
        target.TotalShare = source.TotalShare;
        target.FreeShare = source.FreeShare;
        target.StaffNumber = source.StaffNumber;

        target.UpdatedAt = source.UpdatedAt;
    }
}
