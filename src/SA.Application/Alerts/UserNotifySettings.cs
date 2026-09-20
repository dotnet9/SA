using System.Text.Json;
using SA.Application.Abstractions;
using SA.Domain.Common;

namespace SA.Application.Alerts;

/// <summary>
/// 用户的提醒偏好（免打扰与通知渠道）。
/// </summary>
/// <remarks>
/// <para>
/// 存在服务端而不是浏览器本地：免打扰必须在<b>服务端</b>执行才有意义——
/// 浏览器关掉时前端根本没有机会判断「现在该不该响」。渠道开关同理，
/// 它决定服务端要不要为此用户发起 Web Push 请求。
/// </para>
/// <para>
/// 键为 <c>user.notify.{userId}</c>，值为 JSON。用设置表承载而不是新建一张表：
/// 它是纯偏好、按用户一行、读写都是整行覆盖，没有查询需求。
/// </para>
/// </remarks>
public sealed class UserNotifySettings
{
    /// <summary>是否启用免打扰。</summary>
    public bool DndEnabled { get; set; }

    /// <summary>免打扰开始时刻（<c>HH:mm</c>，业务时区）。</summary>
    public string DndFrom { get; set; } = "23:00";

    /// <summary>免打扰结束时刻（<c>HH:mm</c>，业务时区）。</summary>
    public string DndTo { get; set; } = "08:30";

    /// <summary>免打扰期间是否仍写入站内通知（默认是：记录不丢，只是不打扰）。</summary>
    public bool DndKeepInbox { get; set; } = true;

    /// <summary>是否启用浏览器推送（Web Push）。</summary>
    public bool PushEnabled { get; set; } = true;

    /// <summary>
    /// 免打扰是否覆盖当前时刻。
    /// </summary>
    /// <remarks>
    /// 支持跨零点（如 23:00–08:30）：起止时刻大小关系决定是否跨天，
    /// 否则「23:00 到次日 08:30」这类最常见的设置会被判成永不生效。
    /// </remarks>
    public bool InDndWindow(DateTimeOffset now)
    {
        if (!DndEnabled)
        {
            return false;
        }

        var from = ParseTime(DndFrom);
        var to = ParseTime(DndTo);
        if (from is null || to is null || from == to)
        {
            return false;
        }

        var current = TimeOnly.FromDateTime(SaTime.ToLocal(now));

        return from < to
            ? current >= from && current < to
            : current >= from || current < to;
    }

    private static TimeOnly? ParseTime(string? text) =>
        TimeOnly.TryParse(text, out var parsed) ? parsed : null;
}

/// <summary>提醒偏好的读写。</summary>
public static class UserNotifySettingsStore
{
    /// <summary>取用户的提醒偏好（未配置时返回默认值）。</summary>
    public static async Task<UserNotifySettings> GetAsync(
        ISettingsStore settings,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var json = await settings.GetAppSettingAsync(KeyOf(userId), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new UserNotifySettings();
        }

        try
        {
            return JsonSerializer.Deserialize<UserNotifySettings>(json) ?? new UserNotifySettings();
        }
        catch (JsonException)
        {
            // 配置坏了就退回默认值：提醒链路不该因为一条坏 JSON 而整体失效
            return new UserNotifySettings();
        }
    }

    /// <summary>写入用户的提醒偏好。</summary>
    public static Task SaveAsync(
        ISettingsStore settings,
        string userId,
        UserNotifySettings value,
        CancellationToken cancellationToken = default) =>
        settings.SetAppSettingAsync(KeyOf(userId), JsonSerializer.Serialize(value), cancellationToken);

    /// <summary>取设置键。</summary>
    public static string KeyOf(string userId) => $"user.notify.{userId}";
}
