using Microsoft.EntityFrameworkCore;
using SA.Application.Abstractions;
using SA.Domain.Entities.Research;
using SA.Infrastructure.Persistence;

namespace SA.Infrastructure.Persistence.Stores;

/// <summary>
/// 个股研究数据存储：主营概况 / 主营构成 / 股本变动 / 限售解禁 / 公告 / 研报。
/// </summary>
/// <remarks>
/// 写入策略分两类，差别有实测依据：
/// <list type="bullet">
/// <item><b>按主键 upsert</b>（构成、股本变动、公告、研报）：重跑幂等，重复采集即覆盖。</item>
/// <item><b>整体替换</b>（限售解禁）：上游 <c>xsjj</c> 给的是「当前剩余的待解禁」，
/// 已过期的会被上游移除；若 upsert，历史解禁会一直留着被当成未来待解禁。</item>
/// </list>
/// </remarks>
public sealed class ResearchStore(SaDbContext db) : IResearchStore
{
    private readonly SaDbContext _db = db;

    /* ------------------------------------------------------------------
       主营概况
       ------------------------------------------------------------------ */

    /// <inheritdoc />
    public async Task<int> UpsertProfileAsync(BusinessProfile profile, CancellationToken cancellationToken = default)
    {
        var existing = await _db.BusinessProfiles
            .FirstOrDefaultAsync(row => row.Code == profile.Code, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            await _db.BusinessProfiles.AddAsync(profile, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            existing.BusinessScope = profile.BusinessScope;
            existing.BusinessReview = profile.BusinessReview;
            existing.UpdatedAt = profile.UpdatedAt;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return 1;
    }

    /// <inheritdoc />
    public Task<BusinessProfile?> GetProfileAsync(string code, CancellationToken cancellationToken = default) =>
        _db.BusinessProfiles.AsNoTracking().FirstOrDefaultAsync(row => row.Code == code, cancellationToken);

    /* ------------------------------------------------------------------
       主营构成
       ------------------------------------------------------------------ */

    /// <inheritdoc />
    public async Task<int> UpsertCompositionsAsync(
        IReadOnlyList<BusinessComposition> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return 0;
        }

        var code = items[0].Code;
        var existing = await _db.BusinessCompositions
            .Where(row => row.Code == code)
            .ToDictionaryAsync(
                row => (row.ReportDate, row.MainOpType, row.ItemName),
                cancellationToken).ConfigureAwait(false);

        var written = 0;
        foreach (var item in items)
        {
            if (existing.TryGetValue((item.ReportDate, item.MainOpType, item.ItemName), out var row))
            {
                Copy(item, row);
            }
            else
            {
                await _db.BusinessCompositions.AddAsync(item, cancellationToken).ConfigureAwait(false);
            }

            written++;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return written;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BusinessComposition>> GetCompositionsAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        await _db.BusinessCompositions
            .AsNoTracking()
            .Where(row => row.Code == code)
            .OrderByDescending(row => row.ReportDate)
            .ThenBy(row => row.MainOpType)
            .ThenBy(row => row.Rank)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /* ------------------------------------------------------------------
       股本变动
       ------------------------------------------------------------------ */

    /// <inheritdoc />
    public async Task<int> UpsertShareChangesAsync(
        IReadOnlyList<ShareChange> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
        {
            return 0;
        }

        var code = rows[0].Code;
        var existing = await _db.ShareChanges
            .Where(row => row.Code == code)
            .ToDictionaryAsync(row => row.EndDate, cancellationToken).ConfigureAwait(false);

        var written = 0;
        foreach (var row in rows)
        {
            if (existing.TryGetValue(row.EndDate, out var current))
            {
                current.TotalShares = row.TotalShares;
                current.LimitedShares = row.LimitedShares;
                current.UnlimitedShares = row.UnlimitedShares;
                current.ListedAShares = row.ListedAShares;
                current.ChangeReason = row.ChangeReason;
                current.UpdatedAt = row.UpdatedAt;
            }
            else
            {
                await _db.ShareChanges.AddAsync(row, cancellationToken).ConfigureAwait(false);
            }

            written++;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return written;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ShareChange>> GetShareChangesAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        await _db.ShareChanges
            .AsNoTracking()
            .Where(row => row.Code == code)
            .OrderByDescending(row => row.EndDate)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /* ------------------------------------------------------------------
       限售解禁（整体替换）
       ------------------------------------------------------------------ */

    /// <inheritdoc />
    public async Task<int> ReplaceUpcomingUnlocksAsync(
        string code,
        IReadOnlyList<UpcomingUnlock> rows,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.UpcomingUnlocks
            .Where(row => row.Code == code)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        _db.UpcomingUnlocks.RemoveRange(existing);

        if (rows.Count > 0)
        {
            await _db.UpcomingUnlocks.AddRangeAsync(rows, cancellationToken).ConfigureAwait(false);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rows.Count;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UpcomingUnlock>> GetUpcomingUnlocksAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        await _db.UpcomingUnlocks
            .AsNoTracking()
            .Where(row => row.Code == code)
            .OrderBy(row => row.LiftDate)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /* ------------------------------------------------------------------
       公告
       ------------------------------------------------------------------ */

    /// <inheritdoc />
    public async Task<int> UpsertAnnouncementsAsync(
        IReadOnlyList<Announcement> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
        {
            return 0;
        }

        var codes = rows.Select(row => row.ArtCode).Distinct().ToList();
        var existing = await _db.Announcements
            .Where(row => codes.Contains(row.ArtCode))
            .ToDictionaryAsync(row => row.ArtCode, cancellationToken).ConfigureAwait(false);

        var written = 0;
        foreach (var row in rows)
        {
            if (existing.TryGetValue(row.ArtCode, out var current))
            {
                // 公告可能被更正：标题与类型都可能变，全部覆盖
                current.Title = row.Title;
                current.NoticeDate = row.NoticeDate;
                current.ColumnName = row.ColumnName;
                current.ColumnNames = row.ColumnNames;
                current.AnnType = row.AnnType;
                current.SourceType = row.SourceType;
                current.Url = row.Url;
                current.UpdatedAt = row.UpdatedAt;
            }
            else
            {
                await _db.Announcements.AddAsync(row, cancellationToken).ConfigureAwait(false);
            }

            written++;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return written;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Announcement>> GetAnnouncementsAsync(
        string code,
        string? columnName = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Announcements.AsNoTracking().Where(row => row.Code == code);

        if (!string.IsNullOrWhiteSpace(columnName))
        {
            query = query.Where(row => row.ColumnName == columnName);
        }

        return await query
            .OrderByDescending(row => row.NoticeDate)
            .ThenByDescending(row => row.ArtCode)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, int>> GetAnnouncementTypesAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.Announcements
            .AsNoTracking()
            .Where(row => row.Code == code && row.ColumnName != null)
            .GroupBy(row => row.ColumnName!)
            .Select(group => new { Name = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows
            .OrderByDescending(row => row.Count)
            .ThenBy(row => row.Name, StringComparer.Ordinal)
            .ToDictionary(row => row.Name, row => row.Count, StringComparer.Ordinal);
    }

    /* ------------------------------------------------------------------
       研报
       ------------------------------------------------------------------ */

    /// <inheritdoc />
    public async Task<int> UpsertReportsAsync(
        IReadOnlyList<ResearchReport> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
        {
            return 0;
        }

        var infoCodes = rows.Select(row => row.InfoCode).Distinct().ToList();
        var existing = await _db.ResearchReports
            .Where(row => infoCodes.Contains(row.InfoCode))
            .ToDictionaryAsync(row => row.InfoCode, cancellationToken).ConfigureAwait(false);

        var written = 0;
        foreach (var row in rows)
        {
            if (existing.TryGetValue(row.InfoCode, out var current))
            {
                Copy(row, current);
            }
            else
            {
                await _db.ResearchReports.AddAsync(row, cancellationToken).ConfigureAwait(false);
            }

            written++;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return written;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ResearchReport>> GetReportsAsync(
        string code,
        int limit = 50,
        CancellationToken cancellationToken = default) =>
        await _db.ResearchReports
            .AsNoTracking()
            .Where(row => row.Code == code)
            .OrderByDescending(row => row.PublishDate)
            .ThenByDescending(row => row.InfoCode)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastUpdatedAtAsync(string code, CancellationToken cancellationToken = default)
    {
        // 取四张表里最新的一个写入时间：任一有数据就说明这只标的采集过
        var times = new List<DateTimeOffset>();

        var profile = await _db.BusinessProfiles.AsNoTracking()
            .Where(row => row.Code == code)
            .Select(row => (DateTimeOffset?)row.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (profile is not null)
        {
            times.Add(profile.Value);
        }

        foreach (var time in new[]
                 {
                     await _db.ShareChanges.AsNoTracking().Where(row => row.Code == code)
                         .OrderByDescending(row => row.UpdatedAt).Select(row => (DateTimeOffset?)row.UpdatedAt)
                         .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false),
                     await _db.Announcements.AsNoTracking().Where(row => row.Code == code)
                         .OrderByDescending(row => row.UpdatedAt).Select(row => (DateTimeOffset?)row.UpdatedAt)
                         .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false),
                     await _db.ResearchReports.AsNoTracking().Where(row => row.Code == code)
                         .OrderByDescending(row => row.UpdatedAt).Select(row => (DateTimeOffset?)row.UpdatedAt)
                         .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
                 })
        {
            if (time is not null)
            {
                times.Add(time.Value);
            }
        }

        return times.Count == 0 ? null : times.Max();
    }

    /// <summary>把新数据覆盖到既有行上（不新建实例，保持 EF 跟踪关系）。</summary>
    private static void Copy(BusinessComposition source, BusinessComposition target)
    {
        target.Rank = source.Rank;
        target.Income = source.Income;
        target.IncomeRatio = source.IncomeRatio;
        target.Cost = source.Cost;
        target.CostRatio = source.CostRatio;
        target.Profit = source.Profit;
        target.ProfitRatio = source.ProfitRatio;
        target.GrossProfitRatio = source.GrossProfitRatio;
        target.IsSubItem = source.IsSubItem;
        target.UpdatedAt = source.UpdatedAt;
    }

    private static void Copy(ResearchReport source, ResearchReport target)
    {
        target.Title = source.Title;
        target.OrgName = source.OrgName;
        target.OrgShortName = source.OrgShortName;
        target.Researcher = source.Researcher;
        target.PublishDate = source.PublishDate;
        target.RatingName = source.RatingName;
        target.RatingChange = source.RatingChange;
        target.IndustryName = source.IndustryName;
        target.PredictThisYearEps = source.PredictThisYearEps;
        target.PredictThisYearPe = source.PredictThisYearPe;
        target.PredictNextYearEps = source.PredictNextYearEps;
        target.PredictNextYearPe = source.PredictNextYearPe;
        target.PredictNextTwoYearEps = source.PredictNextTwoYearEps;
        target.PredictNextTwoYearPe = source.PredictNextTwoYearPe;
        target.EncodeUrl = source.EncodeUrl;
        target.Url = source.Url;
        target.UpdatedAt = source.UpdatedAt;
    }
}
