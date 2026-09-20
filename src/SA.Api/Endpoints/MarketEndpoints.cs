using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Market;
using SA.Contracts.Common;
using SA.Contracts.Market;
using SA.Domain.Authorization;

namespace SA.Api.Endpoints;

/// <summary>
/// 市场概览端点（需求规格 FR-MKT 系列）。全部要求 <c>market.view</c> 功能点，
/// 前端裁剪只负责体验，权限判定以后端为准（需求规格 §7.3）。
/// </summary>
public static class MarketEndpoints
{
    /// <summary>
    /// 注册市场端点。
    /// </summary>
    public static IEndpointRouteBuilder MapMarketEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/market")
            .WithTags("market")
            .RequireFunctionPoint(FunctionPointCatalog.MarketView);

        group.MapGet("/overview", GetOverviewAsync);
        group.MapGet("/indices", GetIndicesAsync);
        group.MapGet("/breadth", GetBreadthAsync);
        group.MapGet("/fundflow", GetFundFlowAsync);
        group.MapGet("/sectors", GetSectorsAsync);
        group.MapGet("/rankings", GetRankingsAsync);
        group.MapGet("/status", GetStatusAsync);

        return app;
    }

    /// <summary>市场概览聚合（首屏一次请求）。</summary>
    private static async Task<IResult> GetOverviewAsync(
        HttpContext context,
        MarketService market,
        CancellationToken cancellationToken)
    {
        var result = await market.GetOverviewAsync(cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    /// <summary>指数卡。</summary>
    private static async Task<IResult> GetIndicesAsync(
        HttpContext context,
        MarketService market,
        CancellationToken cancellationToken)
    {
        var result = await market.GetIndicesAsync(cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    /// <summary>涨跌家数与两市资金。</summary>
    private static async Task<IResult> GetBreadthAsync(
        HttpContext context,
        MarketService market,
        CancellationToken cancellationToken)
    {
        var result = await market.GetBreadthAsync(cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    /// <summary>行业热力与排行。</summary>
    private static async Task<IResult> GetSectorsAsync(
        HttpContext context,
        MarketService market,
        CancellationToken cancellationToken)
    {
        var result = await market.GetSectorsAsync(cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    /// <summary>两市资金分层与两融杠杆。</summary>
    private static async Task<IResult> GetFundFlowAsync(
        HttpContext context,
        MarketService market,
        CancellationToken cancellationToken)
    {
        var result = await market.GetFundFlowAsync(cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    /// <summary>榜单。查询参数 <c>take</c> 为每榜条数（默认 8，上限 100）。</summary>
    private static async Task<IResult> GetRankingsAsync(
        HttpContext context,
        MarketService market,
        CancellationToken cancellationToken,
        int take = MarketService.DefaultRankingSize)
    {
        var result = await market.GetRankingsAsync(take, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    /// <summary>数据新鲜度与数据源健康状态。</summary>
    private static async Task<IResult> GetStatusAsync(
        HttpContext context,
        MarketService market,
        CancellationToken cancellationToken)
    {
        var result = await market.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }
}

/// <summary>
/// 搜索端点。要求 <c>stock.search</c> 功能点。
/// </summary>
public static class SearchEndpoints
{
    /// <summary>
    /// 注册搜索端点。
    /// </summary>
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/search")
            .WithTags("search")
            .RequireFunctionPoint(FunctionPointCatalog.StockSearch);

        group.MapGet(string.Empty, SearchAsync);
        group.MapGet("/suggest", SuggestAsync);

        return app;
    }

    /// <summary>搜索：代码 / 名称 / 拼音首字母 / 行业关键词。</summary>
    private static async Task<IResult> SearchAsync(
        HttpContext context,
        SA.Application.Search.SearchService search,
        CancellationToken cancellationToken,
        string? q = null,
        string? board = null,
        string? industry = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = new SA.Contracts.Search.SearchQuery(q, board, industry, page, pageSize);
        var result = await search.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    /// <summary>命令面板提示（⌘/Ctrl+K）。</summary>
    private static async Task<IResult> SuggestAsync(
        HttpContext context,
        SA.Application.Search.SearchService search,
        CancellationToken cancellationToken,
        string? q = null,
        int limit = SA.Application.Search.SearchService.SuggestSize)
    {
        var result = await search.SuggestAsync(q, limit, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }
}
