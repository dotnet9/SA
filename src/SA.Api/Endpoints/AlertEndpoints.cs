using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Abstractions;
using SA.Application.Alerts;
using SA.Contracts.Alerts;
using SA.Contracts.Common;
using SA.Domain.Authorization;
using SA.Domain.Entities.Identity;

namespace SA.Api.Endpoints;

/// <summary>
/// 提醒与通知端点。规则管理要求 <c>alert.manage</c>，通知查看要求 <c>notify.view</c>。
/// </summary>
public static class AlertEndpoints
{
    /// <summary>
    /// 注册提醒与通知端点。
    /// </summary>
    public static IEndpointRouteBuilder MapAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var alerts = app.MapGroup("/api/alerts").WithTags("alerts");
        alerts.MapGet(string.Empty, GetRulesAsync);
        alerts.MapPost(string.Empty, CreateRuleAsync);
        alerts.MapPut("/{ruleId}", UpdateRuleAsync);
        alerts.MapDelete("/{ruleId}", RemoveRuleAsync);

        var notifications = app.MapGroup("/api/notifications").WithTags("notifications");
        notifications.MapGet(string.Empty, GetNotificationsAsync);
        notifications.MapPost("/read", MarkReadAsync);

        return app;
    }

    private static async Task<IResult> GetRulesAsync(
        HttpContext context,
        AlertService alerts,
        SA.Application.Authorization.PermissionService permissions,
        CancellationToken cancellationToken)
    {
        var userId = LocalUser.Id;
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var resolved = await permissions.ResolveAsync(userId, cancellationToken).ConfigureAwait(false);
        var quota = resolved is not null && resolved.Quotas.TryGetValue(QuotaKeys.AlertMax, out var value) ? value : 0;

        var result = await alerts.GetRulesAsync(userId, quota, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> CreateRuleAsync(
        HttpContext context,
        AlertRuleCreateRequest request,
        AlertService alerts,
        SA.Application.Authorization.PermissionService permissions,
        CancellationToken cancellationToken)
    {
        var userId = LocalUser.Id;
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var resolved = await permissions.ResolveAsync(userId, cancellationToken).ConfigureAwait(false);
        var quota = resolved is not null && resolved.Quotas.TryGetValue(QuotaKeys.AlertMax, out var value) ? value : 0;

        var result = await alerts.CreateAsync(userId, request, quota, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> UpdateRuleAsync(
        HttpContext context,
        string ruleId,
        AlertRuleUpdateRequest request,
        AlertService alerts,
        CancellationToken cancellationToken)
    {
        var userId = LocalUser.Id;
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await alerts.UpdateAsync(userId, ruleId, request, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> RemoveRuleAsync(
        HttpContext context,
        string ruleId,
        AlertService alerts,
        CancellationToken cancellationToken)
    {
        var userId = LocalUser.Id;
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await alerts.RemoveAsync(userId, ruleId, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> GetNotificationsAsync(
        HttpContext context,
        AlertService alerts,
        CancellationToken cancellationToken,
        bool unreadOnly = false,
        int limit = 50)
    {
        var userId = LocalUser.Id;
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await alerts.GetNotificationsAsync(userId, unreadOnly, limit, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> MarkReadAsync(
        HttpContext context,
        SA.Contracts.Alerts.NotificationReadRequest request,
        AlertService alerts,
        CancellationToken cancellationToken)
    {
        var userId = LocalUser.Id;
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await alerts.MarkReadAsync(userId, request.Ids, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }
}
