using SA.Application.Authorization;

namespace SA.Api.Auth;

/// <summary>
/// 从当前请求的认证声明读取用户标识。
/// </summary>
/// <remarks>
/// 只读声明、不查库：应用层的授权判定已由端点策略完成，这里只负责把身份传下去。
/// </remarks>
public sealed class HttpUserContext(IHttpContextAccessor accessor) : IUserContext
{
    /// <inheritdoc />
    public string? UserId => accessor.HttpContext?.User.Id();
}
