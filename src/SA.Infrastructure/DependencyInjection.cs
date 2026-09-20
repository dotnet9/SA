using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
}
