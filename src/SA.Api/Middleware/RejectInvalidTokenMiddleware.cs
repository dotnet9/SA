using SA.Api.Http;
using SA.Contracts.Common;

namespace SA.Api.Middleware;

/// <summary>
/// 拒绝「带了令牌但校验失败」的请求，而不是让它退回匿名。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么必须单独拦一道</b>：公开读接口允许匿名访问（见 <c>PublicEndpointExtensions</c>），
/// 而 JWT 校验失败时框架只会把请求标记为「未认证」，不会终止它——
/// 于是「带了过期/伪造令牌」的请求会以匿名身份继续，拿到<b>不受数据范围限制</b>的公开数据。
/// 对「仅自选」角色来说，这就是「令牌一过期 → 在公开页看到全市场」的范围绕过。
/// </para>
/// <para>
/// <b>只拦带了令牌的请求</b>：完全没带 Authorization 头的请求是正常匿名访问，照常放行。
/// 因此公开页对未登录用户始终可用，行为与这里无关。
/// </para>
/// <para>
/// <b>为什么用中间件而不是 <c>JwtBearerEvents.OnAuthenticationFailed</c></b>：
/// 在认证事件里写响应会与随后的授权中间件冲突（后者发现未认证会再写一次挑战响应，
/// 此时响应已开始写出，实测直接变成 500）。中间件在认证之后短路返回，既不发二次响应，
/// 也不进入授权阶段，语义最清楚。返回 2001 后前端会自动刷新令牌并重试，用户无感。
/// </para>
/// </remarks>
public sealed class RejectInvalidTokenMiddleware(RequestDelegate next)
{
    private const string BearerPrefix = "Bearer ";

    private readonly RequestDelegate _next = next;

    /// <summary>处理请求。</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (CarriedToken(context) && context.User?.Identity?.IsAuthenticated != true)
        {
            await ApiResults.WriteFailAsync(
                context,
                ErrorCode.Unauthenticated,
                "登录已失效，请重新登录",
                context.RequestAborted).ConfigureAwait(false);

            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// 请求是否携带了 Bearer 令牌（无论是否有效）。
    /// </summary>
    /// <remarks>
    /// SignalR 走 <c>?access_token=</c> 查询串（浏览器无法自定义 WebSocket 头），
    /// 那种情况由 Hub 自身的 <c>[Authorize]</c> 负责拒绝，这里只看请求头。
    /// </remarks>
    private static bool CarriedToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        return header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            && header.Length > BearerPrefix.Length;
    }
}
