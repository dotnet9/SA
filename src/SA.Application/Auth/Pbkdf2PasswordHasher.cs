using System.Security.Cryptography;

namespace SA.Application.Auth;

/// <summary>
/// 密码哈希。实现遵循详细设计 DDL 的「Hash + Salt 分列存储」形态，
/// 并把迭代次数一并存下，便于日后提高强度而无需强制所有人改密。
/// </summary>
public interface IPasswordHasher
{
    /// <summary>计算密码哈希。</summary>
    PasswordHashResult Hash(string password);

    /// <summary>校验密码。使用固定时间比较，避免计时侧信道。</summary>
    bool Verify(string password, string hash, string salt, int iterations);
}

/// <summary>
/// 一次哈希的结果，对应 <c>User.PasswordHash/PasswordSalt/PasswordIterations</c> 三列。
/// </summary>
/// <param name="Hash">派生密钥（Base64）。</param>
/// <param name="Salt">盐（Base64）。</param>
/// <param name="Iterations">迭代次数。</param>
public readonly record struct PasswordHashResult(string Hash, string Salt, int Iterations);

/// <summary>
/// PBKDF2-HMAC-SHA256 实现。参数取 OWASP 建议的 210,000 次迭代、16 字节盐、32 字节密钥。
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    /// <summary>默认迭代次数。</summary>
    public const int DefaultIterations = 210_000;

    private const int SaltBytes = 16;
    private const int KeyBytes = 32;

    /// <inheritdoc />
    public PasswordHashResult Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            DefaultIterations,
            HashAlgorithmName.SHA256,
            KeyBytes);

        return new PasswordHashResult(
            Convert.ToBase64String(key),
            Convert.ToBase64String(salt),
            DefaultIterations);
    }

    /// <inheritdoc />
    public bool Verify(string password, string hash, string salt, int iterations)
    {
        if (string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(hash)
            || string.IsNullOrWhiteSpace(salt)
            || iterations <= 0)
        {
            return false;
        }

        byte[] saltBytes;
        byte[] expected;
        try
        {
            saltBytes = Convert.FromBase64String(salt);
            expected = Convert.FromBase64String(hash);
        }
        catch (FormatException)
        {
            // 库中被手工篡改或截断的数据不应导致 500，直接判定校验失败
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
