using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SA.Application.Abstractions;
using SA.Application.Auth;
using SA.Application.Authorization;
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
}
