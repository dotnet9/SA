using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Application.Auth;
using SA.Infrastructure.Persistence.Seed;
using SA.Infrastructure.Security;
using SA.Infrastructure.Storage;

namespace SA.Infrastructure.Persistence;

/// <summary>
/// 启动初始化：迁移建库、开启 WAL、确保签名密钥、播种身份与授权数据。
/// 幂等，可安全重复执行（实施计划 §9）。
/// </summary>
public sealed class PersistenceInitializer(
    SaDbContext db,
    IdentitySeeder seeder,
    ISigningKeyProvider signingKey,
    ISettingsStore settings,
    AuthOptions options,
    ILogger<PersistenceInitializer> logger)
{
    private readonly SaDbContext _db = db;
    private readonly IdentitySeeder _seeder = seeder;
    private readonly ISigningKeyProvider _signingKey = signingKey;
    private readonly ISettingsStore _settings = settings;
    private readonly AuthOptions _options = options;
    private readonly ILogger<PersistenceInitializer> _logger = logger;

    /// <summary>
    /// 执行初始化。
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("元数据库迁移完成：{Database}", _db.Database.GetDbConnection().DataSource);

        // 读多写少 + 单机部署，WAL 能显著降低读写互锁（实施计划 §12）
        await _db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken).ConfigureAwait(false);

        await EnsureSigningKeyAsync(cancellationToken).ConfigureAwait(false);
        await _seeder.SeedAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 确定 JWT 签名密钥：配置优先，其次库中持久化值，最后生成并存库。
    /// 这样开发与自用部署都不需要把密钥写进仓库。
    /// </summary>
    private async Task EnsureSigningKeyAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            var fromConfig = DecodeKey(_options.SigningKey);
            _signingKey.Initialize(fromConfig);
            _logger.LogInformation("签名密钥来自配置 Sa:Auth:SigningKey");
            return;
        }

        var persisted = await _settings.GetAppSettingAsync(SigningKeyProvider.PersistedSettingKey, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(persisted))
        {
            _signingKey.Initialize(DecodeKey(persisted));
            return;
        }

        var generated = System.Security.Cryptography.RandomNumberGenerator.GetBytes(48);
        await _settings.SetAppSettingAsync(
            SigningKeyProvider.PersistedSettingKey,
            Convert.ToBase64String(generated),
            cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _signingKey.Initialize(generated);
        _logger.LogInformation("未配置签名密钥，已生成并写入 AppSetting（重启后已登录会话保持有效）");
    }

    /// <summary>
    /// 解析密钥：优先当作 Base64，失败则按 UTF-8 文本处理；长度不足 32 字节直接报错，
    /// 避免用弱密钥签名。
    /// </summary>
    private static byte[] DecodeKey(string value)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            bytes = System.Text.Encoding.UTF8.GetBytes(value);
        }

        if (bytes.Length < 32)
        {
            throw new InvalidOperationException("Sa:Auth:SigningKey 至少需要 32 字节（Base64 或 32 个字符以上的文本）");
        }

        return bytes;
    }
}
