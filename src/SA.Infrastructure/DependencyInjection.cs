using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SA.Application.Abstractions;
using SA.Application.Auth;
using SA.Infrastructure.Persistence;
using SA.Infrastructure.Persistence.Seed;
using SA.Infrastructure.Persistence.Stores;
using SA.Infrastructure.Security;
using SA.Infrastructure.Storage;

namespace SA.Infrastructure;

/// <summary>
/// 基础设施层的服务注册入口。Api 与 Collector 共用同一份注册，保证两个宿主行为一致。
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册数据目录布局。
    /// </summary>
    /// <remarks>
    /// 相对路径一律锚定到仓库根（见 <see cref="DataPaths.ResolveRoot"/>），而不是内容根：
    /// 这样 <c>dotnet run</c>、直接执行 dll、以及内嵌进 SA.Api 的采集器都会落到同一份 data/，
    /// 否则会随启动方式在不同目录各建一份数据库。
    /// </remarks>
    public static IServiceCollection AddSaDataPaths(
        this IServiceCollection services,
        IConfiguration configuration,
        string? contentRoot = null)
    {
        var root = DataPaths.ResolveRoot(configuration["Sa:DataDirectory"], contentRoot);
        services.AddSingleton(new DataPaths(root));
        return services;
    }

    /// <summary>
    /// 注册元数据持久化：DbContext、存储实现、播种器与启动初始化器。
    /// </summary>
    public static IServiceCollection AddSaPersistence(this IServiceCollection services)
    {
        services.AddDbContext<SaDbContext>((provider, builder) =>
        {
            var paths = provider.GetRequiredService<DataPaths>();
            paths.EnsureCreated();
            builder.UseSqlite($"Data Source={paths.DatabaseFile};Foreign Keys=True");
        });

        services.AddScoped<IUserStore, UserStore>();
        services.AddScoped<IRoleStore, RoleStore>();
        services.AddScoped<ISessionStore, SessionStore>();
        services.AddScoped<ISettingsStore, SettingsStore>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<PersistenceInitializer>();

        return services;
    }

    /// <summary>
    /// 注册安全组件：令牌哈希、敏感字段保护、签名密钥与访问令牌签发。
    /// </summary>
    public static IServiceCollection AddSaSecurity(this IServiceCollection services)
    {
        services.AddDataProtection()
            .SetApplicationName("SA")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(DataRootOf(services), "keys")));

        services.AddSingleton<ISigningKeyProvider, SigningKeyProvider>();
        services.AddSingleton<ITokenHasher, Sha256TokenHasher>();
        services.AddSingleton<ISecretProtector>(provider =>
            new DataProtectionSecretProtector(provider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>()));
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        return services;
    }

    /// <summary>
    /// 从已注册的 <see cref="DataPaths"/> 取数据根目录。DataProtection 的密钥环需要真实路径，
    /// 而此处无法注入实例（注册阶段），只能读取服务描述。
    /// </summary>
    private static string DataRootOf(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(DataPaths));
        if (descriptor?.ImplementationInstance is DataPaths paths)
        {
            return paths.Root;
        }

        throw new InvalidOperationException("请先调用 AddSaDataPaths 再调用 AddSaSecurity");
    }
}
