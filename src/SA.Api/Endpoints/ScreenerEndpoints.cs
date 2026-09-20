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
/// <item>每日导出次数用 <see cref="QuotaKeys.DailyQueries"/>，未配置即不限；</item>
/// <item>单次导出行数用 <see cref="QuotaKeys.ExportRows"/>，未配置时退回安全上限；</item>
/// <item>策略数量用 <see cref="QuotaKeys.StrategyMax"/>，未配置即不限。</item>
/// </list>
/// 早期实现自行发明了 <c>export.daily</c> 这个键，并把 0 当作「禁止导出」，
/// 与框架里 0 = 不限的语义正好相反，结果把管理员也挡在门外（实测现象）。
/// </remarks>
public static class ScreenerEndpoints
{
    /// <summary>未配置单次导出行数时的安全上限（避免一次导出把响应体与内存撑爆）。</summary>
    private const int FallbackExportRows = 200;

    /// <summary>筛选日志与策略列表的展示条数。</summary>
    private const int RunLogLimit = 50;

    /// <summary>
    /// 注册选股器端点。
    /// </summary>
    public static IEndpointRouteBuilder MapScreenerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/screener").WithTags("screener");

        group.MapGet("/meta", GetMetaAsync).RequireFunctionPoint(FunctionPointCatalog.StockSearch);

        // 显式筛选：界面上的「开始筛选」走这条，会写入筛选日志
        group.MapPost(string.Empty, RunAsync).RequireFunctionPoint(FunctionPointCatalog.StockSearch);

        // 分布统计：只读且不留痕（同一条件下反复看分布不该刷满日志）
        group.MapPost("/distribution", DistributionAsync).RequireFunctionPoint(FunctionPointCatalog.StockSearch);

        // 筛选日志与我的策略（同一张表的两种用法）
        group.MapGet("/history", GetHistoryAsync).RequireFunctionPoint(FunctionPointCatalog.StockSearch);
        group.MapGet("/strategies", GetStrategiesAsync).RequireFunctionPoint(FunctionPointCatalog.StockSearch);
        group.MapPost("/strategies", SaveStrategyAsync).RequireFunctionPoint(FunctionPointCatalog.StockSearch);
        group.MapPut("/strategies/{id:long}", RenameStrategyAsync).RequireFunctionPoint(FunctionPointCatalog.StockSearch);
        group.MapDelete("/strategies/{id:long}", DeleteRunAsync).RequireFunctionPoint(FunctionPointCatalog.StockSearch);
        group.MapPost("/strategies/{id:long}/replay", ReplayAsync).RequireFunctionPoint(FunctionPointCatalog.StockSearch);

        // 导出记录（选股器页的「导出记录」区块）
        group.MapGet("/exports", GetExportsAsync).RequireFunctionPoint(FunctionPointCatalog.StockSearch);

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
        var (remaining, rowLimit, strategyQuota) = await ResolveQuotasAsync(context, quotaService, cancellationToken)
            .ConfigureAwait(false);

