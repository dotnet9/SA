using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Abstractions;
using SA.Application.Events;
using SA.Contracts.Common;
using SA.Contracts.Events;
using SA.Domain.Authorization;
using SA.Domain.Common;
using SA.Domain.Entities.Events;

namespace SA.Api.Endpoints;

/// <summary>
/// 事件与影响端点。读取要求 <c>stock.events</c>，人工标注要求 <c>event.edit</c>。
/// </summary>
public static class EventEndpoints
{
    /// <summary>
    /// 注册事件端点。
    /// </summary>
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stocks/{code}").WithTags("events");

        group.MapGet("/events", GetAsync)
            .RequireFunctionPoint(FunctionPointCatalog.StockEvents);

        group.MapPut("/events/annotation", AnnotateAsync)
            .RequireFunctionPoint(FunctionPointCatalog.EventEdit);

        group.MapDelete("/events/annotation/{eventKey}", RemoveAnnotationAsync)
            .RequireFunctionPoint(FunctionPointCatalog.EventEdit);

        return app;
    }

    /// <summary>事件时间线与四张拓扑图。</summary>
    private static async Task<IResult> GetAsync(
        HttpContext context,
        string code,
        EventTimelineService events,
        SA.Application.Authorization.PermissionService permissions,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        var canAnnotate = false;

        if (userId is not null)
        {
            var resolved = await permissions.ResolveAsync(userId, cancellationToken).ConfigureAwait(false);
            canAnnotate = resolved?.Has(FunctionPointCatalog.EventEdit) ?? false;
        }

        var result = await events.GetAsync(code, canAnnotate, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    /// <summary>
    /// 写入人工标注（覆盖派生判读）。
    /// </summary>
    private static async Task<IResult> AnnotateAsync(
        HttpContext context,
        string code,
        EventAnnotationRequest request,
        IEventAnnotationStore store,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        if (string.IsNullOrWhiteSpace(request.EventKey))
        {
            return ApiResults.Fail(context, ErrorCode.InvalidParameter, "事件键不能为空");
        }

        var tone = request.Tone?.Trim().ToLowerInvariant();
        if (tone is not ("up" or "down" or "neutral"))
        {
            return ApiResults.Fail(context, ErrorCode.InvalidParameter, "影响方向只能是 up / down / neutral");
        }

        if (request.Impact is < 1 or > 5)
        {
            return ApiResults.Fail(context, ErrorCode.InvalidParameter, "影响强度必须在 1–5 之间");
        }

        await store.UpsertAsync(new EventAnnotation
        {
            Code = code,
            EventKey = request.EventKey.Trim(),
            Tone = tone,
            Impact = request.Impact,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            UserId = userId,
            UpdatedAt = SaTime.Now
        }, cancellationToken).ConfigureAwait(false);

        return ApiResults.Ok(context, 1);
    }

    /// <summary>删除人工标注，恢复为派生判读。</summary>
    private static async Task<IResult> RemoveAnnotationAsync(
        HttpContext context,
        string code,
        string eventKey,
        IEventAnnotationStore store,
        CancellationToken cancellationToken)
    {
        var removed = await store.RemoveAsync(code, eventKey, cancellationToken).ConfigureAwait(false);
        return ApiResults.Ok(context, removed);
    }
}
