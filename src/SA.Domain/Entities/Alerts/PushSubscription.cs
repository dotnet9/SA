namespace SA.Domain.Entities.Alerts;

/// <summary>
/// 浏览器的 Web Push 订阅。对应新增表 <c>PushSubscription</c>。
/// </summary>
/// <remarks>
/// <para>
/// 一个用户可以有多条订阅（手机、平板、多个浏览器各一条），因此按<b>端点</b>去重而不是按用户：
/// 端点由浏览器生成且全局唯一，是这里唯一的自然键。
/// </para>
/// <para>
/// 推送失败要能退化：410/404 表示订阅已失效（用户卸载或清了数据），此时删除该行；
/// 其他失败累加计数，连续失败到阈值后停用该订阅，避免每轮都为死端点付出一次请求。
/// </para>
/// </remarks>
public sealed class PushSubscription
{
    /// <summary>自增主键。</summary>
    public long Id { get; set; }

    /// <summary>订阅所属用户。</summary>
    public required string UserId { get; set; }

    /// <summary>推送端点（浏览器提供的 URL，全局唯一）。</summary>
    public required string Endpoint { get; set; }

    /// <summary>客户端公钥（base64url），用于内容加密。</summary>
    public required string P256dh { get; set; }

    /// <summary>客户端认证密钥（base64url）。</summary>
    public required string Auth { get; set; }

    /// <summary>创建时的 User-Agent（便于用户辨认是哪台设备）。</summary>
    public string? UserAgent { get; set; }

    /// <summary>是否启用（连续失败到阈值后置 false）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>连续失败次数。</summary>
    public int FailCount { get; set; }

    /// <summary>最近一次推送成功时间。</summary>
    public DateTimeOffset? LastOkAt { get; set; }

    /// <summary>最近一次错误摘要。</summary>
    public string? LastError { get; set; }

    /// <summary>创建时间。</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
