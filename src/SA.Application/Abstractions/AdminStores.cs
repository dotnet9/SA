using SA.Domain.Entities.Identity;

namespace SA.Application.Abstractions;

/// <summary>
/// 用户管理（后台）。只服务于后台接口：列表、改角色、启停、重置密码。
/// </summary>
/// <remarks>
/// 与 <see cref="IUserStore"/> 分开：后者是登录链路需要的「按名/按 Id 取用户」，
/// 这里则是管理链路需要的「列出、修改、停用」。分开后登录链路不必依赖任何写能力。
/// </remarks>
public interface IUserAdminStore
{
    /// <summary>列出用户（可按关键词与状态过滤）。</summary>
    Task<IReadOnlyList<User>> ListAsync(
        string? keyword,
        string? status,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>用户总数（按状态分组），用于后台概览。</summary>
    Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>修改用户的基本信息（昵称、角色、状态、是否强制改密）。</summary>
    Task<bool> UpdateAsync(
        string userId,
        string? nickname,
        string? roleId,
        string? status,
        bool? mustChangePwd,
        CancellationToken cancellationToken = default);

    /// <summary>重置密码（写入新哈希并记入密码历史）。</summary>
    Task<bool> ResetPasswordAsync(
        string userId,
        string hash,
        string salt,
        int iterations,
        bool mustChangePwd,
        CancellationToken cancellationToken = default);

    /// <summary>删除用户，返回实际删除条数。</summary>
    Task<int> DeleteAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 角色管理（后台）。除功能点外还需能维护配额。
/// </summary>
public interface IRoleAdminStore
{
    /// <summary>取全部角色及其功能点与配额（用于权限矩阵）。</summary>
    Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>取某角色下的用户数（删除前的占用检查）。</summary>
    Task<IReadOnlyDictionary<string, int>> CountUsersByRoleAsync(CancellationToken cancellationToken = default);

    /// <summary>取单个角色。</summary>
    Task<Domain.Entities.Identity.Role?> FindAsync(string roleId, CancellationToken cancellationToken = default);

    /// <summary>取角色的功能点编码集合。</summary>
    Task<IReadOnlyList<string>> GetFunctionPointsAsync(string roleId, CancellationToken cancellationToken = default);

    /// <summary>取角色的配额。</summary>
    Task<IReadOnlyDictionary<string, int>> GetQuotasAsync(string roleId, CancellationToken cancellationToken = default);

    /// <summary>全量替换角色的功能点集合。</summary>
    Task ReplaceFunctionPointsAsync(
        string roleId,
        IEnumerable<string> codes,
        CancellationToken cancellationToken = default);

    /// <summary>写入角色的配额（键不存在则新增）。</summary>
    Task SetQuotaAsync(string roleId, string quotaKey, int value, CancellationToken cancellationToken = default);

    /// <summary>更新角色基本信息。</summary>
    Task<bool> UpdateAsync(
        string roleId,
        string? name,
        string? description,
        CancellationToken cancellationToken = default);

    /// <summary>删除角色（内置角色与仍有用户的角色不允许删除），返回实际删除条数。</summary>
    Task<int> DeleteAsync(string roleId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 会话与登录日志（后台）。在 <see cref="ISessionStore"/> 之上补单条撤销与日志查询。
/// </summary>
public interface ISessionAdminStore
{
    /// <summary>取最近登录日志（按时间倒序）。</summary>
    Task<IReadOnlyList<LoginLog>> GetLoginLogsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>撤销指定会话（按令牌哈希）。</summary>
    Task<int> RevokeAsync(string tokenHash, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    /// <summary>撤销某用户的全部会话（用于「强制下线」）。</summary>
    Task<int> RevokeAllAsync(string userId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    /// <summary>当前有效会话总数。</summary>
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 操作审计日志。对应新增表 <c>AuditLog</c>（实施计划 §5.8：管理员操作必须留痕）。
/// </summary>
public interface IAuditStore
{
    /// <summary>写一条审计记录。</summary>
    Task AddAsync(Domain.Entities.System.AuditLog log, CancellationToken cancellationToken = default);

    /// <summary>取最近审计记录（可按用户过滤）。</summary>
    Task<IReadOnlyList<Domain.Entities.System.AuditLog>> GetRecentAsync(
        string? userId,
        int limit,
        CancellationToken cancellationToken = default);
}
