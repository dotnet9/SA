namespace SA.Application.Admin;

/// <summary>
/// 站点级设置的键名与默认值。读写两侧共用，避免字面量各写一份。
/// </summary>
/// <remarks>
/// 站点级设置（名称、公告）与「个人偏好」「角色配额」是三类不同的东西：
/// <list type="bullet">
/// <item>站点级设置：全站所有人可见，由后台「系统设置」维护（本类）；</item>
/// <item>个人偏好：主题、涨跌色、推送间隔等，按账号或本机生效，在「个人设置」里改；</item>
/// <item>角色配额：自选上限、每日查询次数等，按角色生效，在「权限矩阵」里改。</item>
/// </list>
/// 把三者混在一个页面会让人以为随便哪个入口都能改所有东西，因此只在后台暴露站点级设置。
/// </remarks>
public static class SiteSettings
{
    /// <summary>站点名称的键。</summary>
    public const string NameKey = "site.name";

    /// <summary>全局公告的键。</summary>
    public const string NoticeKey = "site.notice";

    /// <summary>未配置站点名称时使用的默认值。</summary>
    public const string DefaultName = "股析 SA";
}
