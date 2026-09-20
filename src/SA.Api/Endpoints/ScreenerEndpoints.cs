using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Screener;
using SA.Contracts.Common;
using SA.Contracts.Screener;
using SA.Domain.Authorization;
using SA.Domain.Entities.Identity;

namespace SA.Api.Endpoints;

/// <summary>
/// 条件选股器端点。筛选要求 <c>stock.search</c>；导出额外要求 <c>export.data</c> 并受配额约束。
/// </summary>
/// <remarks>
/// <b>配额口径沿用框架约定</b>（见 <see cref="SA.Application.Authorization.QuotaService"/>）：
/// 配额「未配置或 ≤0」表示<b>不限</b>，而不是禁止。因此：
/// <list type="bullet">
/// <item>导出是否允许由功能点 <c>export.data</c> 决定；</item>
/// <item>每日导出次数用 <see cref="QuotaKeys.DailyQueries"/>（每日调用次数上限），未配置即不限；</item>
/// <item>单次导出行数用 <see cref="QuotaKeys.ExportRows"/>，未配置时退回一个安全上限。</item>
/// </list>
/// 早期实现自行发明了 <c>export.daily</c> 这个键，并把 0 当作「禁止导出」，
/// 与框架里 0 = 不限的语义正好相反，结果把管理员也挡在门外（实测现象）。
/// </remarks>
public static class ScreenerEndpoints
{
    /// <summary>未配置单次导出行数时的安全上限（避免一次导出把响应体与内存撑爆）。</summary>
    private const int FallbackExportRows = 200;

    /// <summary>
    /// 注册选股器端点。
    /// </summary>
    public static IEndpointRouteBuilder MapScreenerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/screener").WithTags("screener");

        group.MapGet("/meta", GetMetaAsync).RequireFunctionPoint(FunctionPointCatalog.StockSearch);
        group.MapPost(string.Empty, RunAsync).RequireFunctionPoint(FunctionPointCatalog.StockSearch);

        // 导出走单独的权限点：它把数据带出系统，与「在界面里看看」不是同一件事
        group.MapPost("/export", ExportAsync).RequireFunctionPoint(FunctionPointCatalog.ExportData);

        return app;
    }

    private static async Task<IResult> GetMetaAsync(
        HttpContext context,
        ScreenerService screener,
        SA.Application.Authorization.QuotaService quotaService,
        CancellationToken cancellationToken)
    {
        var (remaining, rowLimit) = await ResolveExportQuotaAsync(context, quotaService, cancellationToken)
            .ConfigureAwait(false);

        var result = await screener.GetMetaAsync(remaining, rowLimit, cancellationToken).ConfigureAwait(false);
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
        SA.Application.Authorization.QuotaService quotaService,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var (_, rowLimit) = await ResolveExportQuotaAsync(context, quotaService, cancellationToken).ConfigureAwait(false);

        // 先生成内容、再扣配额：数据未就绪（1003）或筛选失败时不应该消耗用户的导出次数
        // （实测过的问题：快照未就绪时一次失败的导出把配额从 2000 扣到了 1999）
        var result = await screener.ExportAsync(request, maxRows: rowLimit, cancellationToken).ConfigureAwait(false);
        if (!result.Ok || result.Value.Content is null)
        {
            return ApiResults.From(context, result);
        }

        // 每日导出次数：未配置上限时 ConsumeAsync 直接放行且不计数（框架约定 0 = 不限）
        var quota = await quotaService.ConsumeAsync(userId, QuotaKeys.DailyQueries, cancellationToken)
            .ConfigureAwait(false);
        if (!quota.Allowed)
        {
            return ApiResults.Fail(
                context,
                ErrorCode.QuotaExceeded,
                $"今日导出次数已用完（{quota.Used}/{quota.Limit}），请明日再试或联系管理员调整配额");
        }

        var (content, fileName, _) = result.Value;
        return Results.File(content, "text/csv; charset=utf-8", fileName);
    }

    /// <summary>
    /// 取导出相关的额度：今日剩余次数（-1 表示不限）与单次行数上限。
    /// </summary>
    private static async Task<(int Remaining, int RowLimit)> ResolveExportQuotaAsync(
        HttpContext context,
        SA.Application.Authorization.QuotaService quotaService,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return (-1, FallbackExportRows);
        }

        // 只读额度、不扣减：元数据接口被频繁调用，扣在这里会凭空消耗额度
        var daily = await quotaService.PeekAsync(userId, QuotaKeys.DailyQueries, cancellationToken).ConfigureAwait(false);
        var remaining = daily.Limit <= 0 ? -1 : Math.Max(0, daily.Limit - daily.Used);

        var rows = await quotaService.PeekAsync(userId, QuotaKeys.ExportRows, cancellationToken).ConfigureAwait(false);
        var rowLimit = rows.Limit <= 0 ? FallbackExportRows : rows.Limit;

        return (remaining, rowLimit);
    }
}
