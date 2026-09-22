using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using SA.Application.Abstractions;
using SA.Domain.Authorization;
using SA.Domain.Entities.Identity;

namespace SA.Application.Authorization;

/// <summary>
/// 权限解析与缓存。功能点与配额在登录时载入并缓存；权限矩阵变更时通过
/// <see cref="InvalidateRole"/> 失效，使「下一请求即生效」（需求规格 §7.3、验收 AC-07）。
/// </summary>
public sealed class PermissionService(IRoleStore roleStore, IUserStore userStore, IMemoryCache cache)
{
    private static readonly ConcurrentDictionary<string, long> RoleVersions = new(StringComparer.Ordinal);
    private readonly IRoleStore _roleStore = roleStore;
    private readonly IUserStore _userStore = userStore;
    private readonly IMemoryCache _cache = cache;

    /// <summary>
    /// 解析结果。
    /// </summary>
    /// <param name="RoleId">角色 Id。</param>
    /// <param name="RoleName">角色显示名。</param>
    /// <param name="FunctionPoints">功能点编码。</param>
    /// <param name="Quotas">操作级参数。</param>
    /// <param name="DataScope">数据范围。</param>
    public sealed record ResolvedPermissions(
        string RoleId,
        string RoleName,
        IReadOnlyList<string> FunctionPoints,
        IReadOnlyDictionary<string, int> Quotas,
        DataScope DataScope)
    {
        /// <summary>是否拥有指定功能点。</summary>
        public bool Has(string code) => FunctionPoints.Contains(code, StringComparer.Ordinal);
    }

    /// <summary>
    /// 取用户权限（带缓存）。
    /// </summary>
    public async Task<ResolvedPermissions?> ResolveAsync(string userId, CancellationToken cancellationToken = default)
    {
        // 本机用户：应用不做登录与权限控制，它不落库，因此直接给「全部功能点 + 默认配额」。
        // 若走查库分支会拿不到用户而返回 null，端点会把「解析失败」当成 401 拒绝访问。
        if (LocalUser.Is(userId))
        {
            return LocalPermissions;
        }

        var user = await _userStore.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        var version = RoleVersions.GetValueOrDefault(user.RoleId);
        var cacheKey = CacheKey(userId);

        if (_cache.TryGetValue(cacheKey, out CacheEntry? cached)
            && cached is not null
            && cached.RoleVersion == version
            && cached.RoleId == user.RoleId)
        {
            return cached.Permissions;
        }

        var role = await _roleStore.FindAsync(user.RoleId, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return null;
        }

        var functionPoints = await _roleStore.GetFunctionPointsAsync(user.RoleId, cancellationToken).ConfigureAwait(false);
        var quotas = await _roleStore.GetQuotasAsync(user.RoleId, cancellationToken).ConfigureAwait(false);
        var scope = DataScopes.FromFunctionPoints(functionPoints);

        var permissions = new ResolvedPermissions(role.Id, role.Name, functionPoints, quotas, scope);
        _cache.Set(cacheKey, new CacheEntry(role.Id, version, permissions), TimeSpan.FromMinutes(10));

        return permissions;
    }

    /// <summary>
    /// 使单个用户的权限缓存失效（角色分配变更、用户资料变更）。
    /// </summary>
    public void Invalidate(string userId) => _cache.Remove(CacheKey(userId));

    /// <summary>
    /// 使某个角色的所有用户权限缓存失效（权限矩阵保存后调用）。
    /// </summary>
    public void InvalidateRole(string roleId) => RoleVersions.AddOrUpdate(roleId, 1, (_, current) => current + 1);

    /// <summary>
    /// 本机用户的权限：全部功能点 + 默认配额。
    /// </summary>
    /// <remarks>
    /// 配额取「够用即可」的固定值：本机自用场景不需要配额约束，
    /// 但保留这几个键是为了让导出、历史年限、策略数量等逻辑不必到处判空。
    /// </remarks>
    private static readonly ResolvedPermissions LocalPermissions = new(
        RoleId: LocalUser.RoleId,
        RoleName: LocalUser.RoleName,
        FunctionPoints: FunctionPointCatalog.AllCodes,
        Quotas: new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [QuotaKeys.ExportRows] = 50_000,
            [QuotaKeys.HistoryYears] = 10,
            [QuotaKeys.DailyQueries] = 2000,
            [QuotaKeys.AlertMax] = 200,
            [QuotaKeys.StrategyMax] = 100,
            [QuotaKeys.WatchlistMax] = 500
        },
        DataScope: DataScope.All);

    private static string CacheKey(string userId) => $"sa:perm:{userId}";

    private sealed record CacheEntry(string RoleId, long RoleVersion, ResolvedPermissions Permissions);
}
