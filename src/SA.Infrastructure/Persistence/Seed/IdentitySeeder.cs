using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Application.Auth;
using SA.Domain.Authorization;
using SA.Domain.Common;
using SA.Domain.Entities.Identity;
using SA.Infrastructure.Security;

namespace SA.Infrastructure.Persistence.Seed;

/// <summary>
/// 身份与授权播种：同步功能点字典、建立内置角色、创建初始管理员。
/// </summary>
public sealed class IdentitySeeder(
    SaDbContext db,
    IUserStore users,
    IRoleStore roles,
    IPasswordHasher hasher,
    AuthOptions options,
    ILogger<IdentitySeeder> logger)
{
    private readonly SaDbContext _db = db;
    private readonly IUserStore _users = users;
    private readonly IRoleStore _roles = roles;
    private readonly IPasswordHasher _hasher = hasher;
    private readonly AuthOptions _options = options;
    private readonly ILogger<IdentitySeeder> _logger = logger;

    /// <summary>
    /// 执行全部播种步骤。幂等：重复启动不会重复插入或覆盖用户改动。
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SyncFunctionPointsAsync(cancellationToken).ConfigureAwait(false);
        await EnsureBuiltInRolesAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAdminAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 把代码中的功能点目录同步进 <c>FunctionPoint</c> 表（供后台矩阵展示与引用完整性）。
    /// 授权判定始终以代码目录为准，改库不能提权。
    /// </summary>
    private async Task SyncFunctionPointsAsync(CancellationToken cancellationToken)
    {
        var desired = FunctionPointCatalog.Groups
            .SelectMany(g => g.Items.Select(i => (Group: g.Name, Item: i)))
            .ToDictionary(x => x.Item.Code, x => (x.Group, x.Item), StringComparer.Ordinal);

        var existing = await _db.FunctionPoints.ToDictionaryAsync(f => f.Code, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);

        var added = 0;
        var updated = 0;
        foreach (var (code, value) in desired)
        {
            if (existing.TryGetValue(code, out var row))
            {
                if (row.GroupName != value.Group || row.Name != value.Item.Name || row.Description != value.Item.Description)
                {
                    row.GroupName = value.Group;
                    row.Name = value.Item.Name;
                    row.Description = value.Item.Description;
                    updated++;
                }
            }
            else
            {
                await _db.FunctionPoints.AddAsync(
                    new FunctionPointRow
                    {
                        Code = code,
                        GroupName = value.Group,
                        Name = value.Item.Name,
                        Description = value.Item.Description
                    },
                    cancellationToken).ConfigureAwait(false);
                added++;
            }
        }

        // 目录中已移除的编码：只在没有任何角色引用时才清理，避免破坏历史授权
        var obsolete = existing.Keys.Except(desired.Keys, StringComparer.Ordinal).ToList();
        var removed = 0;
        foreach (var code in obsolete)
        {
            var referenced = await _db.RoleFunctionPoints.AnyAsync(f => f.FunctionPointCode == code, cancellationToken).ConfigureAwait(false);
            if (referenced)
            {
                _logger.LogWarning("功能点 {Code} 已从代码目录移除，但仍被角色引用，已保留待人工处理", code);
                continue;
            }

            _db.FunctionPoints.Remove(existing[code]);
            removed++;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "功能点同步完成：新增 {Added}，更新 {Updated}，清理 {Removed}，当前 {Total} 项",
            added, updated, removed, desired.Count);
    }

    /// <summary>
    /// 确保三个内置角色存在，并补齐其功能点与操作级参数。
    /// </summary>
    private async Task EnsureBuiltInRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var preset in BuiltInRoles.All)
        {
            var role = await _roles.FindAsync(preset.Id, cancellationToken).ConfigureAwait(false);
            if (role is null)
            {
                await _roles.AddAsync(
                    new Role
                    {
                        Id = preset.Id,
                        Name = preset.Name,
                        Description = preset.Description,
                        IsBuiltin = true,
                        CreatedAt = SaTime.Now
                    },
                    cancellationToken).ConfigureAwait(false);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await _roles.ReplaceFunctionPointsAsync(preset.Id, preset.FunctionPoints, cancellationToken).ConfigureAwait(false);
                await EnsureRoleQuotasAsync(preset.Id, BuiltInRoleQuotas.For(preset.Id), cancellationToken).ConfigureAwait(false);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("已创建内置角色 {Role}（{Count} 个功能点）", preset.Name, preset.FunctionPoints.Count);
                continue;
            }

            // 管理员始终对齐完整目录：代码新增功能点后无需人工补授权，否则系统会自行锁死
            if (role.Id == BuiltInRoleIds.Admin)
            {
                var current = await _roles.GetFunctionPointsAsync(role.Id, cancellationToken).ConfigureAwait(false);
                var missing = FunctionPointCatalog.AllCodes.Except(current, StringComparer.Ordinal).ToList();
                if (missing.Count > 0)
                {
                    await _roles.ReplaceFunctionPointsAsync(role.Id, FunctionPointCatalog.AllCodes, cancellationToken).ConfigureAwait(false);
                    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("管理员角色补齐新增功能点 {Count} 个", missing.Count);
                }
            }
        }
    }

    /// <summary>
    /// 写入角色默认操作级参数（仅缺失的键，不覆盖后台调整过的值）。
    /// </summary>
    private async Task EnsureRoleQuotasAsync(
        string roleId,
        IReadOnlyDictionary<string, int> defaults,
        CancellationToken cancellationToken)
    {
        var existing = await _db.RoleQuotas
            .Where(q => q.RoleId == roleId)
            .Select(q => q.QuotaKey)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var toAdd = defaults
            .Where(kv => !existing.Contains(kv.Key, StringComparer.Ordinal))
            .Select(kv => new RoleQuota { RoleId = roleId, QuotaKey = kv.Key, QuotaValue = kv.Value })
            .ToList();

        if (toAdd.Count > 0)
        {
            await _db.RoleQuotas.AddRangeAsync(toAdd, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 首次启动创建管理员账号。初始密码取配置；未配置则随机生成并**只打印一次**到日志。
    /// </summary>
    private async Task EnsureAdminAsync(CancellationToken cancellationToken)
    {
        if (await _users.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var generated = string.IsNullOrWhiteSpace(_options.AdminInitialPassword);
        var password = generated ? GenerateInitialPassword() : _options.AdminInitialPassword!;
        var result = _hasher.Hash(password);

        var admin = new User
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = "admin",
            Nickname = "管理员",
            PasswordHash = result.Hash,
            PasswordSalt = result.Salt,
            PasswordIterations = result.Iterations,
            PasswordChangedAt = SaTime.Now,
            MustChangePwd = true,
            Status = UserStatus.Active,
            RoleId = BuiltInRoleIds.Admin,
            CreatedAt = SaTime.Now
        };

        await _users.AddAsync(admin, cancellationToken).ConfigureAwait(false);
        await _users.AddPasswordHistoryAsync(admin.Id, result.Hash, result.Salt, result.Iterations, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (generated)
        {
            // 刻意只打印一次：日志留存 7 天，登录后应立即改密
            _logger.LogWarning("已创建初始管理员 admin，随机初始密码为：{Password}（请立即登录并修改）", password);
        }
        else
        {
            _logger.LogInformation("已创建初始管理员 admin（初始密码来自配置 Sa:Auth:AdminInitialPassword）");
        }
    }

    /// <summary>
    /// 生成满足密码策略的随机初始密码（大小写 + 数字 + 符号，长度 16）。
    /// </summary>
    private static string GenerateInitialPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*";
        const string all = upper + lower + digits + symbols;

        var buffer = new char[16];
        buffer[0] = upper[Random.Shared.Next(upper.Length)];
        buffer[1] = lower[Random.Shared.Next(lower.Length)];
        buffer[2] = digits[Random.Shared.Next(digits.Length)];
        buffer[3] = symbols[Random.Shared.Next(symbols.Length)];
        for (var i = 4; i < buffer.Length; i++)
        {
            buffer[i] = all[Random.Shared.Next(all.Length)];
        }

        // 打散固定位置，避免前四位永远是「大写+小写+数字+符号」
        for (var i = buffer.Length - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
        }

        return new string(buffer);
    }
}
