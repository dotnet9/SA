using System.Security.Cryptography;
using System.Text;

namespace SA.Application.Auth;

/// <summary>
/// TOTP（RFC 6238）二次验证。按实施计划 §2 决策 6：全链路实现，但由配置决定是否强制。
/// </summary>
public interface ITotpService
{
    /// <summary>生成 Base32 密钥（20 字节随机，与常见验证器 App 兼容）。</summary>
    string CreateSecret();

    /// <summary>生成 otpauth:// URI，供验证器扫码或手工录入。</summary>
    string BuildOtpAuthUri(string issuer, string account, string secret);

    /// <summary>
    /// 校验验证码。允许前后各 <paramref name="window"/> 个时间步，容忍客户端时钟漂移。
    /// </summary>
    bool Verify(string secret, string code, int window = 1, DateTimeOffset? now = null);
}

/// <summary>
/// RFC 6238 的 HMAC-SHA1 / 6 位 / 30 秒实现。
/// </summary>
public sealed class TotpService : ITotpService
{
    private const int SecretBytes = 20;
    private const int Digits = 6;
    private const int PeriodSeconds = 30;

    /// <inheritdoc />
    public string CreateSecret() => Base32.Encode(RandomNumberGenerator.GetBytes(SecretBytes));

    /// <inheritdoc />
    public string BuildOtpAuthUri(string issuer, string account, string secret)
    {
        var label = Uri.EscapeDataString($"{issuer}:{account}");
        var parameters = $"secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={PeriodSeconds}";
        return $"otpauth://totp/{label}?{parameters}";
    }

    /// <inheritdoc />
    public bool Verify(string secret, string code, int window = 1, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalized = code.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (normalized.Length != Digits || !normalized.All(char.IsAsciiDigit))
        {
            return false;
        }

        byte[] key;
        try
        {
            key = Base32.Decode(secret);
        }
        catch (FormatException)
        {
            return false;
        }

        var counter = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / PeriodSeconds;
        for (var offset = -window; offset <= window; offset++)
        {
            var candidate = ComputeCode(key, counter + offset);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(candidate),
                    Encoding.ASCII.GetBytes(normalized)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 计算指定时间步的验证码：HMAC-SHA1(大端计数器) → 动态截断 → 取模 10^6。
    /// </summary>
    private static string ComputeCode(byte[] key, long counter)
    {
        Span<byte> counterBytes = stackalloc byte[8];
        BitConverter.TryWriteBytes(counterBytes, counter);
        if (BitConverter.IsLittleEndian)
        {
            counterBytes.Reverse();
        }

        Span<byte> mac = stackalloc byte[20];
        HMACSHA1.HashData(key, counterBytes, mac);

        var offset = mac[19] & 0x0F;
        var binary = ((mac[offset] & 0x7F) << 24)
                     | ((mac[offset + 1] & 0xFF) << 16)
                     | ((mac[offset + 2] & 0xFF) << 8)
                     | (mac[offset + 3] & 0xFF);

        var value = binary % (int)Math.Pow(10, Digits);
        return value.ToString(new string('0', Digits));
    }
}

/// <summary>
/// Base32（RFC 4648，无填充），验证器 App 通用的密钥编码。
/// </summary>
internal static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>编码为不带填充的 Base32 字符串。</summary>
    public static string Encode(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                builder.Append(Alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }

        if (bitsLeft > 0)
        {
            builder.Append(Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        }

        return builder.ToString();
    }

    /// <summary>解码 Base32 字符串，非法字符抛 <see cref="FormatException"/>。</summary>
    public static byte[] Decode(string text)
    {
        var normalized = text.Trim().TrimEnd('=').ToUpperInvariant().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (normalized.Length == 0)
        {
            return [];
        }

        var output = new List<byte>(normalized.Length * 5 / 8);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var c in normalized)
        {
            var index = Alphabet.IndexOf(c, StringComparison.Ordinal);
            if (index < 0)
            {
                throw new FormatException($"Base32 含非法字符：{c}");
            }

            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }

        return [.. output];
    }
}
