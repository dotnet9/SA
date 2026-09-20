using SA.Domain.Authorization;

namespace SA.Application.Admin;

/// <summary>
/// 功能点的展示信息（名称与分组）查询。
/// </summary>
/// <remarks>
/// 功能点目录是<b>唯一来源</b>（<see cref="FunctionPointCatalog"/>）：后台权限矩阵直接由它生成，
/// 因此界面不会漏项、也不会出现已废弃的项。这里只负责把目录摊平成便于接口输出的形状。
/// </remarks>
public static class FunctionPointLookup
{
    /// <summary>
    /// 全部功能点及其名称与分组，顺序与目录中的分组顺序一致。
    /// </summary>
    public static IReadOnlyList<(string Code, string Name, string Group)> Describe()
    {
        var result = new List<(string, string, string)>(FunctionPointCatalog.AllCodes.Count);

        foreach (var group in FunctionPointCatalog.Groups)
        {
            foreach (var point in group.Items)
            {
                result.Add((point.Code, point.Name, group.Name));
            }
        }

        // 目录按分组组织，个别功能点可能未归组：兜底补上，保证「目录里的都能在矩阵里看到」
        var known = result.Select(item => item.Item1).ToHashSet(StringComparer.Ordinal);
        foreach (var code in FunctionPointCatalog.AllCodes)
        {
            if (!known.Contains(code))
            {
                var name = FunctionPointCatalog.ByCode.TryGetValue(code, out var point) ? point.Name : code;
                result.Add((code, name, "其他"));
            }
        }

        return result;
    }
}
