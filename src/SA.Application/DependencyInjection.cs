using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SA.Application.Abstractions;
using SA.Application.Auth;
using SA.Application.Authorization;
using SA.Application.Market;
using SA.Application.Search;
using SA.Application.Services;

namespace SA.Application;

/// <summary>
/// 应用层服务注册。放在 Application 程序集内，避免基础设施层替应用层决定生命周期
/// （依赖方向 Api → Application → Domain，见实施计划 §1.1）。
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册鉴权配置与用例服务。
    /// </summary>
    /// <remarks>
    /// <see cref="PermissionService"/> 与 <see cref="QuotaService"/> 依赖仓储（Scoped），
    /// 因此自身必须是 Scoped；它们用到的缓存由单例的 IMemoryCache 承载，不受影响。
    /// </remarks>
    public static IServiceCollection AddSaApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new AuthOptions();
        configuration.GetSection(AuthOptions.SectionName).Bind(options);
        services.AddSingleton(options);

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<PasswordPolicy>();

        services.AddScoped<PermissionService>();
        services.AddScoped<QuotaService>();
        services.AddScoped<AuthService>();
        services.AddScoped<MeService>();

        return services;
    }

    /// <summary>
    /// 注册行情用例：采集配置、内存快照与搜索索引缓存、市场与搜索服务。
    /// </summary>
    /// <remarks>
    /// 两个缓存都是单例：它们承载的是「全市场一次性聚合结果」，
    /// 每个请求各持一份既无意义也会让内存随请求数增长。服务本身是 Scoped，
    /// 因为它们依赖 Scoped 的仓储。
    /// </remarks>
    public static IServiceCollection AddSaMarket(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new CollectOptions();
        configuration.GetSection(CollectOptions.SectionName).Bind(options);
        services.AddSingleton(options);

        services.AddSingleton<MarketSnapshotCache>();
        services.AddSingleton<SearchIndexCache>();

        services.AddScoped<MarketService>();
        services.AddScoped<SearchService>();

        return services;
    }
}
