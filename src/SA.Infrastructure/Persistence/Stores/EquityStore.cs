using Microsoft.EntityFrameworkCore;
using SA.Application.Abstractions;
using SA.Domain.Entities.Equity;

namespace SA.Infrastructure.Persistence.Stores;

/// <summary>
/// 股权结构存储。
/// </summary>
public sealed class EquityStore(SaDbContext db) : IEquityStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<TopHolder>> GetTopHoldersAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        await _db.TopHolders
            .AsNoTracking()
            .Where(holder => holder.Code == code)
            .OrderByDescending(holder => holder.EndDate)
            .ThenBy(holder => holder.IsFreeFloat)
            .ThenBy(holder => holder.Rank)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<HolderCount>> GetHolderCountsAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        await _db.HolderCounts
            .AsNoTracking()
            .Where(count => count.Code == code)
            .OrderBy(count => count.EndDate)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<PledgeStat?> GetPledgeAsync(string code, CancellationToken cancellationToken = default) =>
        _db.PledgeStats
            .AsNoTracking()
            .Where(pledge => pledge.Code == code)
            .OrderByDescending(pledge => pledge.TradeDate)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<int> UpsertTopHoldersAsync(
        IReadOnlyList<TopHolder> holders,
        CancellationToken cancellationToken = default)
    {
        if (holders.Count == 0)
        {
            return 0;
        }

        var code = holders[0].Code;
        var dates = holders.Select(holder => holder.EndDate).Distinct().ToList();

        var existing = await _db.TopHolders
            .Where(holder => holder.Code == code && dates.Contains(holder.EndDate))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var map = existing.ToDictionary(holder => (holder.EndDate, holder.Rank, holder.IsFreeFloat));

        foreach (var holder in holders)
        {
            if (map.TryGetValue((holder.EndDate, holder.Rank, holder.IsFreeFloat), out var row))
            {
                row.HolderName = holder.HolderName;
                row.HoldNum = holder.HoldNum;
                row.HoldRatio = holder.HoldRatio;
                row.FreeHoldRatio = holder.FreeHoldRatio;
                row.HoldChange = holder.HoldChange;
                row.HolderType = holder.HolderType;
                row.SharesType = holder.SharesType;
                row.MarketCap = holder.MarketCap;
                row.NoticeDate = holder.NoticeDate;
                row.UpdatedAt = holder.UpdatedAt;
            }
            else
            {
                await _db.TopHolders.AddAsync(holder, cancellationToken).ConfigureAwait(false);
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return holders.Count;
    }

    /// <inheritdoc />
    public async Task<int> UpsertHolderCountsAsync(
        IReadOnlyList<HolderCount> counts,
        CancellationToken cancellationToken = default)
    {
        if (counts.Count == 0)
        {
            return 0;
        }

        var code = counts[0].Code;
        var dates = counts.Select(count => count.EndDate).Distinct().ToList();

        var existing = await _db.HolderCounts
            .Where(count => count.Code == code && dates.Contains(count.EndDate))
            .ToDictionaryAsync(count => count.EndDate, cancellationToken).ConfigureAwait(false);

        foreach (var count in counts)
        {
            if (existing.TryGetValue(count.EndDate, out var row))
            {
                row.HolderNum = count.HolderNum;
                row.PreviousHolderNum = count.PreviousHolderNum;
                row.HolderNumChange = count.HolderNumChange;
                row.HolderNumRatio = count.HolderNumRatio;
                row.AvgHoldNum = count.AvgHoldNum;
                row.AvgMarketCap = count.AvgMarketCap;
                row.TotalMarketCap = count.TotalMarketCap;
                row.TotalShares = count.TotalShares;
                row.ChangeReason = count.ChangeReason;
                row.ReportName = count.ReportName;
                row.NoticeDate = count.NoticeDate;
                row.UpdatedAt = count.UpdatedAt;
            }
            else
            {
                await _db.HolderCounts.AddAsync(count, cancellationToken).ConfigureAwait(false);
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return counts.Count;
    }

    /// <inheritdoc />
    public async Task<int> UpsertPledgeAsync(PledgeStat pledge, CancellationToken cancellationToken = default)
    {
        var row = await _db.PledgeStats
            .FirstOrDefaultAsync(
                existing => existing.Code == pledge.Code && existing.TradeDate == pledge.TradeDate,
                cancellationToken).ConfigureAwait(false);

        if (row is null)
        {
            await _db.PledgeStats.AddAsync(pledge, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            row.PledgeRatio = pledge.PledgeRatio;
            row.PledgeSharesWan = pledge.PledgeSharesWan;
            row.PledgeDealNum = pledge.PledgeDealNum;
            row.PledgeMarketCapWan = pledge.PledgeMarketCapWan;
            row.Industry = pledge.Industry;
            row.Year1ChangePercent = pledge.Year1ChangePercent;
            row.UpdatedAt = pledge.UpdatedAt;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return 1;
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastUpdatedAtAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var fromHolders = await _db.TopHolders
            .AsNoTracking()
            .Where(holder => holder.Code == code)
            .Select(holder => (DateTimeOffset?)holder.UpdatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var fromCounts = await _db.HolderCounts
            .AsNoTracking()
            .Where(count => count.Code == code)
            .Select(count => (DateTimeOffset?)count.UpdatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var all = fromHolders.Concat(fromCounts).Where(value => value is not null).Select(value => value!.Value).ToList();
        return all.Count == 0 ? null : all.Max();
    }
}
