namespace SA.Contracts.Auth;

/// <summary>
/// 登录请求。
/// </summary>
/// <param name="Username">登录名。</param>
/// <param name="Password">密码。</param>
/// <param name="TotpCode">二次验证码；仅当服务端强制 TOTP 且该账号已启用时必填。</param>
/// <param name="RememberMe">记住我：延长刷新令牌有效期至上限。</param>
public sealed record LoginRequest(
    string Username,
    string Password,
    string? TotpCode = null,
    bool RememberMe = false);

/// <summary>
/// 登录响应。访问令牌放响应体，刷新令牌只经 HttpOnly Cookie 下发（详细设计 §8）。
/// </summary>
/// <param name="AccessToken">JWT 访问令牌。</param>
/// <param name="TokenType">固定为 Bearer。</param>
/// <param name="ExpiresIn">访问令牌有效期（秒）。</param>
/// <param name="User">当前用户与权限快照。</param>
/// <param name="ServerTime">服务器业务时间，便于前端对齐。</param>
public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    Me.MeDto User,
    string ServerTime);
