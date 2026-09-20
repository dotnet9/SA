using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using SA.Contracts.Common;
using SA.Infrastructure.Security;

namespace SA.Api.Auth;

/// <summary>
/// 功能点要求。接口按功能点编码声明所需权限，由 <see cref="FunctionPointAuthorizationHandler"/> 判定
/// （需求规格 §7.3：后端逐接口校验，前端裁剪只用于体验）。
/// </summary>
public sealed class FunctionPointRequirement(params string[] codes) : IAuthorizationRequirement
{
    /// <summary>所需功能点编码（全部满足才放行）。</summary>
    public IReadOnlyList<string> Codes { get; } = codes;
}

/// <summary>
/// 功能点授权判定。功能点来自访问令牌中的多值声明 <c>fp</c>，无需每请求查库。
/// </summary>
public sealed class FunctionPointAuthorizationHandler : AuthorizationHandler<FunctionPointRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        FunctionPointRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        var granted = context.User
            .FindAll(SaClaims.FunctionPoint)
            .SelectMany(c => c.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);

        if (requirement.Codes.All(granted.Contains))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// 授权失败的统一响应：401 未登录 → <c>2001</c>，403 无权限 → <c>2002</c>，
/// 并带上缺失的功能点，便于排查「看不到但能调」之外的反向问题（有权限但被挡）。
/// </summary>
public sealed class SaAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    /// <inheritdoc />
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            await WriteAsync(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录", 401);
            return;
        }

        if (authorizeResult.Forbidden)
        {
            var required = policy.Requirements
                .OfType<FunctionPointRequirement>()
                .SelectMany(r => r.Codes)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var message = required.Count > 0
                ? $"当前账号缺少功能权限：{string.Join("、", required)}"
                : "当前账号无权访问该资源";

            await WriteAsync(context, ErrorCode.NoPermission, message, 403);
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }

    private static Task WriteAsync(HttpContext context, ErrorCode code, string message, int statusCode)
    {
        var traceId = context.Items[Middleware.TraceIdMiddleware.ItemsKey] as string ?? context.TraceIdentifier;
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(ApiResponse.Fail<object>(code, message, traceId));
    }
}

/// <summary>
/// 当前用户信息的读取入口，统一从声明取值，避免各处自行解析。
/// </summary>
public static class CurrentUser
{
    /// <summary>取当前用户 Id；未登录返回 null。</summary>
    public static string? Id(this ClaimsPrincipal principal) =>
        principal.FindFirst(SaClaims.Subject)?.Value;

    /// <summary>取当前用户的数据范围（all / watchlist）。</summary>
    public static string DataScope(this ClaimsPrincipal principal) =>
        principal.FindFirst(SaClaims.DataScope)?.Value ?? "watchlist";

    /// <summary>取当前登录名。</summary>
    public static string? Username(this ClaimsPrincipal principal) =>
        principal.FindFirst(SaClaims.Username)?.Value;

    /// <summary>取设备标识（User-Agent 摘要），用于会话列表展示。</summary>
    public static string? Device(this HttpContext context)
    {
        var agent = context.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(agent))
        {
            return null;
        }

        return agent.Length <= 200 ? agent : agent[..200];
    }

    /// <summary>取来源 IP。</summary>
    public static string? ClientIp(this HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString();
}

/// <summary>
/// 端点扩展：声明所需功能点。
/// </summary>
public static class AuthorizationEndpointExtensions
{
    /// <summary>
    /// 要求调用方具备全部给定功能点。
    /// </summary>
    public static RouteHandlerBuilder RequireFunctionPoint(this RouteHandlerBuilder builder, params string[] codes)
    {
        ArgumentNullException.ThrowIfNull(codes);
        if (codes.Length == 0)
        {
            throw new ArgumentException("至少要声明一个功能点", nameof(codes));
        }

        return builder.RequireAuthorization(policy => policy.Requirements.Add(new FunctionPointRequirement(codes)));
    }
}
