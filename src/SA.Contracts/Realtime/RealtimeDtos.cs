namespace SA.Contracts.Realtime;

/// <summary>
/// 行情推送载荷。字段与详细设计 §7.2 的 <c>Quote</c> 一致。
/// </summary>
/// <remarks>
/// <b>单位与自选列表接口保持一致</b>：<see cref="Amount"/> 为亿元、<see cref="Volume"/> 为手、比率为百分数。
/// 早期版本这里返回的是「元」，而界面按「亿元」渲染，导致实时行的成交额被放大一亿倍；
/// 推送与列表两个来源的口径必须一致，否则同一张表里会混着两种单位的数字。
/// </remarks>
/// <param name="Code">证券代码。</param>
/// <param name="Price">最新价（元）。</param>
/// <param name="Chg">涨跌额（元）。</param>
/// <param name="Pct">涨跌幅（百分数）。</param>
/// <param name="Volume">成交量（手）。</param>
/// <param name="Amount">成交额（<b>亿元</b>）。</param>
/// <param name="Turnover">换手率（百分数）。</param>
/// <param name="VolRatio">量比。</param>
/// <param name="AsOf">行情时间（<c>yyyy-MM-dd HH:mm:ss</c>）。</param>
public sealed record QuotePushDto(
    string Code,
    decimal Price,
    decimal Chg,
    decimal Pct,
    decimal Volume,
    decimal Amount,
    decimal Turnover,
    decimal? VolRatio,
    string? AsOf);

/// <summary>
/// 提醒触发推送载荷（详细设计 §7.2 的 <c>AlertTriggered</c>），第 11 批接入。
/// </summary>
/// <param name="RuleId">规则 Id。</param>
/// <param name="Code">证券代码。</param>
/// <param name="Name">证券名称。</param>
/// <param name="Title">标题。</param>
/// <param name="Body">正文。</param>
/// <param name="Value">触发值。</param>
/// <param name="RuleType">规则类型。</param>
/// <param name="TriggeredAt">触发时间。</param>
public sealed record AlertTriggeredDto(
    string RuleId,
    string Code,
    string? Name,
    string Title,
    string Body,
    string? Value,
    string? RuleType,
    string TriggeredAt);

/// <summary>
/// 站内通知推送载荷（详细设计 §7.2 的 <c>Notification</c>），第 11 批接入。
/// </summary>
/// <param name="Id">通知 Id。</param>
/// <param name="Title">标题。</param>
/// <param name="Body">正文。</param>
/// <param name="Level">级别：up / down / info。</param>
/// <param name="Code">相关证券代码。</param>
/// <param name="CreatedAt">创建时间。</param>
public sealed record NotificationPushDto(
    long Id,
    string Title,
    string Body,
    string Level,
    string? Code,
    string CreatedAt);

/// <summary>
/// 系统提示（如数据源降级），详细设计 §7.2 的 <c>SystemNotice</c>。
/// </summary>
/// <param name="Level">级别：info / warn / err。</param>
/// <param name="Message">提示文案。</param>
public sealed record SystemNoticeDto(string Level, string Message);

/// <summary>
/// 推送节流间隔的允许取值（详细设计 §7.1：3 / 5 / 10 秒）。
/// </summary>
public static class PushIntervals
{
    /// <summary>可选间隔（秒）。</summary>
    public static IReadOnlyList<int> Allowed { get; } = [3, 5, 10];

    /// <summary>默认间隔（秒）。</summary>
    public const int Default = 3;

    /// <summary>把任意输入收敛到允许的取值。</summary>
    public static int Normalize(int seconds)
    {
        foreach (var candidate in Allowed)
        {
            if (seconds <= candidate)
            {
                return candidate;
            }
        }

        return Allowed[^1];
    }
}
