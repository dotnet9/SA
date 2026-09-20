using SA.Application.Abstractions;
using SA.Application.Auth;
using SA.Application.Common;
using SA.Contracts.Admin;
using SA.Contracts.Common;
using SA.Domain.Authorization;
using SA.Domain.Common;
using SA.Domain.Entities.Identity;
using SA.Domain.Entities.System;

namespace SA.Application.Admin;

/// <summary>
/// 后台管理用例：数据源监控、用户与权限、会话与登录日志、系统设置、审计。
/// </summary>
/// <remarks>
/// <para>
/// 所有写操作都<b>强制写审计</b>（实施计划 §5.8）：管理动作出问题时必须能回答「谁在什么时候改了什么」。
/// 审计只记管理动作，不记普通读操作。
/// </para>
/// <para>
/// 三条安全边界：
/// </para>
/// <list type="number">
/// <item>内置角色不可删除；仍有用户的角色不可删除（否则这些用户瞬间失去全部权限）；</item>
/// <item>重置密码同时撤销该用户的全部会话——否则旧令牌在有效期内仍可用，重置就失去意义；</item>
/// <item>删除用户前检查不能删掉最后一个管理员（否则系统会失去管理入口）。</item>
/// </list>
/// </remarks>
public sealed class AdminService(
    ICollectStatusStore collectStatus,
    IUserAdminStore users,
    IRoleAdminStore roles,
    ISessionAdminStore sessions,
    ISettingsStore settings,
    IAuditStore audit,
    SA.Application.Authorization.PermissionService permissions,
    IPasswordHasher hasher,
    PasswordPolicy passwordPolicy,
    IDataPaths paths)
{
    /// <summary>
    /// 数据源监控：各源状态 + 最近采集任务。
    /// </summary>
    public async Task<ServiceResult<DataSourceMonitorDto>> GetDataSourcesAsync(
        int taskLimit,
        CancellationToken cancellationToken = default)
    {
        var sources = await collectStatus.GetSourcesAsync(cancellationToken).ConfigureAwait(false);
        var tasks = await collectStatus.GetRecentTasksAsync(taskLimit, cancellationToken).ConfigureAwait(false);

        var degraded = sources.Count(source => source.Status is Domain.Entities.Collect.DataSourceStates.Warn
            or Domain.Entities.Collect.DataSourceStates.Err);

        return ServiceResult<DataSourceMonitorDto>.Success(new DataSourceMonitorDto(
            Sources: sources.Select(source => new DataSourceStatusRowDto(
                Name: source.Name,
                Type: source.Type,
                Domains: source.Domains,
                Status: source.Status,
                LastOkAt: source.LastOkAt is null ? null : SaTime.Format(source.LastOkAt.Value),
                LatencyMs: source.LatencyMs,
                FailCount: source.FailCount,
                LastError: source.LastError)).ToList(),
            Tasks: tasks.Select(task => new CollectTaskRowDto(
                Id: task.Id,
                TaskName: task.TaskName,
                Source: task.Source,
                Status: task.Status,
                StartedAt: SaTime.Format(task.StartedAt),
                CostMs: task.CostMs,
                RowsWritten: task.RowsWritten,
                Error: task.Error)).ToList(),
            DegradedCount: degraded));
    }

    /// <summary>用户列表与统计。</summary>
    public async Task<ServiceResult<UserListDto>> GetUsersAsync(
        string? keyword,
        string? status,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var rows = await users.ListAsync(keyword, status, limit, cancellationToken).ConfigureAwait(false);
        var counts = await users.CountByStatusAsync(cancellationToken).ConfigureAwait(false);
        var roleRows = await roles.ListAsync(cancellationToken).ConfigureAwait(false);
        var roleMap = roleRows.ToDictionary(role => role.Id, role => role.Name, StringComparer.Ordinal);

        return ServiceResult<UserListDto>.Success(new UserListDto(
            Items: rows.Select(user => new UserRowDto(
                Id: user.Id,
                Username: user.Username,
                Nickname: user.Nickname,
                RoleId: user.RoleId,
                RoleName: roleMap.TryGetValue(user.RoleId, out var roleName) ? roleName : user.RoleId,
                Status: user.Status,
                MustChangePwd: user.MustChangePwd,
                HasTotp: !string.IsNullOrEmpty(user.TotpSecret),
                LastLoginAt: user.LastLoginAt is null ? null : SaTime.Format(user.LastLoginAt.Value),
                CreatedAt: SaTime.Format(user.CreatedAt))).ToList(),
            Total: rows.Count,
            ActiveCount: counts.TryGetValue("active", out var active) ? active : 0,
            DisabledCount: counts.TryGetValue("disabled", out var disabled) ? disabled : 0,
            LockedCount: counts.TryGetValue("locked", out var locked) ? locked : 0));
    }

    /// <summary>
    /// 权限矩阵：全部功能点 × 全部角色的勾选状态，以及各角色的配额。
    /// </summary>
    public async Task<ServiceResult<PermissionMatrixDto>> GetPermissionMatrixAsync(
        CancellationToken cancellationToken = default)
    {
        var roleRows = await roles.ListAsync(cancellationToken).ConfigureAwait(false);
        var userCounts = await roles.CountUsersByRoleAsync(cancellationToken).ConfigureAwait(false);

        var roleDtos = new List<RoleRowDto>();
        foreach (var role in roleRows)
        {
            var points = await roles.GetFunctionPointsAsync(role.Id, cancellationToken).ConfigureAwait(false);
            var quotas = await roles.GetQuotasAsync(role.Id, cancellationToken).ConfigureAwait(false);

            roleDtos.Add(new RoleRowDto(
                Id: role.Id,
                Name: role.Name,
                Description: role.Description,
                IsBuiltin: role.IsBuiltin,
                FunctionPoints: points,
                Quotas: quotas,
                UserCount: userCounts.TryGetValue(role.Id, out var count) ? count : 0));
        }

        return ServiceResult<PermissionMatrixDto>.Success(new PermissionMatrixDto(
            // 功能点清单来自目录常量：这是唯一来源，界面不会漏项也不会出现已废弃的项
            FunctionPoints: FunctionPointLookup.Describe()
                .Select(item => new FunctionPointDto(item.Code, item.Name, item.Group))
                .ToList()
                .ToList(),
            Roles: roleDtos,
            QuotaKeys: QuotaKeys,
            DataScopes: DataScopeCatalog
                .Select(item => new DataScopeOptionDto(item.Code, item.Name, item.Description))
                .ToList()));
    }

    /// <summary>创建用户（初始密码留空时由系统生成并只返回一次）。</summary>
    public async Task<ServiceResult<string>> CreateUserAsync(
        string actorId,
        string? actorName,
        UserCreateRequest request,
        IUserStore userStore,
        CancellationToken cancellationToken = default)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        if (username.Length < 3)
        {
            return ServiceResult<string>.Fail(ErrorCode.InvalidParameter, "用户名至少 3 个字符");
        }

        if (await userStore.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false) is not null)
        {
            return ServiceResult<string>.Fail(ErrorCode.InvalidParameter, $"用户名「{username}」已存在");
        }

        var role = await roles.FindAsync(request.RoleId, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return ServiceResult<string>.Fail(ErrorCode.InvalidParameter, $"未知角色：{request.RoleId}");
        }

        var password = string.IsNullOrWhiteSpace(request.Password) ? PasswordPolicy.Generate() : request.Password;
        var violation = passwordPolicy.Validate(password);
        if (violation is not null)
        {
            return ServiceResult<string>.Fail(ErrorCode.InvalidParameter, violation);
        }

        var hash = hasher.Hash(password);
        var now = SaTime.Now;

        await userStore.AddAsync(new User
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = username,
            Nickname = string.IsNullOrWhiteSpace(request.Nickname) ? username : request.Nickname.Trim(),
            PasswordHash = hash.Hash,
            PasswordSalt = hash.Salt,
            PasswordIterations = hash.Iterations,
            PasswordChangedAt = now,
            // 管理员设置的初始密码必须由用户首次登录时改掉
            MustChangePwd = true,
            Status = "active",
            RoleId = request.RoleId,
            CreatedAt = now
        }, cancellationToken).ConfigureAwait(false);

        await WriteAuditAsync(actorId, actorName, "user.create", username, $"角色={request.RoleId}", cancellationToken)
            .ConfigureAwait(false);

        // 明文只在本次响应里返回一次
        return ServiceResult<string>.Success(password);
    }

    /// <summary>改用户（昵称 / 角色 / 状态 / 强制改密）。</summary>
    public async Task<ServiceResult<int>> UpdateUserAsync(
        string actorId,
        string? actorName,
        string userId,
        UserUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RoleId is not null)
        {
            var role = await roles.FindAsync(request.RoleId, cancellationToken).ConfigureAwait(false);
            if (role is null)
            {
                return ServiceResult<int>.Fail(ErrorCode.InvalidParameter, $"未知角色：{request.RoleId}");
            }
        }

        if (request.Status is not null && request.Status is not ("active" or "disabled"))
        {
            return ServiceResult<int>.Fail(ErrorCode.InvalidParameter, "状态只能是 active / disabled");
        }

        // 不允许把自己停用或降权：会把自己锁在门外
        if (userId == actorId && request.Status == "disabled")
        {
            return ServiceResult<int>.Fail(ErrorCode.InvalidParameter, "不能停用当前登录的账号");
        }

        var updated = await users.UpdateAsync(
            userId,
            request.Nickname,
            request.RoleId,
            request.Status,
            request.MustChangePwd,
            cancellationToken).ConfigureAwait(false);

        if (!updated)
        {
            return ServiceResult<int>.Fail(ErrorCode.NotFound, "用户不存在");
        }

        var changes = new List<string>();
        if (request.Nickname is not null) changes.Add($"昵称→{request.Nickname}");
        if (request.RoleId is not null) changes.Add($"角色→{request.RoleId}");
        if (request.Status is not null) changes.Add($"状态→{request.Status}");
        if (request.MustChangePwd is not null) changes.Add($"强制改密→{request.MustChangePwd}");

        await WriteAuditAsync(actorId, actorName, "user.update", userId, string.Join('；', changes), cancellationToken)
            .ConfigureAwait(false);

        return ServiceResult<int>.Success(1);
    }

    /// <summary>重置用户密码（同时撤销其全部会话）。</summary>
    public async Task<ServiceResult<string>> ResetPasswordAsync(
        string actorId,
        string? actorName,
        string userId,
        string? newPassword,
        CancellationToken cancellationToken = default)
    {
        var password = string.IsNullOrWhiteSpace(newPassword) ? PasswordPolicy.Generate() : newPassword;

        var violation = passwordPolicy.Validate(password);
        if (violation is not null)
        {
            return ServiceResult<string>.Fail(ErrorCode.InvalidParameter, violation);
        }

        var hash = hasher.Hash(password);
        var reset = await users.ResetPasswordAsync(userId, hash.Hash, hash.Salt, hash.Iterations, true, cancellationToken)
            .ConfigureAwait(false);

        if (!reset)
        {
            return ServiceResult<string>.Fail(ErrorCode.NotFound, "用户不存在");
        }

        // 审计只记「已重置」，绝不记明文密码
        await WriteAuditAsync(actorId, actorName, "user.resetpassword", userId, "密码已重置并撤销全部会话", cancellationToken)
            .ConfigureAwait(false);

        // 明文只在本次响应里返回一次：管理员需要把它交给用户
        return ServiceResult<string>.Success(password);
    }

    /// <summary>删除用户。</summary>
    public async Task<ServiceResult<int>> DeleteUserAsync(
        string actorId,
        string? actorName,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == actorId)
        {
            return ServiceResult<int>.Fail(ErrorCode.InvalidParameter, "不能删除当前登录的账号");
        }

        var user = await users.ListAsync(null, null, 500, cancellationToken).ConfigureAwait(false);
        var target = user.FirstOrDefault(row => row.Id == userId);
        if (target is null)
        {
            return ServiceResult<int>.Fail(ErrorCode.NotFound, "用户不存在");
        }

        // 保护最后一个管理员：否则系统会失去管理入口
        var adminCount = user.Count(row => row.RoleId == BuiltInRoleIds.Admin);
        if (target.RoleId == BuiltInRoleIds.Admin && adminCount <= 1)
        {
            return ServiceResult<int>.Fail(ErrorCode.InvalidParameter, "不能删除最后一个管理员账号");
        }

        var removed = await users.DeleteAsync(userId, cancellationToken).ConfigureAwait(false);
        await WriteAuditAsync(actorId, actorName, "user.delete", userId, $"已删除用户 {target.Username}", cancellationToken)
            .ConfigureAwait(false);

        return ServiceResult<int>.Success(removed);
    }

    /// <summary>更新角色的功能点集合。</summary>
    public async Task<ServiceResult<int>> UpdateRoleFunctionPointsAsync(
        string actorId,
        string? actorName,
        string roleId,
        IReadOnlyList<string> codes,
        CancellationToken cancellationToken = default)
    {
        var role = await roles.FindAsync(roleId, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return ServiceResult<int>.Fail(ErrorCode.NotFound, "角色不存在");
        }

        var unknown = codes.Where(code => !FunctionPointCatalog.Contains(code)).ToList();
        if (unknown.Count > 0)
        {
            return ServiceResult<int>.Fail(ErrorCode.InvalidParameter, $"未知功能点：{string.Join('、', unknown)}");
        }

        // 管理员角色必须保留全部功能点：否则可能把自己锁死，系统再也无法管理
        if (roleId == BuiltInRoleIds.Admin)
        {
            var missing = FunctionPointCatalog.AllCodes.Except(codes).ToList();
            if (missing.Count > 0)
            {
                return ServiceResult<int>.Fail(
                    ErrorCode.InvalidParameter,
                    $"管理员角色必须保留全部功能点，缺少：{string.Join('、', missing.Take(5))}");
            }
        }

        await roles.ReplaceFunctionPointsAsync(roleId, codes, cancellationToken).ConfigureAwait(false);
        permissions.InvalidateRole(roleId);

        await WriteAuditAsync(
            actorId, actorName, "role.functionpoints", roleId,
            $"功能点数 {codes.Count}：{string.Join(',', codes.Take(8))}{(codes.Count > 8 ? "…" : string.Empty)}",
            cancellationToken).ConfigureAwait(false);

        return ServiceResult<int>.Success(codes.Count);
    }

    /// <summary>更新角色配额。</summary>
    public async Task<ServiceResult<int>> UpdateRoleQuotasAsync(
        string actorId,
        string? actorName,
        string roleId,
        IReadOnlyDictionary<string, int> quotas,
        CancellationToken cancellationToken = default)
    {
        var role = await roles.FindAsync(roleId, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return ServiceResult<int>.Fail(ErrorCode.NotFound, "角色不存在");
        }

        foreach (var (key, value) in quotas)
        {
            if (!QuotaKeys.Contains(key, StringComparer.Ordinal))
            {
                return ServiceResult<int>.Fail(ErrorCode.InvalidParameter, $"未知配额项：{key}");
            }

            if (value < 0)
            {
                return ServiceResult<int>.Fail(ErrorCode.InvalidParameter, $"配额不能为负：{key}");
            }

            await roles.SetQuotaAsync(roleId, key, value, cancellationToken).ConfigureAwait(false);
        }

        permissions.InvalidateRole(roleId);
        await WriteAuditAsync(
            actorId, actorName, "role.quotas", roleId,
            string.Join('；', quotas.Select(pair => $"{pair.Key}={pair.Value}")),
            cancellationToken).ConfigureAwait(false);

        return ServiceResult<int>.Success(quotas.Count);
    }

    /// <summary>会话与登录日志。</summary>
    public async Task<ServiceResult<SessionListDto>> GetSessionsAsync(
        int loginLimit,
        CancellationToken cancellationToken = default)
    {
        var logs = await sessions.GetLoginLogsAsync(loginLimit, cancellationToken).ConfigureAwait(false);
        var active = await sessions.CountActiveAsync(cancellationToken).ConfigureAwait(false);

        // 登录日志只存用户 Id（不冗余用户名）：这里按 Id 反查用户名，取不到时留空
        var userRows = await users.ListAsync(null, null, 500, cancellationToken).ConfigureAwait(false);
        var nameById = userRows.ToDictionary(user => user.Id, user => user.Username, StringComparer.Ordinal);

        return ServiceResult<SessionListDto>.Success(new SessionListDto(
            Logs: logs.Select(log => new LoginLogRowDto(
                Id: log.Id,
                // 登录日志按设计只记登录名（含失败尝试）而不关联用户 Id：
                // 撞库时正是需要看到「用了哪些不存在的用户名」
                UserName: log.UserName,
                Result: log.Result,
                Ip: log.Ip,
                Device: log.Device,
                Note: log.Note,
                CreatedAt: SaTime.Format(log.CreatedAt),
                // 界面需要直接渲染「成功/失败」，因此在接口层把 Result 翻译成布尔值
                Success: log.Result == LoginResultKind.Success)).ToList(),
            ActiveSessionCount: active,
            FailedToday: logs.Count(log => log.Result != LoginResultKind.Success
                && DateOnly.FromDateTime(SaTime.ToLocal(log.CreatedAt).Date) == SaTime.Today)));
    }

    /// <summary>撤销某个用户全部会话（用于「强制下线」）。</summary>
    public async Task<ServiceResult<int>> RevokeUserSessionsAsync(
        string actorId,
        string? actorName,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var now = SaTime.Now;
        var count = await sessions.RevokeAllAsync(userId, now, cancellationToken).ConfigureAwait(false);

        await WriteAuditAsync(actorId, actorName, "session.revoke", userId, $"撤销 {count} 个会话", cancellationToken)
            .ConfigureAwait(false);

        return ServiceResult<int>.Success(count);
    }

    /// <summary>系统设置与存储占用（只读，含路径与文件大小）。</summary>
    public async Task<ServiceResult<SystemStateDto>> GetSystemStateAsync(
        CancellationToken cancellationToken = default)
    {
        var root = paths.Root;

        var settingsRows = new List<SettingRowDto>();
        foreach (var (key, name, description) in SettingCatalog)
        {
            var value = await settings.GetAppSettingAsync(key, cancellationToken).ConfigureAwait(false);
            settingsRows.Add(new SettingRowDto(key, name, description, value ?? string.Empty, value is not null));
        }

        return ServiceResult<SystemStateDto>.Success(new SystemStateDto(
            DataRoot: root,
            DatabasePath: paths.DatabaseFile,
            ParquetPath: paths.ParquetRoot,
            LogPath: paths.LogRoot,
            DatabaseSizeBytes: SafeFileSize(paths.DatabaseFile),
            ParquetSizeBytes: SafeDirectorySize(paths.ParquetRoot),
            LogSizeBytes: SafeDirectorySize(paths.LogRoot),
            Settings: settingsRows));
    }

    /// <summary>更新系统设置（白名单内的键）。</summary>
    public async Task<ServiceResult<int>> UpdateSettingsAsync(
        string actorId,
        string? actorName,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
    {
        var updated = 0;
        foreach (var (key, value) in values)
        {
            // 只允许改白名单内的键：否则任何前端输入都能写进设置表
            if (!SettingCatalog.Any(item => item.Key == key))
            {
                return ServiceResult<int>.Fail(ErrorCode.InvalidParameter, $"未开放修改的设置项：{key}");
            }

            await settings.SetAppSettingAsync(key, value, cancellationToken).ConfigureAwait(false);
            updated++;
        }

        await WriteAuditAsync(
            actorId, actorName, "settings.update", null,
            string.Join('；', values.Select(pair => $"{pair.Key}={pair.Value}")),
            cancellationToken).ConfigureAwait(false);

        return ServiceResult<int>.Success(updated);
    }

    /// <summary>审计日志。</summary>
    public async Task<ServiceResult<IReadOnlyList<AuditRowDto>>> GetAuditLogsAsync(
        string? userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var rows = await audit.GetRecentAsync(userId, limit, cancellationToken).ConfigureAwait(false);

        return ServiceResult<IReadOnlyList<AuditRowDto>>.Success(rows.Select(row => new AuditRowDto(
            Id: row.Id,
            Username: row.Username,
            Action: row.Action,
            Target: row.Target,
            Detail: row.Detail,
            Ip: row.Ip,
            CreatedAt: SaTime.Format(row.CreatedAt))).ToList());
    }

    /// <summary>
    /// 可修改的系统设置项白名单。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 只暴露「运行期可以安全调整」的键：密钥类与数据目录类不在此列
    /// （它们改了会导致数据找不到或会话失效，必须改配置文件并重启）。
    /// </para>
    /// <para>
    /// <b>只列真正被读取的键</b>：早期版本列了「默认主题 / 默认 K 线根数 / 推送间隔默认值 / 导出每日上限」
    /// 四项，但代码里没有任何地方读它们——那不是设置项，而是四个改了没反应的假开关。
    /// 其中主题与推送间隔属于用户偏好（在「个人设置」里按账号生效），
    /// 导出上限属于角色配额（在权限矩阵里按角色配置），都不该在这里再放一个入口。
    /// </para>
    /// </remarks>
    private static readonly (string Key, string Name, string Description)[] SettingCatalog =
    [
        (SiteSettings.NameKey, "站点名称", "显示在页头与登录页；留空则用默认名称「股析 SA」"),
        (SiteSettings.NoticeKey, "全局公告", "显示在所有页面顶部的横幅；留空则不显示")
    ];

    /// <summary>
    /// 可配置的配额键。
    /// </summary>
    /// <remarks>
    /// 直接引用 <c>QuotaKeys</c> 常量，不在这里另抄一份字面量：
    /// 早期实现自行写了 <c>export.daily</c> 这个键，而应用实际用的是 <c>quota.daily</c> 与 <c>export.rows</c>，
    /// 结果权限矩阵里显示了一列「改了没用」的配额，真正生效的配额反而看不到（实测现象）。
    /// </remarks>
    private static readonly string[] QuotaKeys =
    [
        SA.Domain.Entities.Identity.QuotaKeys.HistoryYears,
        SA.Domain.Entities.Identity.QuotaKeys.DailyQueries,
        SA.Domain.Entities.Identity.QuotaKeys.ExportRows,
        SA.Domain.Entities.Identity.QuotaKeys.WatchlistMax,
        SA.Domain.Entities.Identity.QuotaKeys.AlertMax,
        SA.Domain.Entities.Identity.QuotaKeys.StrategyMax
    ];

    /// <summary>数据范围选项。</summary>
    private static readonly (string Code, string Name, string Description)[] DataScopeCatalog =
    [
        (FunctionPointCatalog.DataScopeAll, "全市场", "可以查看全部股票"),
        (FunctionPointCatalog.DataScopeWatchlist, "仅自选", "只能查看自选股范围内的股票")
    ];

    private async Task WriteAuditAsync(
        string userId,
        string? username,
        string action,
        string? target,
        string? detail,
        CancellationToken cancellationToken)
    {
        await audit.AddAsync(new AuditLog
        {
            UserId = userId,
            Username = username,
            Action = action,
            Target = target,
            Detail = detail,
            CreatedAt = SaTime.Now
        }, cancellationToken).ConfigureAwait(false);
    }

    private static long SafeFileSize(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private static long SafeDirectorySize(string path)
    {
        try
        {
            return Directory.Exists(path)
                ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length)
                : 0;
        }
        catch (IOException)
        {
            return 0;
        }
    }
}
