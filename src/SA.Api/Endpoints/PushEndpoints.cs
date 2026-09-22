using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Abstractions;
using SA.Application.Alerts;
using SA.Contracts.Alerts;
using SA.Contracts.Common;
using SA.Domain.Authorization;
using SA.Domain.Common;
using SA.Domain.Entities.Alerts;

namespace SA.Api.Endpoints;

/// <summary>
/// Web Push 与提醒偏好端点。
/// </summary>
/// <remarks>
/// 浏览器推送需要三件东西齐全：前端拿到 <b>VAPID 公钥</b> → 用它订阅得到端点与密钥 →
/// 服务端保存订阅并在触发时用<b>私钥签名</b>发送。因此公钥接口是这个流程的第一步，
/// 且它必须能在订阅之前读到（只要求登录，不额外要求功能点）。
/// </remarks>
public static class PushEndpoints
{
    /// <summary>
    /// 注册推送与提醒偏好端点。
    /// </summary>
    public static IEndpointRouteBuilder MapPushEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications/push").WithTags("notifications");

        // 公钥：订阅前必须拿到，且不涉及任何用户数据（本应用无登录，匿名即可）
        group.MapGet("/key", GetKeyAsync);

        group.MapPost("/subscribe", SubscribeAsync);

        group.MapDelete("/subscribe", UnsubscribeAsync);

        group.MapGet("/subscriptions", ListAsync);

        // 提醒偏好（免打扰与渠道开关）：写在服务端才会真正生效
        var settings = app.MapGroup("/api/notifications/settings").WithTags("notifications");
        settings.MapGet(string.Empty, GetSettingsAsync);
        settings.MapPut(string.Empty, SaveSettingsAsync);

        return app;
    }

    /// <summary>VAPID 公钥（base64url）。</summary>
    private static IResult GetKeyAsync(HttpContext context, IPushSender sender) =>
        ApiResults.Ok(context, new PushKeyDto(sender.PublicKey));

    /// <summary>保存浏览器订阅（按端点去重）。</summary>
    private static async Task<IResult> SubscribeAsync(
        HttpContext context,
        PushSubscribeRequest request,
        IPushSubscriptionStore store,
        CancellationToken cancellationToken)
    {
        var userId = LocalUser.Id;
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        if (string.IsNullOrWhiteSpace(request.Endpoint)
            || string.IsNullOrWhiteSpace(request.Keys?.P256dh)
            || string.IsNullOrWhiteSpace(request.Keys?.Auth))
        {
            return ApiResults.Fail(context, ErrorCode.InvalidParameter, "订阅信息不完整（endpoint 与 keys 都是必需的）");
        }

        var id = await store.UpsertAsync(new PushSubscription
        {
            UserId = userId,
            Endpoint = request.Endpoint.Trim(),
            P256dh = request.Keys!.P256dh!.Trim(),
            Auth = request.Keys.Auth!.Trim(),
            UserAgent = Truncate(context.Request.Headers.UserAgent.ToString(), 256),
            Enabled = true,
            CreatedAt = SaTime.Now
        }, cancellationToken).ConfigureAwait(false);

        return ApiResults.Ok(context, id);
    }

    /// <summary>
    /// 取消订阅。
    /// </summary>
    /// <remarks>
    /// DELETE 不允许「推断」请求体，因此必须显式标注 <see cref="FromBodyAttribute"/>；
    /// 仍走请求体而不是查询串，是因为推送端点是很长的 URL（含密钥），放进查询串会进访问日志。
    /// </remarks>
    private static async Task<IResult> UnsubscribeAsync(
        HttpContext context,
        [FromBody] PushUnsubscribeRequest request,
        IPushSubscriptionStore store,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint))
        {
            return ApiResults.Fail(context, ErrorCode.InvalidParameter, "缺少 endpoint");
        }

        var removed = await store.RemoveByEndpointAsync(request.Endpoint.Trim(), cancellationToken).ConfigureAwait(false);
        return ApiResults.Ok(context, removed);
    }

    /// <summary>列出当前用户的订阅（界面上让用户看到「哪台设备还收得到」）。</summary>
    private static async Task<IResult> ListAsync(
        HttpContext context,
        IPushSubscriptionStore store,
        CancellationToken cancellationToken)
    {
        var userId = LocalUser.Id;
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var rows = await store.GetByUserAsync(userId, cancellationToken).ConfigureAwait(false);

        return ApiResults.Ok(context, rows.Select(row => new PushSubscriptionRowDto(
            Id: row.Id,
            // 端点是长 URL 且含密钥信息：界面只展示主机名，避免把端点无意间复制出去
            Host: HostOf(row.Endpoint),
            UserAgent: Shorten(row.UserAgent),
            Enabled: row.Enabled,
            FailCount: row.FailCount,
            LastOkAt: row.LastOkAt is null ? null : SaTime.Format(row.LastOkAt.Value),
            LastError: row.LastError,
            CreatedAt: SaTime.Format(row.CreatedAt))).ToList());
    }

    /// <summary>读取提醒偏好。</summary>
    private static async Task<IResult> GetSettingsAsync(
        HttpContext context,
        ISettingsStore settings,
        CancellationToken cancellationToken)
    {
        var userId = LocalUser.Id;
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var value = await UserNotifySettingsStore.GetAsync(settings, userId, cancellationToken).ConfigureAwait(false);
        return ApiResults.Ok(context, ToDto(value));
    }

    /// <summary>保存提醒偏好。</summary>
    private static async Task<IResult> SaveSettingsAsync(
        HttpContext context,
        NotifySettingsRequest request,
        ISettingsStore settings,
        CancellationToken cancellationToken)
    {
        var userId = LocalUser.Id;
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        // 时刻格式必须校验：写进去一个 "25:00" 会让免打扰永远不生效，且用户看不出原因
        if (!IsValidTime(request.DndFrom) || !IsValidTime(request.DndTo))
        {
            return ApiResults.Fail(context, ErrorCode.InvalidParameter, "免打扰时刻必须是 HH:mm 格式（如 23:00）");
        }

        await UserNotifySettingsStore.SaveAsync(settings, userId, new UserNotifySettings
        {
            DndEnabled = request.DndEnabled,
            DndFrom = request.DndFrom,
            DndTo = request.DndTo,
            DndKeepInbox = request.DndKeepInbox,
            PushEnabled = request.PushEnabled
        }, cancellationToken).ConfigureAwait(false);

        return ApiResults.Ok(context, 1);
    }

    private static NotifySettingsDto ToDto(UserNotifySettings value) =>
        new(value.DndEnabled, value.DndFrom, value.DndTo, value.DndKeepInbox, value.PushEnabled);

    private static bool IsValidTime(string? text) =>
        TimeOnly.TryParseExact(text, "HH:mm", out _);

    /// <summary>取端点主机名（不暴露完整端点与其中的密钥）。</summary>
    private static string HostOf(string endpoint)
    {
        try
        {
            return new Uri(endpoint).Host;
        }
        catch (UriFormatException)
        {
            return "未知端点";
        }
    }

    /// <summary>把 UA 压成便于辨认的设备描述。</summary>
    private static string? Shorten(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        // 只截取前 60 个字符：完整 UA 对用户没有价值，反而会挤满表格
        return userAgent.Length <= 60 ? userAgent : userAgent[..60] + "…";
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
