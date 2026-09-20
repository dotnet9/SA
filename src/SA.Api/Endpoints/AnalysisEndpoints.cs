using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Analysis;
using SA.Domain.Authorization;

namespace SA.Api.Endpoints;

/// <summary>
/// 规则引擎推算端点：行业景气度、个股因果链与传导带宽。
/// </summary>
/// <remarks>
/// 这两个接口共用 <c>stock.industry</c>（行业）与 <c>stock.trend</c>（传导带宽依赖日线）的语义，
/// 这里按更贴切的归属分别校验：景气度属行业视图，因果链属个股趋势视图。
/// </remarks>
public static class AnalysisEndpoints
{
    /// <summary>
    /// 注册规则引擎推算端点。
    /// </summary>
    public static IEndpointRouteBuilder MapAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/industry/prosperity", GetProsperityAsync)
            .WithTags("analysis")
            .RequireFunctionPoint(FunctionPointCatalog.StockIndustry);

        app.MapGet("/api/stocks/{code}/causal", GetCausalAsync)
            .WithTags("analysis")
            .RequireFunctionPoint(FunctionPointCatalog.StockTrend);

        return app;
    }

    /// <summary>行业景气度排行。<c>take</c> 为条数上限（默认 30，0 表示全部）。</summary>
    private static async Task<IResult> GetProsperityAsync(
        HttpContext context,
        ProsperityService prosperity,
        CancellationToken cancellationToken,
        int take = 30)
    {
        var result = await prosperity.GetRankAsync(Math.Max(0, take), cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    /// <summary>个股因果链与传导带宽。</summary>
    private static async Task<IResult> GetCausalAsync(
        HttpContext context,
        string code,
        CausalChainService causal,
        CancellationToken cancellationToken)
    {
        var result = await causal.GetAsync(code, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }
}
