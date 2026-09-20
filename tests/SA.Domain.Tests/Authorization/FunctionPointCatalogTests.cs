using SA.Domain.Authorization;

namespace SA.Domain.Tests.Authorization;

/// <summary>
/// 功能点目录的完整性约束。这些断言的意义在于：编码一旦被改动（增删或拼错），
/// 后端授权与前端菜单裁剪会同时静默失配，必须在测试里被挡住。
/// </summary>
public class FunctionPointCatalogTests
{
    [Fact]
    public void 分组数量为七()
    {
        Assert.Equal(7, FunctionPointCatalog.Groups.Count);
    }

    [Fact]
    public void 编码唯一且无空值()
    {
        var codes = FunctionPointCatalog.AllCodes;

        var blanks = codes.Where(string.IsNullOrWhiteSpace).ToArray();
        Assert.Empty(blanks);
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void 编码数量与分组展开一致()
    {
        var expanded = FunctionPointCatalog.Groups.SelectMany(g => g.Items).Select(i => i.Code).ToList();

        Assert.Equal(expanded, FunctionPointCatalog.AllCodes);
        Assert.Equal(expanded.Count, FunctionPointCatalog.ByCode.Count);
    }

    [Theory]
    [InlineData("market.view")]
    [InlineData("stock.trend")]
    [InlineData("topology.view")]
    [InlineData("watchlist.edit")]
    [InlineData("screener.saveStrategy")]
    [InlineData("alert.manage")]
    [InlineData("notify.view")]
    [InlineData("data.scope.watchlist")]
    [InlineData("export.data")]
    [InlineData("event.edit")]
    [InlineData("admin.security")]
    public void 包含需求规格列出的关键编码(string code)
    {
        Assert.True(FunctionPointCatalog.Contains(code), $"缺少功能点 {code}");
    }

    [Fact]
    public void 每个功能点都有名称与说明()
    {
        foreach (var item in FunctionPointCatalog.Groups.SelectMany(g => g.Items))
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Name), $"{item.Code} 缺少名称");
            Assert.False(string.IsNullOrWhiteSpace(item.Description), $"{item.Code} 缺少说明");
        }
    }
}
