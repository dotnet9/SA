using SA.Api.Http;
using SA.Application.Abstractions;
using SA.Application.Admin;
using SA.Contracts.Common;

namespace SA.Api.Endpoints;

/// <summary>
/// 运维端点：数据源与采集任务监控。
/// </summary>
/// <remarks>
/// <para>
/// 原先这里还有用户管理、角色与权限、会话与审计、系统设置四组端点。用户决定
/// <b>去掉登录与权限</b>（所有功能免费开放），因此那四组随之删除——它们本身就是
/// 权限体系的组成部分，留着既无入口也无人可管。
/// </para>
/// <para>
/// 数据源监控保留：它展示的是采集任务与上游连通性，是运维信息，与权限无关，
/// 与「大盘概况」页里被移除的 17 个数据源状态点正好互补（概览页保持干净，
/// 细节放这里）。
/// </para>
/// </remarks>
public static class AdminEndpoints
{
    /// <summary>
    /// 注册运维端点。
    /// </summary>
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var sources = app.MapGroup("/api/admin/datasources").WithTags("admin");
        sources.MapGet(string.Empty, GetDataSourcesAsync);

        return app;
    }

    private static async Task<IResult> GetDataSourcesAsync(
        HttpContext context,
        AdminService admin,
        CancellationToken cancellationToken,
        int taskLimit = 30)
    {
        var result = await admin.GetDataSourcesAsync(taskLimit, cancellationToken).ConfigureAwait(false);
        return ApiResults.From(context, result);
    }
}
