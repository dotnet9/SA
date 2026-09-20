using SA.Domain.Entities.Identity;

namespace SA.Application.Abstractions;

/// <summary>
/// 签发访问令牌。实现放在 Infrastructure（依赖具体 JWT 库），Application 只依赖抽象。
/// </summary>
public interface IAccessTokenIssuer
{
    /// <summary>
    /// 为指定用户签发访问令牌。功能点集合与数据范围写进声明，
    /// 使接口授权无需每请求查库。
    /// </summary>
    IssuedToken Issue(User user, IReadOnlyList<string> functionPoints, string dataScope);
}

/// <summary>
/// 已签发的访问令牌。
/// </summary>
/// <param name="Token">JWT 字符串。</param>
/// <param name="ExpiresAt">过期时刻。</param>
public readonly record struct IssuedToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// 敏感字段（TOTP 密钥等）的加密保护，避免明文落库。
/// </summary>
public interface ISecretProtector
{
    /// <summary>加密。</summary>
    string Protect(string plainText);

    /// <summary>解密；解密失败抛异常（调用方应视为密钥不可用）。</summary>
    string Unprotect(string protectedValue);
}

/// <summary>
/// 令牌哈希工具。刷新令牌明文只存在于 Cookie，库里只存哈希。
/// </summary>
public interface ITokenHasher
{
    /// <summary>生成新的随机令牌明文（Base64Url，32 字节）。</summary>
    string CreateToken();

    /// <summary>计算令牌哈希（SHA-256，Base64）。</summary>
    string Hash(string token);
}
