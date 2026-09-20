using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using SA.Application.Abstractions;

namespace SA.Infrastructure.Security;

/// <summary>
/// 刷新令牌哈希：SHA-256 后 Base64。明文只在 Cookie 中流转，库中不可逆。
/// </summary>
public sealed class Sha256TokenHasher : ITokenHasher
{
    /// <inheritdoc />
    public string CreateToken() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    /// <inheritdoc />
    public string Hash(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        return Convert.ToBase64String(SHA256.HashData(bytes));
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>
/// 敏感字段保护。使用 ASP.NET Core Data Protection，密钥环落在数据目录内，
/// 避免依赖机器级密钥导致换机后无法解密（实施计划 §5.3）。
/// </summary>
public sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private const string Purpose = "sa.secret.v1";
    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    /// <inheritdoc />
    public string Protect(string plainText) => _protector.Protect(plainText);

    /// <inheritdoc />
    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}

/// <summary>
/// JWT 签名密钥的来源。优先取配置；未配置时从库中读取，仍无则生成并持久化，
/// 这样开发环境无需把密钥写进仓库，且重启后已登录会话不会立刻失效。
/// </summary>
public interface ISigningKeyProvider
{
    /// <summary>当前签名密钥（初始化后可用）。</summary>
    byte[] Key { get; }

    /// <summary>是否已初始化。</summary>
    bool Initialized { get; }

    /// <summary>设置密钥。仅允许在启动阶段调用一次。</summary>
    void Initialize(byte[] key);
}

/// <summary>
/// <see cref="ISigningKeyProvider"/> 的默认实现。
/// </summary>
public sealed class SigningKeyProvider : ISigningKeyProvider
{
    private const string SettingKey = "auth.signingkey";
    private byte[]? _key;

    /// <inheritdoc />
    public byte[] Key => _key ?? throw new InvalidOperationException("签名密钥尚未初始化，请确认启动初始化已执行");

    /// <inheritdoc />
    public bool Initialized => _key is not null;

    /// <inheritdoc />
    public void Initialize(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length < 32)
        {
            throw new ArgumentException("签名密钥至少需要 32 字节", nameof(key));
        }

        _key = key;
    }

    /// <summary>库中持久化签名密钥所用的键名。</summary>
    public static string PersistedSettingKey => SettingKey;
}
