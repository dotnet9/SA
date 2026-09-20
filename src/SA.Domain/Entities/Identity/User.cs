namespace SA.Domain.Entities.Identity;

/// <summary>
/// 账号状态。取值与详细设计 DDL 的 <c>Status</c> 列一致。
/// </summary>
public static class UserStatus
{
    /// <summary>启用。</summary>
    public const string Active = "active";

    /// <summary>禁用（禁止登录，但保留数据）。</summary>
    public const string Disabled = "disabled";
}

/// <summary>
/// 用户。对应 <c>User</c> 表，字段与 docs/详细设计.md §3.1 逐列对应。
/// </summary>
public class User
{
    /// <summary>主键。</summary>
    public required string Id { get; set; }

    /// <summary>登录名，唯一。</summary>
    public required string Username { get; set; }

    /// <summary>显示名。</summary>
    public required string Nickname { get; set; }

    /// <summary>PBKDF2 派生密钥（Base64）。</summary>
    public required string PasswordHash { get; set; }

    /// <summary>加盐（Base64）。</summary>
    public required string PasswordSalt { get; set; }

    /// <summary>PBKDF2 迭代次数，随密码一起存储以便日后提高强度而不影响老密码。</summary>
    public int PasswordIterations { get; set; }

    /// <summary>TOTP 密钥（受 Data Protection 保护后存储）。</summary>
    public string? TotpSecret { get; set; }

    /// <summary>是否已启用二次验证。</summary>
    public bool TotpEnabled { get; set; }

    /// <summary>是否强制下次登录改密。</summary>
    public bool MustChangePwd { get; set; }

    /// <summary>密码设置时间，用于有效期校验。</summary>
    public DateTimeOffset? PasswordChangedAt { get; set; }

    /// <summary>状态：<see cref="UserStatus"/>。</summary>
    public string Status { get; set; } = UserStatus.Active;

    /// <summary>所属角色。</summary>
    public required string RoleId { get; set; }

    /// <summary>最近登录时间。</summary>
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>最近登录 IP。</summary>
    public string? LastLoginIp { get; set; }

    /// <summary>连续登录失败次数，达阈值即锁定。</summary>
    public int FailCount { get; set; }

    /// <summary>锁定截止时间。</summary>
    public DateTimeOffset? LockedUntil { get; set; }

    /// <summary>创建时间。</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>角色导航属性。</summary>
    public Role? Role { get; set; }
}
