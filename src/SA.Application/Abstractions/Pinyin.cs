namespace SA.Application.Abstractions;

/// <summary>
/// 名称到拼音首字母的转换。用于搜索的拼音匹配（需求规格 §5.2「拼音首字母」）。
/// </summary>
/// <remarks>
/// 抽象出来是为了让「名称清洗与首字母组装」这段纯逻辑可以脱离第三方拼音库被单测覆盖，
/// 同时把库的选型（实施计划 §2 决策 12：ToolGood.Words.Pinyin，不可用时回退 TinyPinyin）
/// 约束在基础设施层的一处实现里。
/// </remarks>
public interface IPinyinIndexer
{
    /// <summary>
    /// 取单个汉字的拼音首字母（小写）；非汉字返回 null。
    /// </summary>
    string? InitialOf(char ch);

    /// <summary>
    /// 取整串名称的拼音首字母（小写，仅保留字母与数字）。
    /// </summary>
    string? InitialsOf(string name);
}
