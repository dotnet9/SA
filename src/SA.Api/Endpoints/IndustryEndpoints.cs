using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Industry;
using SA.Domain.Authorization;

namespace SA.Api.Endpoints;

/// <summary>
/// 行业与同业对比端点。要求 <c>stock.industry</c> 功能点。
/// </summary>
public static class IndustryEndpoints
{
    /// <summary>
    /// 注册行业对比端点。
    /// </summary>
    public static IEndpointRouteBuilder MapIndustryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stocks/{code}/industry", GetAsync)
            .WithTags("industry")
            .AllowPublicRead();

        return app;
    }

    private static async Task<IResult> GetAsync(
        HttpContext context,
        string code,
        IndustryService industry,
        CancellationToken cancellationToken)
    {
        var result = await industry.GetAsync(code, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }
}
