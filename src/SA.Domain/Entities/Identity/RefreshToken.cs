namespace SA.Domain.Entities.Identity;

/// <summary>
/// 刷新令牌。对应 <c>RefreshToken</c> 表。明文令牌只存在于 Cookie 中，库里存哈希。
/// </summary>
public class RefreshToken
{
    /// <summary>主键。</summary>
    public required string Id { get; set; }

    /// <summary>所属用户。</summary>
    public required string UserId { get; set; }

    /// <summary>令牌哈希（SHA-256，Base64）。</summary>
    public required string TokenHash { get; set; }

    /// <summary>设备标识（User-Agent 摘要）。</summary>
    public string? Device { get; set; }

    /// <summary>签发 IP。</summary>
    public string? Ip { get; set; }

    /// <summary>过期时间。</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>吊销时间（轮换或强制下线）。</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>创建时间。</summary>
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// 登录结果。取值与详细设计 DDL 的 <c>LoginLog.Result</c> 一致。
/// </summary>
public static class LoginResultKind
{
    /// <summary>成功。</summary>
    public const string Success = "success";

    /// <summary>失败（用户名或密码错误、验证码错误）。</summary>
    public const string Failed = "failed";

    /// <summary>被拒绝（账号禁用、锁定、未授权的来源）。</summary>
    public const string Denied = "denied";
}

/// <summary>
/// 登录日志。对应 <c>LoginLog</c> 表，供后台「登录与安全」页展示与过滤。
/// </summary>
public class LoginLog
{
    /// <summary>自增主键。</summary>
    public long Id { get; set; }

    /// <summary>登录名（失败时也记录，便于排查撞库）。</summary>
    public string? UserName { get; set; }

    /// <summary>来源 IP。</summary>
    public string? Ip { get; set; }

    /// <summary>设备标识。</summary>
    public string? Device { get; set; }

    /// <summary>结果：<see cref="LoginResultKind"/>。</summary>
    public required string Result { get; set; }

    /// <summary>备注（失败原因等）。</summary>
    public string? Note { get; set; }

    /// <summary>发生时间。</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
