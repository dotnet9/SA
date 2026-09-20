using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Risk;
using SA.Domain.Authorization;

namespace SA.Api.Endpoints;

/// <summary>
/// 风险与舆情监控端点。要求 <c>stock.risk</c> 功能点。
/// </summary>
public static class RiskEndpoints
{
    /// <summary>
    /// 注册风险端点。
    /// </summary>
    public static IEndpointRouteBuilder MapRiskEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stocks/{code}/risk", GetAsync)
            .WithTags("risk")
            .RequireFunctionPoint(FunctionPointCatalog.StockRisk);

        return app;
    }

    private static async Task<IResult> GetAsync(
        HttpContext context,
        string code,
        RiskService risk,
        CancellationToken cancellationToken)
    {
        var result = await risk.GetAsync(code, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }
}
