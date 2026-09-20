using Microsoft.AspNetCore.Authorization;
using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Auth;
using SA.Application.Services;
using SA.Contracts.Auth;
using SA.Contracts.Common;
using SA.Contracts.Me;

namespace SA.Api.Endpoints;

/// <summary>
/// 登录、令牌轮换、登出、改密与二次验证端点。
/// 刷新令牌只走 HttpOnly Cookie，访问令牌放响应体（详细设计 §8）。
/// </summary>
public static class AuthEndpoints
{
    /// <summary>
    /// 注册鉴权端点。
    /// </summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("auth");

        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapPost("/refresh", RefreshAsync).AllowAnonymous();
        group.MapPost("/logout", LogoutAsync).AllowAnonymous();
        group.MapPost("/change-password", ChangePasswordAsync);
        group.MapGet("/totp/status", TotpStatusAsync);
        group.MapPost("/totp/setup", SetupTotpAsync);
        group.MapPost("/totp/enable", EnableTotpAsync);
        group.MapPost("/totp/disable", DisableTotpAsync);

        return app;
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        LoginRequest request,
        AuthService auth,
        AuthOptions options,
        CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, context.ClientIp(), context.Device(), cancellationToken).ConfigureAwait(false);
        if (!result.Ok || result.Value is null)
        {
            return ApiResults.From(context, result);
        }

        WriteRefreshCookie(context, options, result.Value.RefreshToken, result.Value.RefreshExpiresAt);
        return ApiResults.Ok(context, result.Value.Response);
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext context,
        AuthService auth,
        AuthOptions options,
        CancellationToken cancellationToken)
    {
        var current = context.Request.Cookies[options.RefreshCookieName];
        var result = await auth.RefreshAsync(current, context.ClientIp(), context.Device(), cancellationToken).ConfigureAwait(false);
        if (!result.Ok || result.Value is null)
        {
            ClearRefreshCookie(context, options);
            return ApiResults.From(context, result);
        }

        WriteRefreshCookie(context, options, result.Value.RefreshToken, result.Value.RefreshExpiresAt);
        return ApiResults.Ok(context, result.Value.Response);
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        AuthService auth,
        AuthOptions options,
        CancellationToken cancellationToken)
    {
        var current = context.Request.Cookies[options.RefreshCookieName];
        var result = await auth.LogoutAsync(current, cancellationToken).ConfigureAwait(false);
        ClearRefreshCookie(context, options);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> ChangePasswordAsync(
        HttpContext context,
        ChangePasswordRequest request,
        AuthService auth,
        AuthOptions options,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await auth.ChangePasswordAsync(userId, request, cancellationToken).ConfigureAwait(false);
        if (result.Ok)
        {
            // 改密后服务端已吊销全部会话，这里同步清掉 Cookie，前端应跳回登录页
            ClearRefreshCookie(context, options);
        }

        return ApiResults.From(context, result);
    }

    private static async Task<IResult> TotpStatusAsync(
        HttpContext context,
        MeService me,
        AuthOptions options,
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

        return ApiResults.Ok(context, new TotpStatusDto(result.Value.TotpEnabled, options.RequireTotp));
    }

    private static async Task<IResult> SetupTotpAsync(
        HttpContext context,
        AuthService auth,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await auth.SetupTotpAsync(userId, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> EnableTotpAsync(
        HttpContext context,
        TotpEnableRequest request,
        AuthService auth,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await auth.EnableTotpAsync(userId, request, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> DisableTotpAsync(
        HttpContext context,
        AuthService auth,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await auth.DisableTotpAsync(userId, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    /// <summary>
    /// 写刷新令牌 Cookie：HttpOnly + SameSite=Lax；HTTPS 下加 Secure（本机 http 调试时不能加，
    /// 否则浏览器直接丢弃）。
    /// </summary>
    private static void WriteRefreshCookie(
        HttpContext context,
        AuthOptions options,
        string token,
        DateTimeOffset expiresAt)
    {
        context.Response.Cookies.Append(
            options.RefreshCookieName,
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/api/auth",
                Expires = expiresAt,
                IsEssential = true
            });
    }

    private static void ClearRefreshCookie(HttpContext context, AuthOptions options) =>
        context.Response.Cookies.Delete(
            options.RefreshCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/api/auth"
            });
}
