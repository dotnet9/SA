using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Equity;
using SA.Domain.Authorization;

namespace SA.Api.Endpoints;

/// <summary>
/// 股权结构端点。要求 <c>stock.equity</c> 功能点。
/// </summary>
public static class EquityEndpoints
{
    /// <summary>
    /// 注册股权结构端点。
    /// </summary>
    public static IEndpointRouteBuilder MapEquityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stocks/{code}/equity", GetAsync)
            .WithTags("equity")
            .RequireFunctionPoint(FunctionPointCatalog.StockEquity);

        return app;
    }

    private static async Task<IResult> GetAsync(
        HttpContext context,
        string code,
        EquityService equity,
        CancellationToken cancellationToken)
    {
        var result = await equity.GetAsync(code, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }
}
