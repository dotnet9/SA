using SA.Api.Auth;
using SA.Api.Http;
using SA.Application.Abstractions;
using SA.Application.Admin;

namespace SA.Api.Endpoints;

/// <summary>
/// 站点信息端点：页头名称与全局公告。
/// </summary>
/// <remarks>
/// <b>公开接口</b>：页头在每个页面都要渲染（包括登录页与未登录时的公开页），
/// 因此它必须匿名可读；若要求登录，未登录用户会看到空白的站点名称。
/// 返回体只有站点名称与公告文案，不含任何业务数据。
/// 写入口在后台「系统设置」（要求 <c>admin.security</c>）。
/// </remarks>
public static class SiteEndpoints
{
    /// <summary>
    /// 注册站点信息端点。
    /// </summary>
    public static IEndpointRouteBuilder MapSiteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/system/site", GetAsync)
            .WithTags("system")
            .AllowPublicRead();

        return app;
    }

    private static async Task<IResult> GetAsync(
        HttpContext context,
        ISettingsStore settings,
        CancellationToken cancellationToken)
    {
        var name = await settings.GetAppSettingAsync(SiteSettings.NameKey, cancellationToken).ConfigureAwait(false);
        var notice = await settings.GetAppSettingAsync(SiteSettings.NoticeKey, cancellationToken).ConfigureAwait(false);

        // 站点名称为空时回落到默认值：页头不该出现空白标题
        var resolved = string.IsNullOrWhiteSpace(name) ? SiteSettings.DefaultName : name.Trim();

        return ApiResults.Ok(context, new SiteInfoDto(
            resolved,
            string.IsNullOrWhiteSpace(notice) ? null : notice.Trim()));
    }
}

/// <summary>站点信息。</summary>
/// <param name="Name">站点名称（已回落默认值）。</param>
/// <param name="Notice">全局公告；为空表示不显示横幅。</param>
public sealed record SiteInfoDto(string Name, string? Notice);
