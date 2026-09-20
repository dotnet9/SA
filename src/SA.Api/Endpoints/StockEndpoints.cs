using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Analysis;
using SA.Application.Stocks;
using SA.Contracts.Common;
using SA.Domain.Authorization;

namespace SA.Api.Endpoints;

/// <summary>
/// 个股端点。总览与趋势分别对应 <c>stock.trend</c> 功能点中的「模块访问」权限；
/// 各专注页在所属批次接入时各自声明功能点。
/// </summary>
public static class StockEndpoints
{
    /// <summary>
    /// 注册个股端点。
    /// </summary>
    public static IEndpointRouteBuilder MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stocks/{code}")
            .WithTags("stocks")
            .RequireFunctionPoint(FunctionPointCatalog.StockTrend);

        // 总览页把 8 个模块的入口聚在一起，能进来就看到全局，因此按最宽的趋势权限放行；
        // 每个模块页自己再校验对应功能点
        group.MapGet(string.Empty, GetProfileAsync);
        group.MapGet("/quote", GetProfileAsync);
        group.MapGet("/overview", GetOverviewAsync);
        group.MapGet("/freshness", GetFreshnessAsync);
        group.MapGet("/trend", GetTrendAsync);

        return app;
    }

    private static async Task<IResult> GetProfileAsync(
        HttpContext context,
        string code,
        StockService stocks,
        CancellationToken cancellationToken)
    {
        var result = await stocks.GetProfileAsync(code, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> GetOverviewAsync(
        HttpContext context,
        string code,
        StockService stocks,
        CancellationToken cancellationToken)
    {
        var result = await stocks.GetOverviewAsync(code, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> GetFreshnessAsync(
        HttpContext context,
        string code,
        StockService stocks,
        CancellationToken cancellationToken)
    {
        var result = await stocks.GetFreshnessAsync(code, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    /// <summary>
    /// 趋势与价格结构。查询参数 <c>limit</c> 为 K 线条数（20–240，默认 120）。
    /// </summary>
    private static async Task<IResult> GetTrendAsync(
        HttpContext context,
        string code,
        StockService stocks,
        CancellationToken cancellationToken,
        int limit = 120)
    {
        var result = await stocks.GetTrendAsync(code, limit, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }
}
