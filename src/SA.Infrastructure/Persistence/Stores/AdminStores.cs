using Microsoft.EntityFrameworkCore;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Identity;
using SA.Domain.Entities.System;

namespace SA.Infrastructure.Persistence.Stores;

/// <summary>
/// 用户管理存储（后台）。
/// </summary>
public sealed class UserAdminStore(SaDbContext db) : IUserAdminStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<User>> ListAsync(
        string? keyword,
        string? status,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = keyword.Trim();
            query = query.Where(user => user.Username.Contains(pattern) || user.Nickname.Contains(pattern));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(user => user.Status == status);
        }

        return await query
            .OrderBy(user => user.Username)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(CancellationToken cancellationToken = default)
    {
        var groups = await _db.Users
            .AsNoTracking()
            .GroupBy(user => user.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return groups.ToDictionary(item => item.Status, item => item.Count, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(
        string userId,
        string? nickname,
        string? roleId,
        string? status,
        bool? mustChangePwd,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(row => row.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(nickname))
        {
            user.Nickname = nickname.Trim();
        }

        if (!string.IsNullOrWhiteSpace(roleId))
        {
            user.RoleId = roleId;
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            user.Status = status;
        }

        if (mustChangePwd is not null)
        {
            user.MustChangePwd = mustChangePwd.Value;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ResetPasswordAsync(
        string userId,
        string hash,
        string salt,
        int iterations,
        bool mustChangePwd,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(row => row.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return false;
        }

        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.PasswordIterations = iterations;
        user.PasswordChangedAt = SaTime.Now;
        user.MustChangePwd = mustChangePwd;
        // 重置密码同时清零失败计数并解锁：否则用户拿到新密码仍然被锁在门外
        user.FailCount = 0;
        user.LockedUntil = null;

        // 重置密码必须同时撤销会话：否则旧令牌在有效期内仍可用，重置就失去意义
        var now = SaTime.Now;
        var sessions = await _db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var session in sessions)
        {
            session.RevokedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _db.PasswordHistories.AddAsync(new PasswordHistory
        {
            UserId = userId,
            PasswordHash = hash,
            PasswordSalt = salt,
            PasswordIterations = iterations,
            CreatedAt = now
        }, cancellationToken).ConfigureAwait(false);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        var removed = await _db.Users
            .Where(user => user.Id == userId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        return removed;
    }
}

/// <summary>
/// 角色管理存储（后台）。
/// </summary>
public sealed class RoleAdminStore(SaDbContext db) : IRoleAdminStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken = default) =>
        await _db.Roles
            .AsNoTracking()
            .OrderByDescending(role => role.IsBuiltin)
            .ThenBy(role => role.Id, StringComparer.Ordinal)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<Role?> FindAsync(string roleId, CancellationToken cancellationToken = default) =>
        _db.Roles.FirstOrDefaultAsync(role => role.Id == roleId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetFunctionPointsAsync(
        string roleId,
        CancellationToken cancellationToken = default) =>
        await _db.RoleFunctionPoints
            .AsNoTracking()
            .Where(row => row.RoleId == roleId)
            .Select(row => row.FunctionPointCode)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, int>> GetQuotasAsync(
        string roleId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.RoleQuotas
            .AsNoTracking()
            .Where(row => row.RoleId == roleId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows.ToDictionary(row => row.QuotaKey, row => row.QuotaValue, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task ReplaceFunctionPointsAsync(
        string roleId,
        IEnumerable<string> codes,
        CancellationToken cancellationToken = default)
    {
        var wanted = codes.Distinct(StringComparer.Ordinal).ToList();

        await _db.RoleFunctionPoints
            .Where(row => row.RoleId == roleId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        await _db.RoleFunctionPoints.AddRangeAsync(
            wanted.Select(code => new RoleFunctionPoint { RoleId = roleId, FunctionPointCode = code }),
            cancellationToken).ConfigureAwait(false);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, int>> CountUsersByRoleAsync(CancellationToken cancellationToken = default)
    {
        var groups = await _db.Users
            .AsNoTracking()
            .GroupBy(user => user.RoleId)
            .Select(group => new { RoleId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return groups.ToDictionary(item => item.RoleId, item => item.Count, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task SetQuotaAsync(
        string roleId,
        string quotaKey,
        int value,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.RoleQuotas
            .FirstOrDefaultAsync(quota => quota.RoleId == roleId && quota.QuotaKey == quotaKey, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            await _db.RoleQuotas.AddAsync(new RoleQuota
            {
                RoleId = roleId,
                QuotaKey = quotaKey,
                QuotaValue = value
            }, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            row.QuotaValue = value;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(
        string roleId,
        string? name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(row => row.Id == roleId, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            role.Name = name.Trim();
        }

        if (description is not null)
        {
            role.Description = description.Trim();
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> DeleteAsync(string roleId, CancellationToken cancellationToken = default)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(row => row.Id == roleId, cancellationToken).ConfigureAwait(false);

        // 内置角色不允许删除：它们的功能点集合是系统的语义基础
        if (role is null || role.IsBuiltin)
        {
            return 0;
        }

        // 仍有用户使用的角色不允许删除：否则这些用户会瞬间失去全部权限
        var inUse = await _db.Users.AnyAsync(user => user.RoleId == roleId, cancellationToken).ConfigureAwait(false);
        if (inUse)
        {
            return 0;
        }

        await _db.RoleFunctionPoints.Where(row => row.RoleId == roleId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _db.RoleQuotas.Where(row => row.RoleId == roleId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _db.Roles.Where(row => row.Id == roleId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        return 1;
    }
}

/// <summary>
/// 会话与登录日志存储（后台）。
/// </summary>
public sealed class SessionAdminStore(SaDbContext db) : ISessionAdminStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<LoginLog>> GetLoginLogsAsync(
        int limit,
        CancellationToken cancellationToken = default) =>
        await _db.LoginLogs
            .AsNoTracking()
            .OrderByDescending(log => log.CreatedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> RevokeAsync(
        string tokenHash,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default)
    {
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(row => row.TokenHash == tokenHash && row.RevokedAt == null, cancellationToken)
            .ConfigureAwait(false);

        if (token is null)
        {
            return 0;
        }

        token.RevokedAt = revokedAt;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return 1;
    }

    /// <inheritdoc />
    public async Task<int> RevokeAllAsync(
        string userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default) =>
        await _db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setter => setter.SetProperty(token => token.RevokedAt, revokedAt),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> CountActiveAsync(CancellationToken cancellationToken = default) =>
        await _db.RefreshTokens
            .AsNoTracking()
            .CountAsync(token => token.RevokedAt == null && token.ExpiresAt > SaTime.Now, cancellationToken)
            .ConfigureAwait(false);
}

/// <summary>
/// 审计日志存储。
/// </summary>
public sealed class AuditStore(SaDbContext db) : IAuditStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public async Task AddAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        await _db.AuditLogs.AddAsync(log, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(
        string? userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(log => log.UserId == userId);
        }

        return await query
            .OrderByDescending(log => log.Id)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
