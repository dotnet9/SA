using Microsoft.AspNetCore.Mvc;
using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Authorization;
using SA.Application.Watchlist;
using SA.Contracts.Common;
using SA.Contracts.Watchlist;
using SA.Domain.Authorization;

namespace SA.Api.Endpoints;

/// <summary>
/// 自选股端点。读取要求 <c>watchlist.view</c>，增删改要求 <c>watchlist.edit</c>；
/// 数量上限取自角色配额 <c>watchlist.max</c>（需求规格 §7.2）。
/// </summary>
public static class WatchlistEndpoints
{
    /// <summary>
    /// 注册自选端点。
    /// </summary>
    public static IEndpointRouteBuilder MapWatchlistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/watchlist").WithTags("watchlist");

        // 列表与分组：只读
        group.MapGet("/groups", GetGroupsAsync)
            .RequireFunctionPoint(FunctionPointCatalog.WatchlistView);

        group.MapGet(string.Empty, GetAsync)
            .RequireFunctionPoint(FunctionPointCatalog.WatchlistView);

        // 增删改：需要编辑权限
        group.MapPost("/items", AddAsync)
            .RequireFunctionPoint(FunctionPointCatalog.WatchlistEdit);

        group.MapDelete("/items", RemoveAsync)
            .RequireFunctionPoint(FunctionPointCatalog.WatchlistEdit);

        group.MapPost("/groups", CreateGroupAsync)
            .RequireFunctionPoint(FunctionPointCatalog.WatchlistEdit);

        group.MapDelete("/groups/{groupId}", RemoveGroupAsync)
            .RequireFunctionPoint(FunctionPointCatalog.WatchlistEdit);

        group.MapPut("/order", ReorderAsync)
            .RequireFunctionPoint(FunctionPointCatalog.WatchlistEdit);

        return app;
    }

    /// <summary>自选列表。</summary>
    private static async Task<IResult> GetAsync(
        HttpContext context,
        WatchlistService watchlist,
        PermissionService permissions,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var resolved = await permissions.ResolveAsync(userId, cancellationToken).ConfigureAwait(false);
        if (resolved is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await watchlist.GetAsync(
            userId,
            resolved.Has(FunctionPointCatalog.WatchlistEdit),
            Quota(resolved, QuotaKeys.WatchlistMax),
            cancellationToken).ConfigureAwait(false);

        return ApiResults.From(context, result);
    }

    /// <summary>分组列表（与列表接口同源，便于界面只刷分组）。</summary>
    private static async Task<IResult> GetGroupsAsync(
        HttpContext context,
        WatchlistService watchlist,
        PermissionService permissions,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var resolved = await permissions.ResolveAsync(userId, cancellationToken).ConfigureAwait(false);
        if (resolved is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await watchlist.GetAsync(
            userId,
            resolved.Has(FunctionPointCatalog.WatchlistEdit),
            Quota(resolved, QuotaKeys.WatchlistMax),
            cancellationToken).ConfigureAwait(false);

        if (!result.Ok || result.Value is null)
        {
            return ApiResults.From(context, result);
        }

        return ApiResults.Ok(context, result.Value.Groups);
    }

    private static async Task<IResult> AddAsync(
        HttpContext context,
        WatchAddRequest request,
        WatchlistService watchlist,
        PermissionService permissions,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var resolved = await permissions.ResolveAsync(userId, cancellationToken).ConfigureAwait(false);
        var quota = resolved is null ? 0 : Quota(resolved, QuotaKeys.WatchlistMax);

        var result = await watchlist.AddAsync(userId, request, quota, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    /// <summary>
    /// 批量移除自选。
    /// </summary>
    /// <remarks>
    /// <c>MapDelete</c> 不允许「推断」请求体，因此这里必须显式标注 <see cref="FromBodyAttribute"/>；
    /// 批量删除仍然走请求体，避免把几十个代码堆进查询串。
    /// </remarks>
    private static async Task<IResult> RemoveAsync(
        HttpContext context,
        [FromBody] WatchRemoveRequest request,
        WatchlistService watchlist,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await watchlist.RemoveAsync(userId, request, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> CreateGroupAsync(
        HttpContext context,
        WatchGroupCreateRequest request,
        WatchlistService watchlist,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await watchlist.CreateGroupAsync(userId, request, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> RemoveGroupAsync(
        HttpContext context,
        string groupId,
        WatchlistService watchlist,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await watchlist.RemoveGroupAsync(userId, groupId, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> ReorderAsync(
        HttpContext context,
        WatchReorderRequest request,
        WatchlistService watchlist,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await watchlist.ReorderAsync(userId, request, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static int Quota(PermissionService.ResolvedPermissions resolved, string key) =>
        resolved.Quotas.TryGetValue(key, out var value) ? value : 0;
}

/// <summary>
/// 操作级权限参数的键名（与详细设计 §3.1 的 <c>RoleQuota.QuotaKey</c> 一致）。
/// </summary>
internal static class QuotaKeys
{
    /// <summary>自选数量上限。</summary>
    public const string WatchlistMax = "watchlist.max";
}
