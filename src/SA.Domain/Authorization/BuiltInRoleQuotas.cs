using SA.Domain.Entities.Identity;

namespace SA.Domain.Authorization;

/// <summary>
/// 内置角色的操作级参数默认值。
/// </summary>
/// <remarks>
/// 文档只定义了参数**键**（详细设计 DDL 的 <c>RoleQuota.QuotaKey</c>），未给默认数值，
/// 这里的取值是实施期为「可用的默认体验」设定的，可在后台权限页调整；
/// 已在实施计划中登记为待确认项。
/// </remarks>
public static class BuiltInRoleQuotas
{
    /// <summary>管理员默认参数。</summary>
    public static IReadOnlyDictionary<string, int> Admin { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [QuotaKeys.HistoryYears] = 10,
        [QuotaKeys.DailyQueries] = 2000,
        [QuotaKeys.ExportRows] = 50_000,
        [QuotaKeys.WatchlistMax] = 500,
        [QuotaKeys.AlertMax] = 200,
        [QuotaKeys.StrategyMax] = 100
    };

    /// <summary>普通用户默认参数（无导出权限，故不设导出行数）。</summary>
    public static IReadOnlyDictionary<string, int> User { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [QuotaKeys.HistoryYears] = 5,
        [QuotaKeys.DailyQueries] = 500,
        [QuotaKeys.WatchlistMax] = 100,
        [QuotaKeys.AlertMax] = 50,
        [QuotaKeys.StrategyMax] = 20
    };

    /// <summary>访客默认参数。</summary>
    public static IReadOnlyDictionary<string, int> Guest { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [QuotaKeys.HistoryYears] = 1,
        [QuotaKeys.DailyQueries] = 50,
        [QuotaKeys.WatchlistMax] = 10,
        [QuotaKeys.AlertMax] = 0,
        [QuotaKeys.StrategyMax] = 0
    };

    /// <summary>
    /// 按角色 Id 取默认参数。
    /// </summary>
    public static IReadOnlyDictionary<string, int> For(string roleId) => roleId switch
    {
        BuiltInRoleIds.Admin => Admin,
        BuiltInRoleIds.User => User,
        BuiltInRoleIds.Guest => Guest,
        _ => Empty
    };

    private static readonly IReadOnlyDictionary<string, int> Empty =
        new Dictionary<string, int>(StringComparer.Ordinal);
}
