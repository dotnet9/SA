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
    /// 由功能点集合派生数据范围。
    /// </summary>
    /// <remarks>
    /// 需求规格把这两个功能点定义为「互斥」，但内置管理员预设取的是完整目录
    /// （原型 <c>data.js</c> 的 <c>allFpCodes</c>），因此实际上会同时具备两者。
    /// 判定规则取「<c>data.scope.all</c> 优先」：
    /// <list type="bullet">
    /// <item>具备 <c>data.scope.all</c> → 全市场（该功能点本身就是全市场授权，不构成越权）；</item>
    /// <item>否则一律仅自选，包含两者都不具备的异常配置，按最小权限处理。</item>
    /// </list>
    /// </remarks>
    public static DataScope FromFunctionPoints(IEnumerable<string> codes)
    {
        var set = codes as ICollection<string> ?? codes.ToList();
        return set.Contains(FunctionPointCatalog.DataScopeAll) ? DataScope.All : DataScope.Watchlist;
    }
}
