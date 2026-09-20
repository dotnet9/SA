namespace SA.Domain.Entities.Identity;

/// <summary>
/// 角色。对应 <c>Role</c> 表。内置角色 Id 见 <c>SA.Domain.Authorization.BuiltInRoleIds</c>。
/// </summary>
public class Role
{
    /// <summary>主键。</summary>
    public required string Id { get; set; }

    /// <summary>显示名。</summary>
    public required string Name { get; set; }

    /// <summary>说明。</summary>
    public string? Description { get; set; }

    /// <summary>是否内置角色。内置角色不允许删除，只允许调整功能点。</summary>
    public bool IsBuiltin { get; set; }

    /// <summary>创建时间。</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>角色的功能点授权。</summary>
    public ICollection<RoleFunctionPoint> FunctionPoints { get; set; } = [];

    /// <summary>角色的操作级权限参数。</summary>
    public ICollection<RoleQuota> Quotas { get; set; } = [];
}

/// <summary>
/// 角色与功能点的关联。对应 <c>RoleFunctionPoint</c> 表。
/// </summary>
public class RoleFunctionPoint
{
    /// <summary>角色 Id。</summary>
    public required string RoleId { get; set; }

    /// <summary>功能点编码。</summary>
    public required string FunctionPointCode { get; set; }
}

/// <summary>
/// 角色操作级权限参数。对应 <c>RoleQuota</c> 表。
/// </summary>
public class RoleQuota
{
    /// <summary>角色 Id。</summary>
    public required string RoleId { get; set; }

    /// <summary>参数键，取值见 <see cref="QuotaKeys"/>。</summary>
    public required string QuotaKey { get; set; }

    /// <summary>参数值。</summary>
    public int QuotaValue { get; set; }
}

/// <summary>
/// 操作级权限参数的键。与详细设计 DDL 的 <c>RoleQuota.QuotaKey</c> 注释一致。
/// </summary>
public static class QuotaKeys
{
    /// <summary>可查看的历史数据最大年限。</summary>
    public const string HistoryYears = "history.years";

    /// <summary>单日分析查询次数上限。</summary>
    public const string DailyQueries = "quota.daily";

    /// <summary>单次导出行数上限。</summary>
    public const string ExportRows = "export.rows";

    /// <summary>自选股数量上限。</summary>
    public const string WatchlistMax = "watchlist.max";

    /// <summary>提醒规则数量上限。</summary>
    public const string AlertMax = "alert.max";

    /// <summary>选股策略数量上限。</summary>
    public const string StrategyMax = "strategy.max";
}
