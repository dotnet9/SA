namespace SA.Domain.Authorization;

/// <summary>
/// 数据范围。由角色的 <c>data.scope.all</c> / <c>data.scope.watchlist</c> 功能点派生，
/// 两者互斥（docs/需求规格.md §7.1「数据范围」）。
/// </summary>
public enum DataScope
{
    /// <summary>全市场：可查询任意股票。</summary>
    All = 0,

    /// <summary>仅自选：所有查询结果都被裁剪到该用户自选股范围内。</summary>
    Watchlist = 1
}

/// <summary>
/// 数据范围的派生与解析。
/// </summary>
public static class DataScopes
{
    /// <summary>
    /// 由功能点集合派生数据范围。同时具备或都不具备时按保守口径处理：
    /// 只有明确拥有 <c>data.scope.all</c> 且不具备 <c>data.scope.watchlist</c> 才视为全市场。
    /// </summary>
    public static DataScope FromFunctionPoints(IEnumerable<string> codes)
    {
        var set = codes as ICollection<string> ?? codes.ToList();
        var hasAll = set.Contains(FunctionPointCatalog.DataScopeAll);
        var hasWatchlist = set.Contains(FunctionPointCatalog.DataScopeWatchlist);

        return hasAll && !hasWatchlist ? DataScope.All : DataScope.Watchlist;
    }
}
