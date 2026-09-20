using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SA.Infrastructure.Storage;

namespace SA.Infrastructure.Persistence;

/// <summary>
/// 迁移工具用的上下文工厂。显式提供后，<c>dotnet ef</c> 不再尝试启动 SA.Api 进程，
/// 也就不会在生成迁移时触发启动初始化（播种、密钥生成等）。
/// </summary>
public sealed class SaDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SaDbContext>
{
    /// <inheritdoc />
    public SaDbContext CreateDbContext(string[] args)
    {
        var root = DataPaths.ResolveRoot(configured: null);
        var options = new DbContextOptionsBuilder<SaDbContext>()
            .UseSqlite($"Data Source={Path.Combine(root, "sa.db")}")
            .Options;

        return new SaDbContext(options);
    }
}
