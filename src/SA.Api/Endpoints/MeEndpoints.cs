using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Services;
using SA.Contracts.Common;
using SA.Contracts.Me;

namespace SA.Api.Endpoints;

/// <summary>
/// 当前用户端点：权限快照与个人设置。前端启动时靠这些接口决定路由与菜单裁剪
/// （需求规格 §7.3）。
/// </summary>
public static class MeEndpoints
{
    /// <summary>
    /// 注册当前用户端点。
    /// </summary>
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me").WithTags("me");

        group.MapGet(string.Empty, GetMeAsync);
        group.MapGet("/permissions", GetPermissionsAsync);
        group.MapGet("/settings", GetSettingsAsync);
        group.MapPut("/settings", UpdateSettingsAsync);

        return app;
    }

    private static async Task<IResult> GetMeAsync(
        HttpContext context,
        MeService me,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await me.GetMeAsync(userId, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    /// <summary>
    /// 只返回权限相关字段，供前端在权限矩阵变更后轻量刷新。
    /// </summary>
    private static async Task<IResult> GetPermissionsAsync(
        HttpContext context,
        MeService me,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await me.GetMeAsync(userId, cancellationToken).ConfigureAwait(false);
        if (!result.Ok || result.Value is null)
        {
            return ApiResults.From(context, result);
        }

        var dto = result.Value;
        return ApiResults.Ok(context, new PermissionsDto(
            dto.RoleId,
            dto.RoleName,
            dto.DataScope,
            dto.FunctionPoints,
            dto.Quotas,
            dto.MustChangePwd));
    }

    private static async Task<IResult> GetSettingsAsync(
        HttpContext context,
        MeService me,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await me.GetSettingsAsync(userId, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> UpdateSettingsAsync(
        HttpContext context,
        System.Text.Json.JsonElement payload,
        MeService me,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await me.UpdateSettingsAsync(userId, payload, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }
}
