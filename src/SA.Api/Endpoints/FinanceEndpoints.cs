using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Finance;
using SA.Domain.Authorization;

namespace SA.Api.Endpoints;

/// <summary>
/// 财务（盈利与财务表现）端点。要求 <c>stock.finance</c> 功能点。
/// </summary>
public static class FinanceEndpoints
{
    /// <summary>
    /// 注册财务端点。
    /// </summary>
    public static IEndpointRouteBuilder MapFinanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stocks/{code}/finance", GetAsync)
            .WithTags("finance")
            .RequireFunctionPoint(FunctionPointCatalog.StockFinance);

        return app;
    }

    private static async Task<IResult> GetAsync(
        HttpContext context,
        string code,
        FinanceService finance,
        CancellationToken cancellationToken)
    {
        var result = await finance.GetAsync(code, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }
}
