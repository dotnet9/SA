using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Analysis;
using SA.Domain.Authorization;

namespace SA.Api.Endpoints;

/// <summary>
/// 规则引擎推算端点：行业景气度、个股因果链与传导带宽。
/// </summary>
/// <remarks>
/// <b>公开读接口</b>：两者都是由公开数据算出的分析结果，不登录也能看。
/// 返回体里每项都带权重、口径与置信度，因此公开不代表「不可核对」。
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
            .AllowPublicRead();

        app.MapGet("/api/stocks/{code}/causal", GetCausalAsync)
            .WithTags("analysis")
            .AllowPublicRead();

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
