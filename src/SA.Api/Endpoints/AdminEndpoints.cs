using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Abstractions;
using SA.Application.Admin;
using SA.Contracts.Admin;
using SA.Contracts.Common;
using SA.Domain.Authorization;
using SA.Domain.Entities.System;

namespace SA.Api.Endpoints;

/// <summary>
/// 后台管理端点。数据源监控、用户与权限、会话、系统设置各自要求对应的后台功能点。
/// </summary>
public static class AdminEndpoints
{
    /// <summary>
    /// 注册后台端点。
    /// </summary>
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var sources = app.MapGroup("/api/admin/datasources").WithTags("admin");
        sources.MapGet(string.Empty, GetDataSourcesAsync)
            .RequireFunctionPoint(FunctionPointCatalog.AdminDatasource);

        var users = app.MapGroup("/api/admin/users").WithTags("admin");
        users.MapGet(string.Empty, GetUsersAsync).RequireFunctionPoint(FunctionPointCatalog.AdminUsers);
        users.MapPost(string.Empty, CreateUserAsync).RequireFunctionPoint(FunctionPointCatalog.AdminUsers);
        users.MapPut("/{userId}", UpdateUserAsync).RequireFunctionPoint(FunctionPointCatalog.AdminUsers);
        users.MapDelete("/{userId}", DeleteUserAsync).RequireFunctionPoint(FunctionPointCatalog.AdminUsers);
        users.MapPost("/{userId}/reset-password", ResetPasswordAsync).RequireFunctionPoint(FunctionPointCatalog.AdminUsers);

        var permissions = app.MapGroup("/api/admin/permissions").WithTags("admin");
        permissions.MapGet(string.Empty, GetMatrixAsync).RequireFunctionPoint(FunctionPointCatalog.AdminPermissions);
        permissions.MapPut("/{roleId}/function-points", UpdateFunctionPointsAsync)
            .RequireFunctionPoint(FunctionPointCatalog.AdminPermissions);
        permissions.MapPut("/{roleId}/quotas", UpdateQuotasAsync)
            .RequireFunctionPoint(FunctionPointCatalog.AdminPermissions);

        var security = app.MapGroup("/api/admin/security").WithTags("admin");
        security.MapGet("/sessions", GetSessionsAsync).RequireFunctionPoint(FunctionPointCatalog.AdminSecurity);
        security.MapPost("/sessions/{userId}/revoke", RevokeSessionsAsync)
            .RequireFunctionPoint(FunctionPointCatalog.AdminSecurity);
        security.MapGet("/audit", GetAuditAsync).RequireFunctionPoint(FunctionPointCatalog.AdminSecurity);

        var system = app.MapGroup("/api/admin/system").WithTags("admin");
        system.MapGet("/state", GetStateAsync).RequireFunctionPoint(FunctionPointCatalog.AdminSecurity);
        system.MapPut("/settings", UpdateSettingsAsync).RequireFunctionPoint(FunctionPointCatalog.AdminSecurity);

        return app;
    }

    private static async Task<IResult> GetDataSourcesAsync(
        HttpContext context,
        AdminService admin,
        CancellationToken cancellationToken,
        int taskLimit = 30)
    {
        var result = await admin.GetDataSourcesAsync(taskLimit, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> GetUsersAsync(
        HttpContext context,
        AdminService admin,
        CancellationToken cancellationToken,
        string? keyword = null,
        string? status = null,
        int limit = 200)
    {
        var result = await admin.GetUsersAsync(keyword, status, limit, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> CreateUserAsync(
        HttpContext context,
        UserCreateRequest request,
        AdminService admin,
        IUserStore userStore,
        CancellationToken cancellationToken)
    {
        var actor = context.User.Id();
        if (actor is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await admin.CreateUserAsync(actor, context.User.Username(), request, userStore, cancellationToken)
            .ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> UpdateUserAsync(
        HttpContext context,
        string userId,
        UserUpdateRequest request,
        AdminService admin,
        CancellationToken cancellationToken)
    {
        var actor = context.User.Id();
        if (actor is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await admin.UpdateUserAsync(actor, context.User.Username(), userId, request, cancellationToken)
            .ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> DeleteUserAsync(
        HttpContext context,
        string userId,
        AdminService admin,
        CancellationToken cancellationToken)
    {
        var actor = context.User.Id();
        if (actor is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await admin.DeleteUserAsync(actor, context.User.Username(), userId, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> ResetPasswordAsync(
        HttpContext context,
        string userId,
        PasswordResetRequest request,
        AdminService admin,
        CancellationToken cancellationToken)
    {
        var actor = context.User.Id();
        if (actor is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await admin.ResetPasswordAsync(actor, context.User.Username(), userId, request.NewPassword, cancellationToken)
            .ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> GetMatrixAsync(
        HttpContext context,
        AdminService admin,
        CancellationToken cancellationToken)
    {
        var result = await admin.GetPermissionMatrixAsync(cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> UpdateFunctionPointsAsync(
        HttpContext context,
        string roleId,
        RoleFunctionPointsRequest request,
        AdminService admin,
        CancellationToken cancellationToken)
    {
        var actor = context.User.Id();
        if (actor is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await admin.UpdateRoleFunctionPointsAsync(
            actor, context.User.Username(), roleId, request.Codes ?? [], cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> UpdateQuotasAsync(
        HttpContext context,
        string roleId,
        RoleQuotasRequest request,
        AdminService admin,
        CancellationToken cancellationToken)
    {
        var actor = context.User.Id();
        if (actor is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await admin.UpdateRoleQuotasAsync(
            actor, context.User.Username(), roleId, request.Quotas ?? new Dictionary<string, int>(), cancellationToken)
            .ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> GetSessionsAsync(
        HttpContext context,
        AdminService admin,
        CancellationToken cancellationToken,
        int loginLimit = 100)
    {
        var result = await admin.GetSessionsAsync(loginLimit, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> RevokeSessionsAsync(
        HttpContext context,
        string userId,
        AdminService admin,
        CancellationToken cancellationToken)
    {
        var actor = context.User.Id();
        if (actor is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await admin.RevokeUserSessionsAsync(actor, context.User.Username(), userId, cancellationToken)
            .ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> GetAuditAsync(
        HttpContext context,
        AdminService admin,
        CancellationToken cancellationToken,
        string? userId = null,
        int limit = 100)
    {
        var result = await admin.GetAuditLogsAsync(userId, limit, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> GetStateAsync(
        HttpContext context,
        AdminService admin,
        CancellationToken cancellationToken)
    {
        var result = await admin.GetSystemStateAsync(cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> UpdateSettingsAsync(
        HttpContext context,
        SettingsUpdateRequest request,
        AdminService admin,
        CancellationToken cancellationToken)
    {
        var actor = context.User.Id();
        if (actor is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await admin.UpdateSettingsAsync(
            actor, context.User.Username(), request.Values ?? new Dictionary<string, string>(), cancellationToken)
            .ConfigureAwait(false);
        return ApiResults.From(context, result);
    }
}
