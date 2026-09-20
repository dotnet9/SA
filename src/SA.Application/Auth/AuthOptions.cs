namespace SA.Application.Auth;

/// <summary>
/// 鉴权相关配置。对应 docs/详细设计.md §10 配置项中的 <c>Sa:Auth</c> 节点。
/// </summary>
public sealed class AuthOptions
{
    /// <summary>配置节名。</summary>
    public const string SectionName = "Sa:Auth";

    /// <summary>访问令牌有效期（分钟）。</summary>
    public int AccessTokenMinutes { get; set; } = 30;

    /// <summary>刷新令牌有效期（小时）。</summary>
    public int RefreshTokenHours { get; set; } = 72;

    /// <summary>单账号最大并发会话数。</summary>
    public int MaxSessionsPerUser { get; set; } = 5;

    /// <summary>连续登录失败达到该次数即锁定。</summary>
    public int LockThreshold { get; set; } = 5;

    /// <summary>锁定时长（分钟）。</summary>
    public int LockMinutes { get; set; } = 15;

    /// <summary>密码最小长度。</summary>
    public int PasswordMinLength { get; set; } = 10;

    /// <summary>密码有效期（天）。</summary>
    public int PasswordExpireDays { get; set; } = 90;

    /// <summary>
    /// 是否强制二次验证。需求规格要求全账号强制，但开发期默认关闭
    /// （实施计划 §2 决策 6）；开启后登录接口要求 totpCode。
    /// </summary>
    public bool RequireTotp { get; set; }

    /// <summary>JWT 签名密钥（至少 32 字节）。敏感项走环境变量或用户机密，不写入仓库。</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>签发者。</summary>
    public string Issuer { get; set; } = "sa";

    /// <summary>受众。</summary>
    public string Audience { get; set; } = "sa-web";

    /// <summary>刷新令牌 Cookie 名。</summary>
    public string RefreshCookieName { get; set; } = "sa.rt";

    /// <summary>首启创建的管理员初始密码；为空则随机生成并打印到启动日志一次。</summary>
    public string? AdminInitialPassword { get; set; }
}
