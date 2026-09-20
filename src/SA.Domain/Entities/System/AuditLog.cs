namespace SA.Domain.Entities.System;

/// <summary>
/// 操作审计日志。对应新增表 <c>AuditLog</c>（实施计划 §5.8）。
/// </summary>
/// <remarks>
/// <para>
/// 管理类操作（改用户、改角色功能点、重置密码、改系统设置、撤销会话）必须留痕：
/// 出问题时能回答「谁在什么时候把什么改成了什么」。
/// </para>
/// <para>
/// 只记管理动作，不记普通读操作——后者量大且无追溯价值，会把表撑爆且淹没真正重要的记录。
/// </para>
/// </remarks>
public sealed class AuditLog
{
    /// <summary>自增主键。</summary>
    public long Id { get; set; }

    /// <summary>操作人用户 Id。</summary>
    public required string UserId { get; set; }

    /// <summary>操作人用户名（冗余保存：用户被删后仍能看出是谁操作的）。</summary>
    public string? Username { get; set; }

    /// <summary>动作，如 <c>user.update</c> / <c>role.functionpoints</c>。</summary>
    public required string Action { get; set; }

    /// <summary>目标对象，如用户 Id 或角色 Id。</summary>
    public string? Target { get; set; }

    /// <summary>变更摘要（不含敏感信息，如密码只记「已重置」）。</summary>
    public string? Detail { get; set; }

    /// <summary>来源 IP。</summary>
    public string? Ip { get; set; }

    /// <summary>时间。</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
