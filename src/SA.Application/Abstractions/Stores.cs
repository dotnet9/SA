using SA.Domain.Entities.Identity;

namespace SA.Application.Abstractions;

/// <summary>
/// 用户与密码历史的读写。实现位于 Infrastructure，Application 只依赖此接口
/// （依赖方向：Api → Application → Domain，见实施计划 §1.1）。
/// </summary>
public interface IUserStore
{
    /// <summary>按登录名取用户（不区分大小写）。</summary>
    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>按 Id 取用户。</summary>
    Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>更新用户（保存由调用方通过 <see cref="IUnitOfWork"/> 提交）。</summary>
    void Update(User user);

    /// <summary>新增用户。</summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>记录一条密码历史（用于「不与最近 N 次重复」校验）。</summary>
    Task AddPasswordHistoryAsync(string userId, string hash, string salt, int iterations, CancellationToken cancellationToken = default);

    /// <summary>取最近若干条密码历史，按时间倒序。</summary>
    Task<IReadOnlyList<PasswordHistoryEntry>> GetRecentPasswordsAsync(string userId, int take, CancellationToken cancellationToken = default);

    /// <summary>是否存在任意用户（用于判断是否首次启动）。</summary>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 密码历史条目。
/// </summary>
/// <param name="Hash">派生密钥。</param>
/// <param name="Salt">盐。</param>
/// <param name="Iterations">迭代次数。</param>
public readonly record struct PasswordHistoryEntry(string Hash, string Salt, int Iterations);

/// <summary>
/// 角色、功能点授权与操作级参数的读写。
/// </summary>
public interface IRoleStore
{
    /// <summary>取角色。</summary>
    Task<Role?> FindAsync(string roleId, CancellationToken cancellationToken = default);

    /// <summary>取角色的功能点编码集合。</summary>
    Task<IReadOnlyList<string>> GetFunctionPointsAsync(string roleId, CancellationToken cancellationToken = default);

    /// <summary>取角色的操作级权限参数。</summary>
    Task<IReadOnlyDictionary<string, int>> GetQuotasAsync(string roleId, CancellationToken cancellationToken = default);

    /// <summary>取全部角色。</summary>
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>新增角色。</summary>
    Task AddAsync(Role role, CancellationToken cancellationToken = default);

    /// <summary>整体替换角色的功能点授权。</summary>
    Task ReplaceFunctionPointsAsync(string roleId, IEnumerable<string> codes, CancellationToken cancellationToken = default);
}

/// <summary>
/// 会话、登录日志与配额用量的读写。
/// </summary>
public interface ISessionStore
{
    /// <summary>新增刷新令牌。</summary>
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);

    /// <summary>按令牌哈希取未吊销的刷新令牌。</summary>
    Task<RefreshToken?> FindRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>取用户当前的活跃会话（未过期未吊销）。</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>吊销单个刷新令牌。</summary>
    void RevokeRefreshToken(RefreshToken token, DateTimeOffset revokedAt);

    /// <summary>吊销用户全部刷新令牌（强制下线 / 改密后使用），返回受影响条数。</summary>
    Task<int> RevokeAllRefreshTokensAsync(string userId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    /// <summary>写登录日志。</summary>
    Task AddLoginLogAsync(LoginLog log, CancellationToken cancellationToken = default);

    /// <summary>取指定用户指定业务日、指定种类的配额用量。</summary>
    Task<int> GetQuotaUsageAsync(string userId, string day, string kind, CancellationToken cancellationToken = default);

    /// <summary>累加配额用量并返回累加后的值。</summary>
    Task<int> IncrementQuotaUsageAsync(string userId, string day, string kind, int delta, CancellationToken cancellationToken = default);
}

/// <summary>
/// 系统级与个人设置的读写。
/// </summary>
public interface ISettingsStore
{
    /// <summary>取系统设置值。</summary>
    Task<string?> GetAppSettingAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>写系统设置值（不存在则新增）。</summary>
    Task SetAppSettingAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>取个人设置 JSON。</summary>
    Task<string?> GetUserSettingsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>写个人设置 JSON。</summary>
    Task SetUserSettingsAsync(string userId, string json, CancellationToken cancellationToken = default);
}

/// <summary>
/// 工作单元：把一次用例内的多处写入合并为一次提交。
/// </summary>
public interface IUnitOfWork
{
    /// <summary>提交挂起的变更。</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
