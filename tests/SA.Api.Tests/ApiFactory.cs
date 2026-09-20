using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace SA.Api.Tests;

/// <summary>
/// 集成测试宿主。每个工厂实例使用独立的临时数据目录，避免污染开发库，
/// 也让「首启播种」这条路径在每个测试类里都真实跑一遍。
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>测试用管理员初始密码（满足密码策略）。</summary>
    public const string AdminPassword = "SaTest!2026Pass";

    /// <summary>改密测试用的新密码。</summary>
    public const string NewAdminPassword = "SaTest!2026Next";

    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), "sa-tests", Guid.NewGuid().ToString("N"));

    private readonly int _lockThreshold;

    /// <summary>
    /// 默认宿主：锁定阈值走 appsettings 的 5。xUnit 的 IClassFixture 要求真正的无参构造函数，
    /// 可选参数不算，因此这里显式提供。
    /// </summary>
    public ApiFactory()
        : this(lockThreshold: 5)
    {
    }

    /// <summary>
    /// 构造测试宿主。
    /// </summary>
    /// <param name="lockThreshold">登录失败锁定阈值。</param>
    protected ApiFactory(int lockThreshold)
    {
        _lockThreshold = lockThreshold;
    }

    /// <summary>本次测试实例的数据目录。</summary>
    public string DataDirectory => _dataDirectory;

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Sa:DataDirectory", _dataDirectory);
        builder.UseSetting("Sa:Auth:AdminInitialPassword", AdminPassword);
        builder.UseSetting("Sa:Auth:RequireTotp", "false");
        builder.UseSetting("Sa:Auth:LockThreshold", _lockThreshold.ToString());
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        try
        {
            if (Directory.Exists(_dataDirectory))
            {
                Directory.Delete(_dataDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // SQLite 连接释放有延迟，清理失败不影响测试结论
        }
    }
}

/// <summary>
/// 低锁定阈值的宿主，用于验证「连续失败后账号被锁」这条路径而不影响其他测试。
/// </summary>
public sealed class StrictLockApiFactory() : ApiFactory(lockThreshold: 2);