        var result = await screener.GetMetaAsync(remaining, rowLimit, strategyQuota, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> RunAsync(
        HttpContext context,
        ScreenerRequest request,
        ScreenerService screener,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await screener.RunAndRecordAsync(request, userId, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> DistributionAsync(
        HttpContext context,
        ScreenerDistributionRequest request,
        ScreenerService screener,
        CancellationToken cancellationToken)
    {
        // 未给条件时按「全市场」统计，字段缺省用涨跌幅（最常看的分布）
        var condition = request.Request ?? new ScreenerRequest(null, null, null, null, true, 1, 200, null);
        var field = string.IsNullOrWhiteSpace(request.Field) ? ScreenerFields.Pct : request.Field;

        var result = await screener.GetDistributionAsync(condition, field, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> GetHistoryAsync(
        HttpContext context,
        ScreenerService screener,
        SA.Application.Authorization.QuotaService quotaService,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var (_, _, strategyQuota) = await ResolveQuotasAsync(context, quotaService, cancellationToken).ConfigureAwait(false);
        var result = await screener.GetRunsAsync(userId, false, RunLogLimit, strategyQuota, cancellationToken)
            .ConfigureAwait(false);

        return ApiResults.From(context, result);
    }

    private static async Task<IResult> GetStrategiesAsync(
        HttpContext context,
        ScreenerService screener,
        SA.Application.Authorization.QuotaService quotaService,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var (_, _, strategyQuota) = await ResolveQuotasAsync(context, quotaService, cancellationToken).ConfigureAwait(false);
        var result = await screener.GetRunsAsync(userId, true, RunLogLimit, strategyQuota, cancellationToken)
            .ConfigureAwait(false);

        return ApiResults.From(context, result);
    }

    private static async Task<IResult> SaveStrategyAsync(
        HttpContext context,
        ScreenerStrategySaveRequest request,
        ScreenerService screener,
        SA.Application.Authorization.QuotaService quotaService,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var (_, _, strategyQuota) = await ResolveQuotasAsync(context, quotaService, cancellationToken).ConfigureAwait(false);
        var result = await screener.SaveStrategyAsync(userId, request, strategyQuota, cancellationToken).ConfigureAwait(false);

        return ApiResults.From(context, result);
    }

    private static async Task<IResult> RenameStrategyAsync(
        HttpContext context,
        long id,
        ScreenerStrategyRenameRequest request,
        ScreenerService screener,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await screener.RenameStrategyAsync(userId, id, request.Name, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> DeleteRunAsync(
        HttpContext context,
        long id,
        ScreenerService screener,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await screener.DeleteRunAsync(userId, id, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> ReplayAsync(
        HttpContext context,
        long id,
        ScreenerService screener,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await screener.ReplayAsync(userId, id, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }

    private static async Task<IResult> GetExportsAsync(
        HttpContext context,
        ScreenerService screener,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return ApiResults.Fail(context, ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var result = await screener.GetExportLogsAsync(userId, 50, cancellationToken).ConfigureAwait(false);
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

        var (_, rowLimit, _) = await ResolveQuotasAsync(context, quotaService, cancellationToken).ConfigureAwait(false);

        // 先生成内容、再扣配额：数据未就绪（1003）或筛选失败时不应该消耗用户的导出次数
        // （实测过的问题：快照未就绪时一次失败的导出把配额从 2000 扣到了 1999）
        var result = await screener.ExportAsync(request, maxRows: rowLimit, cancellationToken).ConfigureAwait(false);
        if (!result.Ok || result.Value.Content is null)
        {
            return ApiResults.From(context, result);
        }

        var quota = await quotaService.ConsumeAsync(userId, QuotaKeys.DailyQueries, cancellationToken)
            .ConfigureAwait(false);
        if (!quota.Allowed)
        {
            return ApiResults.Fail(
                context,
                ErrorCode.QuotaExceeded,
                $"今日导出次数已用完（{quota.Used}/{quota.Limit}），请明日再试或联系管理员调整配额");
        }

        var (content, fileName, rows) = result.Value;

        // 写导出日志（实施计划 §9：导出必须留痕，含数据集、格式与行数）
        await screener.RecordExportAsync(userId, "screener", rows, cancellationToken).ConfigureAwait(false);

        return Results.File(content, "text/csv; charset=utf-8", fileName);
    }

    /// <summary>
    /// 取三类额度：今日剩余导出次数、单次行数上限、策略数量上限。
    /// </summary>
    /// <remarks>
    /// 只读额度、不扣减：元数据接口被频繁调用，扣在这里会凭空消耗额度。
    /// 返回值中的 <c>-1</c> 表示「未配置上限」。
    /// </remarks>
    private static async Task<(int Remaining, int RowLimit, int StrategyQuota)> ResolveQuotasAsync(
        HttpContext context,
        SA.Application.Authorization.QuotaService quotaService,
        CancellationToken cancellationToken)
    {
        var userId = context.User.Id();
        if (userId is null)
        {
            return (-1, FallbackExportRows, -1);
        }

        var daily = await quotaService.PeekAsync(userId, QuotaKeys.DailyQueries, cancellationToken).ConfigureAwait(false);
        var remaining = daily.Limit <= 0 ? -1 : Math.Max(0, daily.Limit - daily.Used);

        var rows = await quotaService.PeekAsync(userId, QuotaKeys.ExportRows, cancellationToken).ConfigureAwait(false);
        var rowLimit = rows.Limit <= 0 ? FallbackExportRows : rows.Limit;

        var strategies = await quotaService.PeekAsync(userId, QuotaKeys.StrategyMax, cancellationToken).ConfigureAwait(false);
        var strategyQuota = strategies.Limit <= 0 ? -1 : strategies.Limit;

        return (remaining, rowLimit, strategyQuota);
    }
}

/// <summary>分布统计请求。</summary>
/// <param name="Field">统计字段；为空时用涨跌幅。</param>
/// <param name="Request">筛选条件；为空时表示全市场。</param>
public sealed record ScreenerDistributionRequest(string? Field, ScreenerRequest? Request);
