using Microsoft.EntityFrameworkCore;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Capital;

namespace SA.Infrastructure.Persistence.Stores;

/// <summary>
/// 资金面数据存储。
/// </summary>
/// <remarks>
/// 大宗交易采用「整体替换」：同一天可以有多笔、且买方卖方可能完全相同（无法用业务字段做幂等键），
/// 整体替换是这里最简单也最不容易出错的做法（与行情快照同一思路）。
/// </remarks>
public sealed class CapitalStore(SaDbContext db) : ICapitalStore
{
    /// <summary>保留的资金流窗口（与采集窗口一致，避免表无限增长）。</summary>
    private const int FundFlowRetain = 120;

    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<FundFlowDaily>> GetFundFlowAsync(
        string code,
        int days,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.FundFlows
            .AsNoTracking()
            .Where(row => row.Code == code)
            .OrderByDescending(row => row.Date)
            .Take(Math.Clamp(days, 1, FundFlowRetain))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        rows.Reverse();
        return rows;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BillboardRecord>> GetBillboardsAsync(
        string code,
        int limit,
        CancellationToken cancellationToken = default) =>
        await _db.Billboards
            .AsNoTracking()
            .Where(row => row.Code == code)
            .OrderByDescending(row => row.TradeDate)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<BlockTrade>> GetBlockTradesAsync(
        string code,
        int limit,
        CancellationToken cancellationToken = default) =>
        await _db.BlockTrades
            .AsNoTracking()
            .Where(row => row.Code == code)
            .OrderByDescending(row => row.TradeDate)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<MarginDetail>> GetMarginDetailsAsync(
        string code,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.MarginDetails
            .AsNoTracking()
            .Where(row => row.Code == code)
            .OrderByDescending(row => row.Date)
            .Take(Math.Clamp(limit, 1, 60))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        rows.Reverse();
        return rows;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NorthboundHolding>> GetNorthboundAsync(
        string code,
        int limit,
        CancellationToken cancellationToken = default) =>
        await _db.NorthboundHoldings
            .AsNoTracking()
            .Where(row => row.Code == code)
            .OrderByDescending(row => row.HoldDate)
            .Take(Math.Clamp(limit, 1, 20))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> UpsertFundFlowAsync(
        IReadOnlyList<FundFlowDaily> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
        {
            return 0;
        }

        var code = rows[0].Code;
        var dates = rows.Select(row => row.Date).Distinct().ToList();

        var existing = await _db.FundFlows
            .Where(row => row.Code == code && dates.Contains(row.Date))
            .ToDictionaryAsync(row => row.Date, cancellationToken).ConfigureAwait(false);

        foreach (var row in rows)
        {
            if (existing.TryGetValue(row.Date, out var target))
            {
                target.MainNet = row.MainNet;
                target.SuperLargeNet = row.SuperLargeNet;
                target.LargeNet = row.LargeNet;
                target.MediumNet = row.MediumNet;
                target.SmallNet = row.SmallNet;
                target.MainRatio = row.MainRatio;
                target.Close = row.Close;
                target.ChangePercent = row.ChangePercent;
                target.UpdatedAt = row.UpdatedAt;
            }
            else
            {
                await _db.FundFlows.AddAsync(row, cancellationToken).ConfigureAwait(false);
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 保留窗口：超出的旧行直接删除，避免逐日数据无限累积
        var stale = await _db.FundFlows
            .Where(row => row.Code == code)
            .OrderByDescending(row => row.Date)
            .Skip(FundFlowRetain)
            .Select(row => new { row.Code, row.Date })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (stale.Count > 0)
        {
            var staleDates = stale.Select(item => item.Date).ToList();
            await _db.FundFlows
                .Where(row => row.Code == code && staleDates.Contains(row.Date))
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        }

        return rows.Count;
    }

    /// <inheritdoc />
    public async Task<int> UpsertBillboardsAsync(
        string code,
        IReadOnlyList<BillboardRecord> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
        {
            return 0;
        }

        var dates = rows.Select(row => row.TradeDate).Distinct().ToList();
        var existing = await _db.Billboards
            .Where(row => row.Code == code && dates.Contains(row.TradeDate))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // 同一天可能因为不同原因多次上榜，按「日期 + 原因」匹配
        var map = existing.ToDictionary(row => (row.TradeDate, row.Reason ?? string.Empty));

        foreach (var row in rows)
        {
            if (map.TryGetValue((row.TradeDate, row.Reason ?? string.Empty), out var target))
            {
                target.Explain = row.Explain;
                target.Close = row.Close;
                target.ChangePercent = row.ChangePercent;
                target.TurnoverRate = row.TurnoverRate;
                target.NetAmount = row.NetAmount;
                target.BuyAmount = row.BuyAmount;
                target.SellAmount = row.SellAmount;
                target.DealAmount = row.DealAmount;
                // 后续涨跌幅会随交易日推进被回填，因此每次采集都覆盖
                target.Next1Change = row.Next1Change;
                target.Next5Change = row.Next5Change;
                target.Next10Change = row.Next10Change;
                target.UpdatedAt = row.UpdatedAt;
            }
            else
            {
                await _db.Billboards.AddAsync(row, cancellationToken).ConfigureAwait(false);
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rows.Count;
    }

    /// <inheritdoc />
    public async Task<int> ReplaceBlockTradesAsync(
        string code,
        IReadOnlyList<BlockTrade> rows,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await _db.BlockTrades.Where(row => row.Code == code).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        if (rows.Count > 0)
        {
            await _db.BlockTrades.AddRangeAsync(rows, cancellationToken).ConfigureAwait(false);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return rows.Count;
    }

    /// <inheritdoc />
    public async Task<int> UpsertMarginDetailsAsync(
        IReadOnlyList<MarginDetail> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
        {
            return 0;
        }

        var code = rows[0].Code;
        var dates = rows.Select(row => row.Date).Distinct().ToList();

        var existing = await _db.MarginDetails
            .Where(row => row.Code == code && dates.Contains(row.Date))
            .ToDictionaryAsync(row => row.Date, cancellationToken).ConfigureAwait(false);

        foreach (var row in rows)
        {
            if (existing.TryGetValue(row.Date, out var target))
            {
                target.FinanceBalance = row.FinanceBalance;
                target.FinanceBuy = row.FinanceBuy;
                target.FinanceNetBuy = row.FinanceNetBuy;
                target.LoanBalance = row.LoanBalance;
                target.LoanVolume = row.LoanVolume;
                target.TotalBalance = row.TotalBalance;
                target.FinanceBalanceRatio = row.FinanceBalanceRatio;
                target.Close = row.Close;
                target.ChangePercent = row.ChangePercent;
                target.UpdatedAt = row.UpdatedAt;
            }
            else
            {
                await _db.MarginDetails.AddAsync(row, cancellationToken).ConfigureAwait(false);
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rows.Count;
    }

    /// <inheritdoc />
    public async Task<int> UpsertNorthboundAsync(
        IReadOnlyList<NorthboundHolding> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
        {
            return 0;
        }

        var code = rows[0].Code;
        var dates = rows.Select(row => row.HoldDate).Distinct().ToList();

        var existing = await _db.NorthboundHoldings
            .Where(row => row.Code == code && dates.Contains(row.HoldDate))
            .ToDictionaryAsync(row => row.HoldDate, cancellationToken).ConfigureAwait(false);

        foreach (var row in rows)
        {
            if (existing.TryGetValue(row.HoldDate, out var target))
            {
                target.DateType = row.DateType;
                target.HoldShares = row.HoldShares;
                target.PreviousHoldShares = row.PreviousHoldShares;
                target.AddShares = row.AddShares;
                target.AddSharesAmp = row.AddSharesAmp;
                target.HoldMarketCap = row.HoldMarketCap;
                target.OrgQuantity = row.OrgQuantity;
                target.PreviousOrgQuantity = row.PreviousOrgQuantity;
                target.FreeSharesRatio = row.FreeSharesRatio;
                target.TotalSharesRatio = row.TotalSharesRatio;
                target.Industry = row.Industry;
                target.UpdatedAt = row.UpdatedAt;
            }
            else
            {
                await _db.NorthboundHoldings.AddAsync(row, cancellationToken).ConfigureAwait(false);
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rows.Count;
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastUpdatedAtAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var fromFlow = await _db.FundFlows
            .AsNoTracking()
            .Where(row => row.Code == code)
            .Select(row => (DateTimeOffset?)row.UpdatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var fromMargin = await _db.MarginDetails
            .AsNoTracking()
            .Where(row => row.Code == code)
            .Select(row => (DateTimeOffset?)row.UpdatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var all = fromFlow.Concat(fromMargin).Where(value => value is not null).Select(value => value!.Value).ToList();
        return all.Count == 0 ? null : all.Max();
    }
}
