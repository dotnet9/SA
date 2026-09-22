using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Analysis;
using SA.Application.Research;
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

        // 价值研究（实施计划 §6.4）：全部公开读，与个股其它模块一致
        group.MapGet("/value-research", GetValueResearchAsync);
        group.MapGet("/fundamental-history", GetFundamentalHistoryAsync);
        group.MapGet("/business-composition", GetBusinessCompositionAsync);
        group.MapGet("/share-structure", GetShareStructureAsync);
        group.MapGet("/announcements", GetAnnouncementsAsync);
        group.MapGet("/research", GetResearchAsync);

        return app;
    }

    private static async Task<IResult> GetValueResearchAsync(
        HttpContext context,
        string code,
        ValueResearchService research,
        CancellationToken cancellationToken)
    {
        var result = await research.GetAsync(code, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    /// <summary>历史财务序列。查询参数 <c>years</c> 为回看年数（默认 10）。</summary>
    private static async Task<IResult> GetFundamentalHistoryAsync(
        HttpContext context,
        string code,
        ValueResearchService research,
        CancellationToken cancellationToken,
        int years = 10)
    {
        var result = await research.GetHistoryAsync(code, years, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> GetBusinessCompositionAsync(
        HttpContext context,
        string code,
        ValueResearchService research,
        CancellationToken cancellationToken)
    {
        var result = await research.GetCompositionAsync(code, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> GetShareStructureAsync(
        HttpContext context,
        string code,
        ValueResearchService research,
        CancellationToken cancellationToken)
    {
        var result = await research.GetShareStructureAsync(code, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    /// <summary>公告列表。查询参数 <c>type</c> 按类型过滤，<c>limit</c> 为条数上限。</summary>
    private static async Task<IResult> GetAnnouncementsAsync(
        HttpContext context,
        string code,
        ValueResearchService research,
        CancellationToken cancellationToken,
        string? type = null,
        int limit = 200)
    {
        var result = await research.GetAnnouncementsAsync(code, type, limit, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    /// <summary>研报列表（不含目标价）。</summary>
    private static async Task<IResult> GetResearchAsync(
        HttpContext context,
        string code,
        ValueResearchService research,
        CancellationToken cancellationToken,
        int limit = 50)
    {
        var result = await research.GetReportsAsync(code, limit, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
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
