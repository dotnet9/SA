using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Screener;
using SA.Contracts.Common;
using SA.Contracts.Screener;
using SA.Domain.Authorization;

namespace SA.Api.Endpoints;

/// <summary>
/// 条件选股器端点。要求 <c>stock.search</c>（复用搜索权限）；导出额外要求 <c>export.data</c> 并受每日配额约束。
/// </summary>
public static class ScreenerEndpoints
{
    /// <summary>
    /// 注册选股器端点。
    /// </summary>
    public static IEndpointRouteBuilder MapScreenerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/screener").WithTags("screener");

        group.MapGet("/meta", GetMetaAsync).RequireFunctionPoint(FunctionPointCatalog.StockSearch);
        group.MapPost(string.Empty, RunAsync).RequireFunctionPoint(FunctionPointCatalog.StockSearch);

        // 导出走单独的权限点与配额：它把数据带出系统，与「在界面里看看」不是同一件事
        group.MapPost("/export", ExportAsync).RequireFunctionPoint(FunctionPointCatalog.ExportData);

        return app;
    }

    private static async Task<IResult> GetMetaAsync(
        HttpContext context,
        ScreenerService screener,
        SA.Application.Authorization.QuotaService quotaService,
        CancellationToken cancellationToken)
    {
        var remaining = await ResolveExportQuotaAsync(context, quotaService, cancellationToken)
            .ConfigureAwait(false);

        var result = await screener.GetMetaAsync(remaining, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> RunAsync(
        HttpContext context,
        ScreenerRequest request,
        ScreenerService screener,
        CancellationToken cancellationToken)
    {
        var result = await screener.RunAsync(request, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> ExportAsync(
        HttpContext context,
        ScreenerRequest request,
        ScreenerService screener,
        SA.Application.Authorization.PermissionService permissions,
        SA.Application.Authorization.QuotaService quotaService,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        // 导出次数按天计数：配额为 0 表示该角色不允许导出。
        // 先「消费」再生成内容：配额判定与扣减在同一处完成，避免并发下超额导出。
        var quota = await quotaService.ConsumeAsync(userId, "export", cancellationToken).ConfigureAwait(false);
        if (quota.Limit <= 0)
        {
            return ApiResults.Fail(context, ErrorCode.ExportNotAllowed, "当前角色未开放数据导出");
        }

        if (!quota.Allowed)
        {
            return ApiResults.Fail(
                context,
                ErrorCode.QuotaExceeded,
                $"今日导出次数已用完（{quota.Used}/{quota.Limit}），请明日再试或联系管理员调整配额");
        }

        var result = await screener.ExportAsync(request, maxRows: 200, cancellationToken).ConfigureAwait(false);
        if (!result.Ok || result.Value.Content is null)
        {
            return ApiResults.From(context, result);
        }

        var (content, fileName, _) = result.Value;
        return Results.File(content, "text/csv; charset=utf-8", fileName);
    }

    private static async Task<int> ResolveExportQuotaAsync(
        HttpContext context,
        SA.Application.Authorization.QuotaService quotaService,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return 0;
        }

        // 只读配额用量，不扣减：元数据接口被频繁调用，扣在这里会凭空消耗额度
        var quota = await quotaService.PeekAsync(userId, "export", cancellationToken).ConfigureAwait(false);
        return quota.Limit <= 0 ? 0 : Math.Max(0, quota.Limit - quota.Used);
    }
}
