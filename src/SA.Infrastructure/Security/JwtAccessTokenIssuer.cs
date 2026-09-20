using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SA.Application.Abstractions;
using SA.Application.Auth;
using SA.Domain.Common;
using SA.Domain.Entities.Identity;
using SA.Infrastructure.Security;

namespace SA.Infrastructure.Security;

/// <summary>
/// 访问令牌声明名。前后端与授权处理器共用，避免字符串散落。
/// </summary>
public static class SaClaims
{
    /// <summary>用户 Id。</summary>
    public const string Subject = "sub";

    /// <summary>登录名。</summary>
    public const string Username = "name";

    /// <summary>显示名。</summary>
    public const string Nickname = "nickname";

    /// <summary>角色 Id。</summary>
    public const string Role = "role";

    /// <summary>数据范围：all / watchlist。</summary>
    public const string DataScope = "scope";

    /// <summary>功能点编码（多值声明）。</summary>
    public const string FunctionPoint = "fp";
}

/// <summary>
/// 使用 HMAC-SHA256 签发访问令牌。功能点与数据范围写进声明，使授权无需每请求查库。
/// </summary>
public sealed class JwtAccessTokenIssuer(AuthOptions options, ISigningKeyProvider signingKey) : IAccessTokenIssuer
{
    private readonly AuthOptions _options = options;
    private readonly ISigningKeyProvider _signingKey = signingKey;

    /// <inheritdoc />
    public IssuedToken Issue(User user, IReadOnlyList<string> functionPoints, string dataScope)
    {
        var now = SaTime.Now;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_signingKey.Key),
                SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [SaClaims.Subject] = user.Id,
                [SaClaims.Username] = user.Username,
                [SaClaims.Nickname] = user.Nickname,
                [SaClaims.Role] = user.RoleId,
                [SaClaims.DataScope] = dataScope,
                [SaClaims.FunctionPoint] = functionPoints.ToArray()
            }
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new IssuedToken(token, expiresAt);
    }
}

/// <summary>
/// 访问令牌校验参数。签发与校验共用同一份定义，避免两侧口径漂移。
/// </summary>
public static class SaJwtValidation
{
    /// <summary>
    /// 构造校验参数。
    /// </summary>
    public static TokenValidationParameters Create(AuthOptions options, ISigningKeyProvider signingKey) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(signingKey.Key),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = SaClaims.Username,
        RoleClaimType = SaClaims.Role
    };
}
