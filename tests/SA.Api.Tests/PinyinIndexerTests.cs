using SA.Infrastructure.Search;

namespace SA.Api.Tests;

/// <summary>
/// 拼音索引。样本全部取自东财全市场列表的真实名称（含全角字母、空格与 ST 前缀），
/// 确保「按拼音搜得到」这条需求在异常名称上同样成立。
/// </summary>
public class PinyinIndexerTests
{
    private readonly ToolGoodPinyinIndexer _indexer = new();

    [Theory]
    [InlineData("宁德时代", "ndsd")]
    [InlineData("贵州茅台", "gzmt")]
    [InlineData("比亚迪", "byd")]
    [InlineData("招商银行", "zsyh")]
    public void 常见证券名取到正确首字母(string name, string expected) =>
        Assert.Equal(expected, _indexer.InitialsOf(name));

    [Theory]
    [InlineData("万  科Ａ", "wka")]
    [InlineData("TCL科技", "tclkj")]
    [InlineData("深振业Ａ", "szya")]
    [InlineData("*ST海航", "sthh")]
    [InlineData("国华退", "ght")]
    public void 含全角与字母的名称同样可取到首字母(string name, string expected) =>
        Assert.Equal(expected, _indexer.InitialsOf(name));

    [Fact]
    public void 无汉字且无字母的名称返回空()
    {
        Assert.Null(_indexer.InitialsOf("---"));
        Assert.Null(_indexer.InitialsOf("   "));
        Assert.Null(_indexer.InitialsOf(string.Empty));
    }

    [Fact]
    public void 非汉字字符不参与拼音转换() =>
        Assert.Null(_indexer.InitialOf('A'));
}
