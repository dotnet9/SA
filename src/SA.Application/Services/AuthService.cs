using SA.Application.Abstractions;
using SA.Application.Auth;
using SA.Application.Authorization;
using SA.Application.Common;
using SA.Contracts.Auth;
using SA.Contracts.Common;
using SA.Contracts.Me;
using SA.Domain.Common;
using SA.Domain.Entities.Identity;

namespace SA.Application.Services;

/// <summary>
/// 登录、令牌轮换、登出、改密与二次验证。错误码语义见详细设计 §1.2：
/// 凭证错误用 <c>2001</c>、锁定用 <c>2004</c>、参数不合规用 <c>1001</c>。
/// </summary>
public sealed class AuthService(
    IUserStore users,
    ISessionStore sessions,
    PermissionService permissions,
    IPasswordHasher hasher,
    PasswordPolicy passwordPolicy,
    ITotpService totp,
    IAccessTokenIssuer tokenIssuer,
    ITokenHasher tokenHasher,
    ISecretProtector secretProtector,
    AuthOptions options,
    IUnitOfWork unitOfWork)
{
    private readonly IUserStore _users = users;
    private readonly ISessionStore _sessions = sessions;
    private readonly PermissionService _permissions = permissions;
    private readonly IPasswordHasher _hasher = hasher;
    private readonly PasswordPolicy _passwordPolicy = passwordPolicy;
    private readonly ITotpService _totp = totp;
    private readonly IAccessTokenIssuer _tokenIssuer = tokenIssuer;
    private readonly ITokenHasher _tokenHasher = tokenHasher;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly AuthOptions _options = options;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    /// <summary>
    /// 登录成功后的产物：响应体 + 需要写入 Cookie 的刷新令牌。
    /// </summary>
    /// <param name="Response">响应体。</param>
    /// <param name="RefreshToken">刷新令牌明文（仅此一次可见）。</param>
    /// <param name="RefreshExpiresAt">刷新令牌过期时刻。</param>
    /// <param name="MustChangePwd">是否需强制改密。</param>
    public sealed record LoginOutcome(
        LoginResponse Response,
        string RefreshToken,
        DateTimeOffset RefreshExpiresAt,
        bool MustChangePwd);

    /// <summary>
    /// 登录。
    /// </summary>
    public async Task<ServiceResult<LoginOutcome>> LoginAsync(
        LoginRequest request,
        string? ip,
        string? device,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ServiceResult<LoginOutcome>.Fail(ErrorCode.InvalidParameter, "请输入用户名与密码");
        }

        var user = await _users.FindByUsernameAsync(request.Username.Trim(), cancellationToken).ConfigureAwait(false);

        // 用户不存在：记录失败日志但不区分提示，避免枚举账号
        if (user is null)
        {
            await WriteLogAsync(request.Username, ip, device, LoginResultKind.Failed, "用户不存在", cancellationToken).ConfigureAwait(false);
            return ServiceResult<LoginOutcome>.Fail(ErrorCode.Unauthenticated, "用户名或密码错误");
        }

        if (string.Equals(user.Status, UserStatus.Disabled, StringComparison.Ordinal))
        {
            await WriteLogAsync(user.Username, ip, device, LoginResultKind.Denied, "账号已禁用", cancellationToken).ConfigureAwait(false);
            return ServiceResult<LoginOutcome>.Fail(ErrorCode.Unauthenticated, "账号已禁用，请联系管理员");
        }

        var now = SaTime.Now;
        if (user.LockedUntil is { } lockedUntil && lockedUntil > now)
        {
            var remainMinutes = Math.Max(1, (int)Math.Ceiling((lockedUntil - now).TotalMinutes));
            await WriteLogAsync(user.Username, ip, device, LoginResultKind.Denied, "账号锁定中", cancellationToken).ConfigureAwait(false);
            return ServiceResult<LoginOutcome>.Fail(
                ErrorCode.AccountLocked,
                $"登录失败次数过多，账号已锁定，请 {remainMinutes} 分钟后重试");
        }

        if (!_hasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
        {
            user.FailCount++;
            var locked = user.FailCount >= _options.LockThreshold;
            if (locked)
            {
                user.LockedUntil = now.AddMinutes(_options.LockMinutes);
            }

            await WriteLogAsync(
                user.Username,
                ip,
                device,
                locked ? LoginResultKind.Denied : LoginResultKind.Failed,
                locked ? "密码错误达阈值，已锁定" : "密码错误",
                cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return locked
                ? ServiceResult<LoginOutcome>.Fail(ErrorCode.AccountLocked, "登录失败次数过多，账号已锁定，请稍后重试")
                : ServiceResult<LoginOutcome>.Fail(ErrorCode.Unauthenticated, "用户名或密码错误");
        }

        // 已启用 TOTP 的账号一律要求验证码；服务端强制时同样要求
        if (user.TotpEnabled)
        {
            if (string.IsNullOrWhiteSpace(request.TotpCode))
            {
                return ServiceResult<LoginOutcome>.Fail(ErrorCode.Unauthenticated, "请输入二次验证码");
            }

            if (user.TotpSecret is null
                || !_totp.Verify(SafeUnprotect(user.TotpSecret), request.TotpCode))
            {
                await WriteLogAsync(user.Username, ip, device, LoginResultKind.Failed, "二次验证码错误", cancellationToken).ConfigureAwait(false);
                return ServiceResult<LoginOutcome>.Fail(ErrorCode.Unauthenticated, "二次验证码错误");
            }
        }
        else if (_options.RequireTotp)
        {
            await WriteLogAsync(user.Username, ip, device, LoginResultKind.Denied, "未绑定二次验证", cancellationToken).ConfigureAwait(false);
            return ServiceResult<LoginOutcome>.Fail(ErrorCode.Unauthenticated, "该账号尚未绑定二次验证，请联系管理员");
        }

        // 登录成功：清失败计数、记最近登录
        user.FailCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        user.LastLoginIp = ip;
        _users.Update(user);

        var resolved = await _permissions.ResolveAsync(user.Id, cancellationToken).ConfigureAwait(false);
        if (resolved is null)
        {
            return ServiceResult<LoginOutcome>.Fail(ErrorCode.Unexpected, "角色配置异常，请联系管理员");
        }

        var (refreshToken, refreshExpiresAt) = await CreateSessionAsync(user, ip, device, request.RememberMe, cancellationToken).ConfigureAwait(false);
        await WriteLogAsync(user.Username, ip, device, LoginResultKind.Success, null, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var issued = _tokenIssuer.Issue(user, resolved.FunctionPoints, resolved.DataScope.ToString().ToLowerInvariant());
        var me = BuildMe(user, resolved);

        return ServiceResult<LoginOutcome>.Success(new LoginOutcome(
            new LoginResponse(issued.Token, "Bearer", _options.AccessTokenMinutes * 60, me, SaTime.Format(now)),
            refreshToken,
            refreshExpiresAt,
            user.MustChangePwd));
    }

    /// <summary>
    /// 用刷新令牌换取新的访问令牌，并轮换刷新令牌（旧的立即吊销）。
    /// </summary>
    public async Task<ServiceResult<LoginOutcome>> RefreshAsync(
        string? refreshToken,
        string? ip,
        string? device,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return ServiceResult<LoginOutcome>.Fail(ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var hash = _tokenHasher.Hash(refreshToken);
        var stored = await _sessions.FindRefreshTokenAsync(hash, cancellationToken).ConfigureAwait(false);
        var now = SaTime.Now;

        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= now)
        {
            return ServiceResult<LoginOutcome>.Fail(ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var user = await _users.FindByIdAsync(stored.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null || string.Equals(user.Status, UserStatus.Disabled, StringComparison.Ordinal))
        {
            return ServiceResult<LoginOutcome>.Fail(ErrorCode.Unauthenticated, "账号不可用，请重新登录");
        }

        var resolved = await _permissions.ResolveAsync(user.Id, cancellationToken).ConfigureAwait(false);
        if (resolved is null)
        {
            return ServiceResult<LoginOutcome>.Fail(ErrorCode.Unexpected, "角色配置异常，请联系管理员");
        }

        // 轮换：旧令牌吊销，新令牌继承剩余有效期上限
        _sessions.RevokeRefreshToken(stored, now);
        var remainingHours = Math.Max(1, (int)Math.Ceiling((stored.ExpiresAt - now).TotalHours));
        var (newToken, newExpiresAt) = await CreateSessionAsync(
            user,
            ip,
            device,
            rememberMe: remainingHours > _options.RefreshTokenHours / 2,
            cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var issued = _tokenIssuer.Issue(user, resolved.FunctionPoints, resolved.DataScope.ToString().ToLowerInvariant());
        var me = BuildMe(user, resolved);

        return ServiceResult<LoginOutcome>.Success(new LoginOutcome(
            new LoginResponse(issued.Token, "Bearer", _options.AccessTokenMinutes * 60, me, SaTime.Format(now)),
            newToken,
            newExpiresAt,
            user.MustChangePwd));
    }

    /// <summary>
    /// 登出：吊销当前刷新令牌。幂等——令牌不存在也返回成功。
    /// </summary>
    public async Task<ServiceResult<bool>> LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var stored = await _sessions.FindRefreshTokenAsync(_tokenHasher.Hash(refreshToken), cancellationToken).ConfigureAwait(false);
            if (stored is not null && stored.RevokedAt is null)
            {
                _sessions.RevokeRefreshToken(stored, SaTime.Now);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return ServiceResult<bool>.Success(true);
    }

    /// <summary>
    /// 修改密码。成功后吊销该账号全部会话，强制重新登录。
    /// </summary>
    public async Task<ServiceResult<bool>> ChangePasswordAsync(
        string userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return ServiceResult<bool>.Fail(ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
        {
            return ServiceResult<bool>.Fail(ErrorCode.InvalidParameter, "当前密码不正确");
        }

        var policyError = _passwordPolicy.Validate(request.NewPassword);
        if (policyError is not null)
        {
            return ServiceResult<bool>.Fail(ErrorCode.InvalidParameter, policyError);
        }

        var history = await _users.GetRecentPasswordsAsync(userId, _passwordPolicy.HistoryRetention, cancellationToken).ConfigureAwait(false);
        if (_passwordPolicy.IsReused(request.NewPassword, history))
        {
            return ServiceResult<bool>.Fail(ErrorCode.InvalidParameter, $"新密码不能与最近 {_passwordPolicy.HistoryRetention} 次使用过的密码相同");
        }

        var result = _hasher.Hash(request.NewPassword);
        await _users.AddPasswordHistoryAsync(userId, user.PasswordHash, user.PasswordSalt, user.PasswordIterations, cancellationToken).ConfigureAwait(false);

        user.PasswordHash = result.Hash;
        user.PasswordSalt = result.Salt;
        user.PasswordIterations = result.Iterations;
        user.PasswordChangedAt = SaTime.Now;
        user.MustChangePwd = false;
        _users.Update(user);

        await _sessions.RevokeAllRefreshTokensAsync(userId, SaTime.Now, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ServiceResult<bool>.Success(true);
    }

    /// <summary>
    /// 生成 TOTP 密钥并暂存（此时尚未启用，需再调用 <see cref="EnableTotpAsync"/> 校验一次验证码）。
    /// </summary>
    public async Task<ServiceResult<TotpSetupDto>> SetupTotpAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return ServiceResult<TotpSetupDto>.Fail(ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var secret = _totp.CreateSecret();
        user.TotpSecret = _secretProtector.Protect(secret);
        user.TotpEnabled = false;
        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ServiceResult<TotpSetupDto>.Success(
            new TotpSetupDto(secret, _totp.BuildOtpAuthUri("SA 股析", user.Username, secret)));
    }

    /// <summary>
    /// 校验并启用 TOTP。
    /// </summary>
    public async Task<ServiceResult<bool>> EnableTotpAsync(
        string userId,
        TotpEnableRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return ServiceResult<bool>.Fail(ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var secret = string.IsNullOrWhiteSpace(request.Secret)
            ? (user.TotpSecret is null ? null : SafeUnprotect(user.TotpSecret))
            : request.Secret;

        if (secret is null || !_totp.Verify(secret, request.Code))
        {
            return ServiceResult<bool>.Fail(ErrorCode.InvalidParameter, "验证码不正确，请检查验证器时间后重试");
        }

        user.TotpSecret = _secretProtector.Protect(request.Secret ?? secret);
        user.TotpEnabled = true;
        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ServiceResult<bool>.Success(true);
    }

    /// <summary>
    /// 关闭 TOTP（需当前密码确认由端点层校验，这里只做状态变更）。
    /// </summary>
    public async Task<ServiceResult<bool>> DisableTotpAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return ServiceResult<bool>.Fail(ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        if (_options.RequireTotp)
        {
            return ServiceResult<bool>.Fail(ErrorCode.NoPermission, "服务端已强制二次验证，不能关闭");
        }

        user.TotpEnabled = false;
        user.TotpSecret = null;
        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ServiceResult<bool>.Success(true);
    }

    /// <summary>
    /// 创建会话（刷新令牌），并按 <c>MaxSessionsPerUser</c> 淘汰最老的会话。
    /// </summary>
    private async Task<(string Token, DateTimeOffset ExpiresAt)> CreateSessionAsync(
        User user,
        string? ip,
        string? device,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        var active = await _sessions.GetActiveSessionsAsync(user.Id, cancellationToken).ConfigureAwait(false);
        if (active.Count >= _options.MaxSessionsPerUser)
        {
            var now = SaTime.Now;
            foreach (var stale in active.OrderBy(t => t.CreatedAt).Take(active.Count - _options.MaxSessionsPerUser + 1))
            {
                _sessions.RevokeRefreshToken(stale, now);
            }
        }

        var token = _tokenHasher.CreateToken();
        var hours = rememberMe ? _options.RefreshTokenHours : Math.Max(1, _options.RefreshTokenHours / 3);
        var expiresAt = SaTime.Now.AddHours(hours);

        await _sessions.AddRefreshTokenAsync(
            new RefreshToken
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = user.Id,
                TokenHash = _tokenHasher.Hash(token),
                Device = device,
                Ip = ip,
                ExpiresAt = expiresAt,
                CreatedAt = SaTime.Now
            },
            cancellationToken).ConfigureAwait(false);

        return (token, expiresAt);
    }

    private Task WriteLogAsync(
        string? username,
        string? ip,
        string? device,
        string result,
        string? note,
        CancellationToken cancellationToken) =>
        _sessions.AddLoginLogAsync(
            new LoginLog
            {
                UserName = username,
                Ip = ip,
                Device = device,
                Result = result,
                Note = note,
                CreatedAt = SaTime.Now
            },
            cancellationToken);

    private string SafeUnprotect(string value)
    {
        try
        {
            return _secretProtector.Unprotect(value);
        }
        catch (Exception)
        {
            // 密钥轮换或配置变更会导致历史密文不可解，此时视为未绑定，要求重新绑定
            return string.Empty;
        }
    }

    private MeDto BuildMe(User user, PermissionService.ResolvedPermissions resolved) =>
        new(
            user.Id,
            user.Username,
            user.Nickname,
            resolved.RoleId,
            resolved.RoleName,
            user.MustChangePwd,
            user.TotpEnabled,
            _options.RequireTotp,
            resolved.DataScope.ToString().ToLowerInvariant(),
            resolved.FunctionPoints,
            resolved.Quotas);
}
