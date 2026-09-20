namespace SA.Domain.Authorization;

/// <summary>
/// 内置角色的标识。自定义角色使用用户生成的 Id，因此内置角色一律用这里的固定值。
/// </summary>
public static class BuiltInRoleIds
{
    /// <summary>管理员：全部功能点。</summary>
    public const string Admin = "admin";

    /// <summary>普通用户：只读分析 + 自选 + 选股 + 提醒，无导出与后台。</summary>
    public const string User = "user";

    /// <summary>访客：仅市场概览 + 趋势 + 搜索，数据范围仅自选。</summary>
    public const string Guest = "guest";
}

/// <summary>
/// 内置角色预设。功能点清单逐条对应 docs/需求规格.md §7.2 与原型
/// <c>design/web/_shared/data.js:895-914</c> 的 <c>roles</c>，用于首次启动时播种。
/// </summary>
public static class BuiltInRoles
{
    /// <summary>
    /// 全部内置角色，顺序即后台展示顺序。
    /// </summary>
    public static IReadOnlyList<RolePreset> All { get; } =
    [
        new(BuiltInRoleIds.Admin,
            "管理员",
            "全部功能点，含后台管理",
            FunctionPointCatalog.AllCodes),

        new(BuiltInRoleIds.User,
            "普通用户",
            "只读分析 + 自选 + 选股 + 提醒，无导出与后台",
            [
                FunctionPointCatalog.MarketView,
                FunctionPointCatalog.StockSearch,
                FunctionPointCatalog.StockTrend,
                FunctionPointCatalog.StockFinance,
                FunctionPointCatalog.StockEquity,
                FunctionPointCatalog.StockCapital,
                FunctionPointCatalog.StockIndustry,
                FunctionPointCatalog.StockEvents,
                FunctionPointCatalog.StockRisk,
                FunctionPointCatalog.StockRating,
                FunctionPointCatalog.TopologyView,
                FunctionPointCatalog.WatchlistView,
                FunctionPointCatalog.WatchlistEdit,
                FunctionPointCatalog.ScreenerUse,
                FunctionPointCatalog.ScreenerSaveStrategy,
                FunctionPointCatalog.AlertManage,
                FunctionPointCatalog.NotifyView,
                FunctionPointCatalog.DataScopeAll,
                FunctionPointCatalog.HistoryYears,
                FunctionPointCatalog.QuotaDaily
            ]),

        new(BuiltInRoleIds.Guest,
            "访客",
            "仅市场概览 + 趋势 + 搜索，数据范围仅自选",
            [
                FunctionPointCatalog.MarketView,
                FunctionPointCatalog.StockSearch,
                FunctionPointCatalog.StockTrend,
                FunctionPointCatalog.DataScopeWatchlist
            ])
    ];

    /// <summary>
    /// 按 Id 查找内置角色预设。
    /// </summary>
    public static RolePreset? Find(string roleId) =>
        All.FirstOrDefault(r => string.Equals(r.Id, roleId, StringComparison.Ordinal));
}

/// <summary>
/// 角色预设。
/// </summary>
/// <param name="Id">角色 Id。</param>
/// <param name="Name">显示名。</param>
/// <param name="Description">说明文案。</param>
/// <param name="FunctionPoints">该角色开启的功能点编码。</param>
public sealed record RolePreset(string Id, string Name, string Description, IReadOnlyList<string> FunctionPoints);
