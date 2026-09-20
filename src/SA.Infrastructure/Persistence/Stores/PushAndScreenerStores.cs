using Microsoft.EntityFrameworkCore;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Alerts;
using SA.Domain.Entities.Screener;
using SA.Domain.Entities.System;

namespace SA.Infrastructure.Persistence.Stores;

/// <summary>
/// Web Push 订阅存储。按端点去重（端点由浏览器生成且全局唯一）。
/// </summary>
public sealed class PushSubscriptionStore(SaDbContext db) : IPushSubscriptionStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PushSubscription>> GetByUserAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        await _db.PushSubscriptions
            .AsNoTracking()
            .Where(row => row.UserId == userId)
            .OrderByDescending(row => row.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PushSubscription>> GetEnabledAsync(CancellationToken cancellationToken = default) =>
        await _db.PushSubscriptions
            .AsNoTracking()
            .Where(row => row.Enabled)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<long> UpsertAsync(PushSubscription subscription, CancellationToken cancellationToken = default)
    {
        var row = await _db.PushSubscriptions
            .FirstOrDefaultAsync(existing => existing.Endpoint == subscription.Endpoint, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            await _db.PushSubscriptions.AddAsync(subscription, cancellationToken).ConfigureAwait(false);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return subscription.Id;
        }

        // 重新订阅说明该端点仍然有效：换绑用户（同一浏览器换账号登录）并重置失败状态
        row.UserId = subscription.UserId;
        row.P256dh = subscription.P256dh;
        row.Auth = subscription.Auth;
        row.UserAgent = subscription.UserAgent;
        row.Enabled = true;
        row.FailCount = 0;
        row.LastError = null;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row.Id;
    }

    /// <inheritdoc />
    public async Task<int> RemoveByEndpointAsync(string endpoint, CancellationToken cancellationToken = default) =>
        await _db.PushSubscriptions
            .Where(row => row.Endpoint == endpoint)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task RecordResultAsync(
        long subscriptionId,
        bool ok,
        string? error,
        int disableAfterFailures,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.PushSubscriptions
            .FirstOrDefaultAsync(existing => existing.Id == subscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return;
        }

        if (ok)
        {
            row.FailCount = 0;
            row.LastOkAt = SaTime.Now;
            row.LastError = null;
        }
        else
        {
            row.FailCount++;
            row.LastError = error is null ? null : (error.Length <= 500 ? error : error[..500]);

            // 连续失败到阈值即停用：每轮都为死端点付一次请求是纯粹的浪费
            if (row.FailCount >= disableAfterFailures)
            {
                row.Enabled = false;
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// 选股器执行记录存储（筛选日志 + 我的策略共用一张表）。
/// </summary>
public sealed class ScreenerRunStore(SaDbContext db) : IScreenerRunStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScreenerRun>> GetRecentAsync(
        string userId,
        bool strategiesOnly,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ScreenerRuns.AsNoTracking().Where(row => row.UserId == userId);
        if (strategiesOnly)
        {
            query = query.Where(row => row.Name != null);
        }

        return await query
            .OrderByDescending(row => row.Id)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<ScreenerRun?> FindAsync(string userId, long id, CancellationToken cancellationToken = default) =>
        _db.ScreenerRuns.FirstOrDefaultAsync(row => row.UserId == userId && row.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<ScreenerRun?> FindByNameAsync(string userId, string name, CancellationToken cancellationToken = default) =>
        _db.ScreenerRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.UserId == userId && row.Name == name, cancellationToken);

    /// <inheritdoc />
    public async Task<long> AddAsync(ScreenerRun run, CancellationToken cancellationToken = default)
    {
        await _db.ScreenerRuns.AddAsync(run, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return run.Id;
    }

    /// <inheritdoc />
    public async Task<bool> RenameAsync(
        string userId,
        long id,
        string? name,
        CancellationToken cancellationToken = default)
    {
        var row = await FindAsync(userId, id, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return false;
        }

        row.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> DeleteAsync(string userId, long id, CancellationToken cancellationToken = default) =>
        await _db.ScreenerRuns
            .Where(row => row.UserId == userId && row.Id == id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> TrimAsync(
        string userId,
        int keepNonStrategy,
        CancellationToken cancellationToken = default)
    {
        // 只裁剪未保存为策略的记录：策略是用户显式保存的，不能被自动清理掉
        var cutoff = await _db.ScreenerRuns
            .Where(row => row.UserId == userId && row.Name == null)
            .OrderByDescending(row => row.Id)
            .Skip(Math.Max(1, keepNonStrategy))
            .Select(row => (long?)row.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (cutoff is null)
        {
            return 0;
        }

        return await _db.ScreenerRuns
            .Where(row => row.UserId == userId && row.Name == null && row.Id <= cutoff.Value)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<int> CountStrategiesAsync(string userId, CancellationToken cancellationToken = default) =>
        _db.ScreenerRuns.CountAsync(row => row.UserId == userId && row.Name != null, cancellationToken);
}

/// <summary>
/// 导出记录存储。
/// </summary>
public sealed class ExportLogStore(SaDbContext db) : IExportLogStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task AddAsync(ExportLog log, CancellationToken cancellationToken = default)
    {
        await _db.ExportLogs.AddAsync(log, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExportLog>> GetRecentAsync(
        string userId,
        int limit,
        CancellationToken cancellationToken = default) =>
        await _db.ExportLogs
            .AsNoTracking()
            .Where(row => row.UserId == userId)
            .OrderByDescending(row => row.Id)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> CountTodayAsync(string userId, CancellationToken cancellationToken = default)
    {
        // 业务日是 Asia/Shanghai 的日期，不是 UTC：否则晚上 8 点后导出的记录会被算到第二天
        var start = SaTime.Today.ToDateTime(TimeOnly.MinValue);
        var startOffset = new DateTimeOffset(start, SaTime.Zone.GetUtcOffset(start));

        return await _db.ExportLogs
            .AsNoTracking()
            .CountAsync(row => row.UserId == userId && row.CreatedAt >= startOffset, cancellationToken)
            .ConfigureAwait(false);
    }
}
