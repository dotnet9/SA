namespace SA.Domain.Entities.Identity;

/// <summary>
/// 密码历史。对应新增表 <c>PasswordHistory</c>（实施计划 §5.3 登记）：
/// 需求规格 §9 要求「密码不与最近 5 次重复」，必须留历史才能校验。
/// </summary>
public class PasswordHistory
{
    /// <summary>自增主键。</summary>
    public long Id { get; set; }

    /// <summary>用户 Id。</summary>
    public required string UserId { get; set; }

    /// <summary>历史密码派生密钥（Base64）。</summary>
    public required string PasswordHash { get; set; }

    /// <summary>历史盐（Base64）。</summary>
    public required string PasswordSalt { get; set; }

    /// <summary>迭代次数。</summary>
    public int PasswordIterations { get; set; }

    /// <summary>记录时间。</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
