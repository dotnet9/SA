using SA.Domain.Authorization;

namespace SA.Domain.Tests.Authorization;

/// <summary>
/// 内置角色预设。与原型 <c>design/web/_shared/data.js</c> 的 roles 逐条对应，
/// 差异会直接表现为「登录后菜单项数量与原型不一致」。
/// </summary>
public class BuiltInRolesTests
{
    [Fact]
    public void 内置角色为管理员普通用户访客()
    {
        var actual = BuiltInRoles.All.Select(r => r.Id).ToArray();

        Assert.Equal(new[] { BuiltInRoleIds.Admin, BuiltInRoleIds.User, BuiltInRoleIds.Guest }, actual);
    }

    [Fact]
    public void 管理员拥有全部功能点()
    {
        var admin = BuiltInRoles.Find(BuiltInRoleIds.Admin);
        Assert.NotNull(admin);

        Assert.Equal(FunctionPointCatalog.AllCodes.Count, admin.FunctionPoints.Count);

        var missing = FunctionPointCatalog.AllCodes.Except(admin.FunctionPoints, StringComparer.Ordinal).ToArray();
        Assert.Empty(missing);
    }

    [Fact]
    public void 普通用户为二十项且无导出与后台()
    {
        var user = BuiltInRoles.Find(BuiltInRoleIds.User);
        Assert.NotNull(user);

        Assert.Equal(20, user.FunctionPoints.Count);
        Assert.DoesNotContain(FunctionPointCatalog.ExportData, user.FunctionPoints);
        Assert.Contains(FunctionPointCatalog.WatchlistEdit, user.FunctionPoints);
        Assert.Contains(FunctionPointCatalog.ScreenerSaveStrategy, user.FunctionPoints);
        Assert.Contains(FunctionPointCatalog.AlertManage, user.FunctionPoints);

        var adminCodes = user.FunctionPoints.Where(c => c.StartsWith("admin.", StringComparison.Ordinal)).ToArray();
        Assert.Empty(adminCodes);
    }

    [Fact]
    public void 访客仅市场概览搜索趋势且数据范围仅自选()
    {
        var guest = BuiltInRoles.Find(BuiltInRoleIds.Guest);
        Assert.NotNull(guest);

        var expected = new[]
        {
            FunctionPointCatalog.MarketView,
            FunctionPointCatalog.StockSearch,
            FunctionPointCatalog.StockTrend,
            FunctionPointCatalog.DataScopeWatchlist
        };

        Assert.Equal(expected, guest.FunctionPoints.ToArray());
    }

    [Fact]
    public void 所有预设功能点都在目录内()
    {
        foreach (var role in BuiltInRoles.All)
        {
            foreach (var code in role.FunctionPoints)
            {
                Assert.True(FunctionPointCatalog.Contains(code), $"{role.Id} 引用了未知功能点 {code}");
            }
        }
    }

    [Fact]
    public void 数据范围由功能点派生且互斥取保守口径()
    {
        Assert.Equal(DataScope.All, DataScopes.FromFunctionPoints(new[] { FunctionPointCatalog.DataScopeAll }));
        Assert.Equal(DataScope.Watchlist, DataScopes.FromFunctionPoints(new[] { FunctionPointCatalog.DataScopeWatchlist }));

        // 两者都不具备属于异常配置，按最小权限处理
        Assert.Equal(DataScope.Watchlist, DataScopes.FromFunctionPoints(Array.Empty<string>()));

        // 两者同时具备是管理员预设的实际形态（完整目录），以 data.scope.all 为准
        Assert.Equal(
            DataScope.All,
            DataScopes.FromFunctionPoints(new[] { FunctionPointCatalog.DataScopeAll, FunctionPointCatalog.DataScopeWatchlist }));
    }

    [Fact]
    public void 管理员预设的完整目录派生为全市场数据范围()
    {
        var admin = BuiltInRoles.Find(BuiltInRoleIds.Admin);
        Assert.NotNull(admin);

        Assert.Equal(DataScope.All, DataScopes.FromFunctionPoints(admin.FunctionPoints));
    }
}
