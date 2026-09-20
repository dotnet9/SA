using System.Reflection;
using SA.Api.Http;
using SA.Contracts.Common;
using SA.Domain.Common;

namespace SA.Api.Endpoints;

/// <summary>
/// 系统与运维端点。健康检查不要求鉴权，供启动自检与后续反代探活使用。
/// </summary>
public static class SystemEndpoints
{
    /// <summary>
    /// 注册系统端点。
    /// </summary>
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("system");

        group.MapGet("/health", (HttpContext context, IHostEnvironment environment) =>
            ApiResults.Ok(context, new HealthDto(
                "ok",
                Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0",
                environment.EnvironmentName,
                SaTime.Format(SaTime.Now))));

        return app;
    }
}
