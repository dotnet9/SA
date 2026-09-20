using System.Text;
using SA.Application.Abstractions;

namespace SA.Application.Search;

/// <summary>
/// 拼音首字母的清洗与组装。纯逻辑，不依赖具体拼音库。
/// </summary>
/// <remarks>
/// 证券名称里混着大量非汉字写法，实测样本来自东财全市场列表：
/// 「万  科Ａ」（全角Ａ + 双空格）、「TCL科技」、「*ST海航」、「PT金田A」、「国华退」。
/// 直接交给拼音库会把空格、全角字母、星号原样带进索引，导致按 <c>wka</c> 搜不到「万  科Ａ」。
/// 因此统一规则：汉字取拼音首字母，ASCII 字母与数字原样保留并转小写，其余字符丢弃。
/// </remarks>
public static class PinyinInitials
{
    /// <summary>
    /// 按给定规则组装首字母串。
    /// </summary>
    /// <param name="name">证券名称。</param>
    /// <param name="initialOf">取单个汉字拼音首字母的函数；非汉字返回 null。</param>
    /// <returns>小写首字母串；无可用字符时返回 null。</returns>
    public static string? Build(string? name, Func<char, string?> initialOf)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var buffer = new StringBuilder(name.Length);
        foreach (var raw in name)
        {
            // 全角字母 / 数字（Ａ、１）先折半，否则会被当作非字母丢弃
            var ch = Fold(raw);

            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                buffer.Append(ch);
                continue;
            }

            if (ch is >= 'A' and <= 'Z')
            {
                buffer.Append(char.ToLowerInvariant(ch));
                continue;
            }

            var initial = initialOf(raw);
            if (!string.IsNullOrEmpty(initial))
            {
                foreach (var letter in initial)
                {
                    if (char.IsLetterOrDigit(letter))
                    {
                        buffer.Append(char.ToLowerInvariant(letter));
                    }
                }
            }
        }

        return buffer.Length == 0 ? null : buffer.ToString();
    }

    /// <summary>
    /// 把全角 ASCII 区间折半；其余字符原样返回。
    /// </summary>
    private static char Fold(char ch) =>
        ch is >= '\uFF01' and <= '\uFF5E' ? (char)(ch - 0xFEE0) : ch == '\u3000' ? ' ' : ch;
}

/// <summary>
/// 名称与查询串的匹配判定。搜索排序依赖这套规则，因此与 <see cref="PinyinInitials"/> 放在一起。
/// </summary>
public static class SearchMatcher
{
    /// <summary>
    /// 判断查询串是否命中名称（包含式，忽略大小写与空白）。
    /// </summary>
    public static bool NameMatches(string? name, string query) =>
        !string.IsNullOrEmpty(name)
        && name.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Contains(query, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 判断查询串是否命中拼音首字母（前缀式：<c>nd</c> 命中 <c>ndsd</c>，避免短串命中过多无关标的）。
    /// </summary>
    public static bool PinyinMatches(string? pinyin, string query) =>
        !string.IsNullOrEmpty(pinyin)
        && pinyin.StartsWith(query, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 判断查询串是否命中行业名（包含式）。
    /// </summary>
    public static bool IndustryMatches(string? industry, string query) =>
        !string.IsNullOrEmpty(industry)
        && industry.Contains(query, StringComparison.OrdinalIgnoreCase);
}
