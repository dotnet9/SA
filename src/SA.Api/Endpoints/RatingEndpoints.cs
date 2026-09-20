using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Rating;
using SA.Domain.Authorization;

namespace SA.Api.Endpoints;

/// <summary>
/// 机构评级端点。要求 <c>stock.rating</c> 功能点。
/// </summary>
public static class RatingEndpoints
{
    /// <summary>
    /// 注册评级端点。
    /// </summary>
    public static IEndpointRouteBuilder MapRatingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stocks/{code}/rating", GetAsync)
            .WithTags("rating")
            .RequireFunctionPoint(FunctionPointCatalog.StockRating);

        return app;
    }

    private static async Task<IResult> GetAsync(
        HttpContext context,
        string code,
        RatingService rating,
        CancellationToken cancellationToken)
    {
        var result = await rating.GetAsync(code, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }
}
