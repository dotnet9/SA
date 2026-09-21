using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Analysis;
using SA.Application.Stocks;
using SA.Contracts.Common;
using SA.Domain.Authorization;

namespace SA.Api.Endpoints;

/// <summary>
/// 个股端点（总览 / 行情条 / 新鲜度 / 趋势）。
/// </summary>
/// <remarks>
/// <b>公开读接口</b>：个股资料与行情属于公开信息，不登录也能查阅，
/// 这样搜索引擎与分享出去的个股链接都能直接打开。
/// </remarks>
public static class StockEndpoints
{
    /// <summary>
    /// 注册个股端点。
    /// </summary>
    public static IEndpointRouteBuilder MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stocks/{code}")
            .WithTags("stocks")
            .AllowPublicRead();

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
