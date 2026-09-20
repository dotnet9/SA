using Microsoft.EntityFrameworkCore;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Identity;
using SA.Domain.Entities.System;

namespace SA.Infrastructure.Persistence.Stores;

/// <summary>
/// 用户与密码历史存储。
/// </summary>
public sealed class UserStore(SaDbContext db) : IUserStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        return _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == normalized, cancellationToken);
    }

    /// <inheritdoc />
    public Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    /// <inheritdoc />
    public void Update(User user) => _db.Users.Update(user);

    /// <inheritdoc />
    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await _db.Users.AddAsync(user, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task AddPasswordHistoryAsync(
        string userId,
        string hash,
        string salt,
        int iterations,
        CancellationToken cancellationToken = default)
    {
        await _db.PasswordHistories.AddAsync(
            new PasswordHistory
            {
                UserId = userId,
                PasswordHash = hash,
                PasswordSalt = salt,
                PasswordIterations = iterations,
                CreatedAt = SaTime.Now
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PasswordHistoryEntry>> GetRecentPasswordsAsync(
        string userId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.PasswordHistories
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .ThenByDescending(h => h.Id)
            .Take(take)
            .Select(h => new { h.PasswordHash, h.PasswordSalt, h.PasswordIterations })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows
            .Select(r => new PasswordHistoryEntry(r.PasswordHash, r.PasswordSalt, r.PasswordIterations))
            .ToList();
    }

    /// <inheritdoc />
    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        _db.Users.AnyAsync(cancellationToken);
}

/// <summary>
/// 角色、功能点授权与操作级参数存储。
/// </summary>
public sealed class RoleStore(SaDbContext db) : IRoleStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public Task<Role?> FindAsync(string roleId, CancellationToken cancellationToken = default) =>
        _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetFunctionPointsAsync(string roleId, CancellationToken cancellationToken = default) =>
        await _db.RoleFunctionPoints
            .Where(f => f.RoleId == roleId)
            .Select(f => f.FunctionPointCode)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, int>> GetQuotasAsync(string roleId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.RoleQuotas
            .Where(q => q.RoleId == roleId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows.ToDictionary(q => q.QuotaKey, q => q.QuotaValue, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.Roles.OrderBy(r => r.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task AddAsync(Role role, CancellationToken cancellationToken = default) =>
        await _db.Roles.AddAsync(role, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task ReplaceFunctionPointsAsync(
        string roleId,
        IEnumerable<string> codes,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.RoleFunctionPoints
            .Where(f => f.RoleId == roleId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        _db.RoleFunctionPoints.RemoveRange(existing);
        await _db.RoleFunctionPoints.AddRangeAsync(
            codes.Distinct(StringComparer.Ordinal).Select(c => new RoleFunctionPoint { RoleId = roleId, FunctionPointCode = c }),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// 会话、登录日志与配额用量存储。
/// </summary>
public sealed class SessionStore(SaDbContext db) : ISessionStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        await _db.RefreshTokens.AddAsync(token, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<RefreshToken?> FindRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RefreshToken>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var now = SaTime.Now;
        return await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void RevokeRefreshToken(RefreshToken token, DateTimeOffset revokedAt)
    {
        token.RevokedAt = revokedAt;
        _db.RefreshTokens.Update(token);
    }

    /// <inheritdoc />
    public async Task<int> RevokeAllRefreshTokensAsync(
        string userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default)
    {
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var token in active)
        {
            token.RevokedAt = revokedAt;
        }

        return active.Count;
    }

    /// <inheritdoc />
    public async Task AddLoginLogAsync(LoginLog log, CancellationToken cancellationToken = default) =>
        await _db.LoginLogs.AddAsync(log, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> GetQuotaUsageAsync(string userId, string day, string kind, CancellationToken cancellationToken = default)
    {
        var usage = await _db.QuotaUsages
            .FirstOrDefaultAsync(q => q.UserId == userId && q.Day == day && q.Kind == kind, cancellationToken)
            .ConfigureAwait(false);

        return usage?.Used ?? 0;
    }

    /// <inheritdoc />
    public async Task<int> IncrementQuotaUsageAsync(
        string userId,
        string day,
        string kind,
        int delta,
        CancellationToken cancellationToken = default)
    {
        var usage = await _db.QuotaUsages
            .FirstOrDefaultAsync(q => q.UserId == userId && q.Day == day && q.Kind == kind, cancellationToken)
            .ConfigureAwait(false);

        if (usage is null)
        {
            usage = new QuotaUsage { UserId = userId, Day = day, Kind = kind, Used = delta };
            await _db.QuotaUsages.AddAsync(usage, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            usage.Used += delta;
            _db.QuotaUsages.Update(usage);
        }

        // 配额必须立即可见（并发登录/查询时若靠调用方提交，可能被重复放行）
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return usage.Used;
    }
}

/// <summary>
/// 系统级与个人设置存储。
/// </summary>
public sealed class SettingsStore(SaDbContext db) : ISettingsStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<string?> GetAppSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        var row = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken).ConfigureAwait(false);
        return row?.Value;
    }

    /// <inheritdoc />
    public async Task SetAppSettingAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var row = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            await _db.AppSettings.AddAsync(
                new AppSetting { Key = key, Value = value, UpdatedAt = SaTime.Now },
                cancellationToken).ConfigureAwait(false);
            return;
        }

        row.Value = value;
        row.UpdatedAt = SaTime.Now;
        _db.AppSettings.Update(row);
    }

    /// <inheritdoc />
    public async Task<string?> GetUserSettingsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var row = await _db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken).ConfigureAwait(false);
        return row?.Json;
    }

    /// <inheritdoc />
    public async Task SetUserSettingsAsync(string userId, string json, CancellationToken cancellationToken = default)
    {
        var row = await _db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            await _db.UserSettings.AddAsync(
                new UserSetting { UserId = userId, Json = json, UpdatedAt = SaTime.Now },
                cancellationToken).ConfigureAwait(false);
            return;
        }

        row.Json = json;
        row.UpdatedAt = SaTime.Now;
        _db.UserSettings.Update(row);
    }
}

/// <summary>
/// 工作单元：一次用例内的所有写入统一提交。
/// </summary>
public sealed class UnitOfWork(SaDbContext db) : IUnitOfWork
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
