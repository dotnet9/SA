using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Capital;
using SA.Domain.Authorization;

namespace SA.Api.Endpoints;

/// <summary>
/// 资金面与筹码端点。要求 <c>stock.capital</c> 功能点。
/// </summary>
public static class CapitalEndpoints
{
    /// <summary>
    /// 注册资金面端点。
    /// </summary>
    public static IEndpointRouteBuilder MapCapitalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stocks/{code}/capital", GetAsync)
            .WithTags("capital")
            .AllowPublicRead();

        return app;
    }

    private static async Task<IResult> GetAsync(
        HttpContext context,
        string code,
        CapitalService capital,
        CancellationToken cancellationToken)
    {
        var result = await capital.GetAsync(code, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }
}
