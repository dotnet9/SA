namespace SA.Domain.Entities.Identity;

/// <summary>
/// 功能点字典。对应 <c>FunctionPoint</c> 表：内容由代码中的
/// <c>SA.Domain.Authorization.FunctionPointCatalog</c> 在启动时同步入库，仅供后台矩阵展示，
/// 授权判定永远以代码目录为准（避免改库即可提权）。
/// </summary>
public class FunctionPointRow
{
    /// <summary>功能点编码。</summary>
    public required string Code { get; set; }

    /// <summary>所属分组名。</summary>
    public required string GroupName { get; set; }

    /// <summary>显示名。</summary>
    public required string Name { get; set; }

    /// <summary>说明。</summary>
    public string? Description { get; set; }
}
