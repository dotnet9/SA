using Microsoft.EntityFrameworkCore;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Collect;
using SA.Domain.Entities.Market;

namespace SA.Infrastructure.Persistence.Stores;

/// <summary>
/// 证券基础信息存储。按代码 upsert，天然幂等（实施计划 §9.2）。
/// </summary>
public sealed class InstrumentStore(SaDbContext db) : IInstrumentStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Instrument>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.Instruments.AsNoTracking().OrderBy(i => i.Code).ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, Instrument>> GetByCodesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken = default)
    {
        var wanted = codes.ToList();
        var rows = await _db.Instruments
            .AsNoTracking()
            .Where(i => wanted.Contains(i.Code))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows.ToDictionary(i => i.Code, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public Task<Instrument?> FindAsync(string code, CancellationToken cancellationToken = default) =>
        _db.Instruments.AsNoTracking().FirstOrDefaultAsync(i => i.Code == code, cancellationToken);

    /// <inheritdoc />
    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        _db.Instruments.CountAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<int> UpsertAsync(IReadOnlyList<Instrument> instruments, CancellationToken cancellationToken = default)
    {
        if (instruments.Count == 0)
        {
            return 0;
        }

        var codes = instruments.Select(i => i.Code).ToList();
        var existing = await _db.Instruments
            .Where(i => codes.Contains(i.Code))
            .ToDictionaryAsync(i => i.Code, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);

        var written = 0;
        foreach (var instrument in instruments)
        {
            if (existing.TryGetValue(instrument.Code, out var row))
            {
                row.Name = instrument.Name;
                row.Market = instrument.Market;
                row.Board = instrument.Board;
                row.Industry = instrument.Industry;
                row.IsSt = instrument.IsSt;
                row.UpdatedOn = instrument.UpdatedOn;

                // 拼音一旦算出来就不必重算，只有新代码或改名时才覆盖
                if (!string.IsNullOrEmpty(instrument.Pinyin))
                {
                    row.Pinyin = instrument.Pinyin;
                }
            }
            else
            {
                await _db.Instruments.AddAsync(instrument, cancellationToken).ConfigureAwait(false);
            }

            written++;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return written;
    }

    /// <inheritdoc />
    public async Task<int> UpdatePinyinAsync(
        IReadOnlyDictionary<string, string> pinyinByCode,
        CancellationToken cancellationToken = default)
    {
        if (pinyinByCode.Count == 0)
        {
            return 0;
        }

        var codes = pinyinByCode.Keys.ToList();
        var rows = await _db.Instruments
            .Where(i => codes.Contains(i.Code))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var updated = 0;
        foreach (var row in rows)
        {
            if (pinyinByCode.TryGetValue(row.Code, out var pinyin) && row.Pinyin != pinyin)
            {
                row.Pinyin = pinyin;
                updated++;
            }
        }

        if (updated > 0)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return updated;
    }
}

/// <summary>
/// 个股行情快照存储。整体替换语义：一轮扫描的结果即为库内全量。
/// </summary>
public sealed class QuoteSnapshotStore(SaDbContext db) : IQuoteSnapshotStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuoteSnapshot>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.QuoteSnapshots.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, QuoteSnapshot>> GetByCodesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken = default)
    {
        var wanted = codes.ToList();
        var rows = await _db.QuoteSnapshots
            .AsNoTracking()
            .Where(s => wanted.Contains(s.Code))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows.ToDictionary(s => s.Code, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        _db.QuoteSnapshots.CountAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastUpdatedAtAsync(CancellationToken cancellationToken = default)
    {
        // 单表全量替换，取任意一行的写入时间即可代表整体新鲜度
        var row = await _db.QuoteSnapshots
            .AsNoTracking()
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => (DateTimeOffset?)s.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return row;
    }

    /// <inheritdoc />
    public async Task<int> ReplaceAllAsync(
        IReadOnlyList<QuoteSnapshot> snapshots,
        CancellationToken cancellationToken = default)
    {
        // 先清后写：不做逐条 diff，理由是「整体替换」语义最不容易出现残留旧行；
        // 全市场一轮约 6,000 行，放在一个事务里 SQLite 可在百毫秒级完成。
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await _db.QuoteSnapshots.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _db.QuoteSnapshots.AddRangeAsync(snapshots, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return snapshots.Count;
    }
}

/// <summary>
/// 行业板块快照存储。
/// </summary>
public sealed class SectorStore(SaDbContext db) : ISectorStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Sector>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.Sectors.AsNoTracking().OrderByDescending(s => s.Pct).ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> ReplaceAllAsync(IReadOnlyList<Sector> sectors, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await _db.Sectors.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _db.Sectors.AddRangeAsync(sectors, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return sectors.Count;
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastUpdatedAtAsync(CancellationToken cancellationToken = default) =>
        await _db.Sectors
            .AsNoTracking()
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => (DateTimeOffset?)s.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// 指数快照存储。
/// </summary>
public sealed class IndexStore(SaDbContext db) : IIndexStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<IndexQuote>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.IndexQuotes.AsNoTracking().OrderBy(i => i.SortOrder).ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> ReplaceAllAsync(IReadOnlyList<IndexQuote> indices, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await _db.IndexQuotes.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _db.IndexQuotes.AddRangeAsync(indices, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return indices.Count;
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastUpdatedAtAsync(CancellationToken cancellationToken = default) =>
        await _db.IndexQuotes
            .AsNoTracking()
            .OrderByDescending(i => i.UpdatedAt)
            .Select(i => (DateTimeOffset?)i.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// 采集监控存储：数据源状态与任务日志。
/// </summary>
public sealed class CollectStatusStore(SaDbContext db) : ICollectStatusStore
{
    /// <summary>滚动窗口内保留的任务日志条数上限，避免单表无限增长。</summary>
    private const int MaxTaskLogs = 2000;

    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DataSourceStatus>> GetSourcesAsync(CancellationToken cancellationToken = default) =>
        await _db.DataSourceStatuses.AsNoTracking().OrderBy(s => s.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task RecordSourceAsync(
        string source,
        string domains,
        string type,
        bool ok,
        long latencyMs,
        string? error,
        int degradeAfterFailures,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.DataSourceStatuses.FirstOrDefaultAsync(s => s.Name == source, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            row = new DataSourceStatus
            {
                Name = source,
                Type = type,
                Domains = domains,
                Status = DataSourceStates.Idle
            };
            await _db.DataSourceStatuses.AddAsync(row, cancellationToken).ConfigureAwait(false);
        }

        row.Domains = domains;
        row.Type = type;
        row.UpdatedAt = SaTime.Now;

        if (ok)
        {
            row.Status = DataSourceStates.Ok;
            row.LastOkAt = SaTime.Now;
            row.FailCount = 0;
            row.LastError = null;
        }
        else
        {
            row.FailCount++;
            row.LastError = error is null ? null : (error.Length <= 500 ? error : error[..500]);
            row.Status = row.FailCount >= degradeAfterFailures ? DataSourceStates.Err : DataSourceStates.Warn;
        }

        row.LatencyMs = (int)Math.Min(int.MaxValue, latencyMs);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> AddTaskLogAsync(CollectTaskLog log, CancellationToken cancellationToken = default)
    {
        await _db.CollectTaskLogs.AddAsync(log, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 保留窗口：超出上限时删掉最旧的记录（界面只展示最近若干条）
        var total = await _db.CollectTaskLogs.CountAsync(cancellationToken).ConfigureAwait(false);
        if (total > MaxTaskLogs)
        {
            var cutoff = await _db.CollectTaskLogs
                .OrderByDescending(l => l.Id)
                .Skip(MaxTaskLogs)
                .Select(l => l.Id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (cutoff > 0)
            {
                await _db.CollectTaskLogs.Where(l => l.Id <= cutoff).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return log.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CollectTaskLog>> GetRecentTasksAsync(
        int take,
        CancellationToken cancellationToken = default) =>
        await _db.CollectTaskLogs
            .AsNoTracking()
            .OrderByDescending(l => l.Id)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// 交易日历存储。
/// </summary>
public sealed class TradingCalendarStore(SaDbContext db) : ITradingCalendarStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<TradingDay>> GetRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default) =>
        await _db.TradingDays
            .AsNoTracking()
            .Where(d => d.Date >= from && d.Date <= to)
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> UpsertAsync(IReadOnlyList<TradingDay> days, CancellationToken cancellationToken = default)
    {
        if (days.Count == 0)
        {
            return 0;
        }

        var dates = days.Select(d => d.Date).ToList();
        var existing = await _db.TradingDays
            .Where(d => dates.Contains(d.Date))
            .ToDictionaryAsync(d => d.Date, cancellationToken).ConfigureAwait(false);

        foreach (var day in days)
        {
            if (existing.TryGetValue(day.Date, out var row))
            {
                row.IsOpen = day.IsOpen;
            }
            else
            {
                await _db.TradingDays.AddAsync(day, cancellationToken).ConfigureAwait(false);
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return days.Count;
    }

    /// <inheritdoc />
    public async Task<DateOnly?> GetLastDateAsync(CancellationToken cancellationToken = default)
    {
        var row = await _db.TradingDays
            .AsNoTracking()
            .OrderByDescending(d => d.Date)
            .Select(d => (DateOnly?)d.Date)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return row;
    }
}

/// <summary>
/// 市场级日度统计存储。字段级合并写入，见 <see cref="IMarketStatStore.UpsertAsync"/>。
/// </summary>
public sealed class MarketStatStore(SaDbContext db) : IMarketStatStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public Task<MarketStat?> FindAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        _db.MarketStats.FirstOrDefaultAsync(s => s.Date == date, cancellationToken);

    /// <inheritdoc />
    public Task<MarketStat?> GetLatestAsync(CancellationToken cancellationToken = default) =>
        _db.MarketStats.AsNoTracking().OrderByDescending(s => s.Date).FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task UpsertAsync(MarketStatPatch patch, CancellationToken cancellationToken = default)
    {
        var row = await _db.MarketStats.FirstOrDefaultAsync(s => s.Date == patch.Date, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            row = new MarketStat { Date = patch.Date };
            await _db.MarketStats.AddAsync(row, cancellationToken).ConfigureAwait(false);
        }

        if (patch.LimitUp is not null)
        {
            row.LimitUp = patch.LimitUp.Value;
        }

        if (patch.LimitDown is not null)
        {
            row.LimitDown = patch.LimitDown.Value;
        }

        if (patch.FundFlow is { } flow)
        {
            row.MainNet = flow.MainNet;
            row.SuperLarge = flow.SuperLarge;
            row.Large = flow.Large;
            row.Medium = flow.Medium;
            row.Small = flow.Small;
            row.FundFlowDate = patch.FundFlowDate ?? flow.Date;
        }

        if (patch.Margin is { } margin)
        {
            row.FinanceBalance = margin.FinanceBalance;
            row.LoanBalance = margin.LoanBalance;
            row.MarginDate = patch.MarginDate ?? margin.Date;
        }

        row.UpdatedAt = SaTime.Now;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
