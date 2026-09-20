using SA.Application.Abstractions;
using SA.Application.Search;
using ToolGood.Words.Pinyin;

namespace SA.Infrastructure.Search;

/// <summary>
/// 基于 ToolGood.Words.Pinyin 的拼音索引实现（实施计划 §2 决策 12）。
/// </summary>
/// <remarks>
/// 该库自带精简字典、无文件依赖、可离线运行，符合「本机运行、不引入 Python」的约束。
/// 若日后需要回退 TinyPinyin，只需在此文件替换 <see cref="InitialOf"/> 的实现，
/// 上层的清洗与匹配规则（<see cref="PinyinInitials"/>）不受影响。
/// </remarks>
public sealed class ToolGoodPinyinIndexer : IPinyinIndexer
{
    /// <inheritdoc />
    public string? InitialOf(char ch)
    {
        if (!IsCjk(ch))
        {
            return null;
        }

        var pinyin = WordsHelper.GetPinyin(ch.ToString());
        return string.IsNullOrWhiteSpace(pinyin) ? null : pinyin[..1];
    }

    /// <inheritdoc />
    public string? InitialsOf(string name) => PinyinInitials.Build(name, InitialOf);

    /// <summary>
    /// 判断是否为需要走拼音的汉字（CJK 统一表意文字基本区与扩展 A）。
    /// </summary>
    private static bool IsCjk(char ch) =>
        ch is >= '\u4E00' and <= '\u9FFF' or >= '\u3400' and <= '\u4DBF';
}
