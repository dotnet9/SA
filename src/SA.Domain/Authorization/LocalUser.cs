namespace SA.Domain.Authorization;

/// <summary>
/// 本机用户标识。
/// </summary>
/// <remarks>
/// <para>
/// 本应用<b>不做登录与权限控制</b>，但提醒规则、选股策略、推送订阅、事件标注这些数据
/// 需要一个稳定的存储归属（否则无法区分「谁的规则」）。因此统一归属到这个固定的本机用户，
/// 数据仍然持久化在服务端——这样「关掉页面也能收到推送」才成立
/// （提醒的后台巡检必须在服务端按规则执行）。
/// </para>
/// <para>
/// 与自选股的区别：自选存在浏览器本地，各设备独立；提醒与推送必须由服务端执行，
/// 无法只放浏览器，因此这里保留服务端存储。
/// </para>
/// </remarks>
public static class LocalUser
{
    /// <summary>本机用户 Id。</summary>
    public const string Id = "local";

    /// <summary>本机用户名（写入审计与标注的创建者字段）。</summary>
    public const string Username = "local";

    /// <summary>本机用户的角色 Id（不落库，权限解析时直接给全部功能点）。</summary>
    public const string RoleId = "local";

    /// <summary>本机用户的角色显示名。</summary>
    public const string RoleName = "本机用户";

    /// <summary>是否为内置的本机用户。</summary>
    public static bool Is(string? userId) => string.Equals(userId, Id, StringComparison.Ordinal);
}
