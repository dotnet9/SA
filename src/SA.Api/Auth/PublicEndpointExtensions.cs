namespace SA.Api.Auth;

/// <summary>
/// 公开读接口的标记。全部公开面都经由此扩展声明，便于统一审计与调整。
/// </summary>
/// <remarks>
/// <para>
/// <b>分区规则</b>（一处定义，全站生效）：
/// </para>
/// <list type="bullet">
/// <item><b>公开（无需登录）</b>：公开市场数据与个股公开资料——
/// 市场概览与指数、搜索、个股各模块（趋势 / 财务 / 股权 / 资金 / 行业 / 事件 / 因果 / 风险 / 评级）、
/// 四种拓扑图、行业景气度、站点名称与公告。这些都是任何人打开网页就能看到的信息。</item>
/// <item><b>需登录</b>：个人数据与操作——自选股、条件选股器（含策略与导出）、提醒规则、通知中心、
/// 提醒形态预览、个人设置，以及后台四页（另需 admin.* 功能点）。</item>
/// </list>
/// <para>
/// <b>为什么公开端点不能再挂 <c>RequireFunctionPoint</c></b>：匿名请求没有任何功能点，
/// 挂上校验会让公开页对未登录用户返回 2002，与「不登录也能看」直接冲突。
/// 因此公开端点只声明「可匿名访问」，其访问控制交给数据范围（见下）。
/// </para>
/// <para>
/// <b>数据范围仍然生效</b>：功能点 <c>data.scope.watchlist</c> 对<b>已登录</b>用户依旧把可见标的
/// 收窄到其自选范围（榜单、搜索、选股都会过滤）；匿名用户没有个人范围，因此看到全市场公开数据。
/// 换句话说：功能点在这里不再管「能不能看」，只管「能看到多少」。
/// </para>
/// <para>
/// <b>令牌无效时不降级</b>：带了一个过期/伪造令牌的请求会被拒绝（401），
/// 而不是当作匿名放行——否则「仅自选」角色的令牌一过期，就会在公开页上看到全市场数据。
/// 该行为由 <c>JwtBearerEvents.OnAuthenticationFailed</c> 强制。
/// </para>
/// </remarks>
public static class PublicEndpointExtensions
{
    /// <summary>
    /// 把端点（或路由分组）标记为公开可读：匿名可访问，登录后按数据范围过滤。
    /// </summary>
    /// <remarks>
    /// 放在 Api 层而不是 Application 层：它是传输层的授权策略声明，
    /// 与应用层的用例无关（用例对匿名与登录一视同仁，只按传入的 UserId 决定范围）。
    /// </remarks>
    public static TBuilder AllowPublicRead<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AllowAnonymous();
}
