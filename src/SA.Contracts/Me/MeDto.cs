namespace SA.Contracts.Me;

/// <summary>
/// 当前用户与权限快照。前端据此裁剪路由、菜单与按钮，后端按同一份功能点判定
/// （需求规格 §7.3「避免看不到但能调」）。
/// </summary>
/// <param name="Id">用户 Id。</param>
/// <param name="Username">登录名。</param>
/// <param name="Nickname">显示名。</param>
/// <param name="RoleId">角色 Id。</param>
/// <param name="RoleName">角色显示名。</param>
/// <param name="MustChangePwd">是否需强制改密；为 true 时前端只允许进入改密页。</param>
/// <param name="TotpEnabled">该账号是否已启用二次验证。</param>
/// <param name="TotpRequired">服务端是否强制二次验证（配置项）。</param>
/// <param name="DataScope">数据范围：all / watchlist。</param>
/// <param name="FunctionPoints">功能点编码集合。</param>
/// <param name="Quotas">操作级权限参数。</param>
public sealed record MeDto(
    string Id,
    string Username,
    string Nickname,
    string RoleId,
    string RoleName,
    bool MustChangePwd,
    bool TotpEnabled,
    bool TotpRequired,
    string DataScope,
    IReadOnlyList<string> FunctionPoints,
    IReadOnlyDictionary<string, int> Quotas);

/// <summary>
/// 修改密码请求。
/// </summary>
/// <param name="CurrentPassword">当前密码；强制改密场景下仍要求填写。</param>
/// <param name="NewPassword">新密码。</param>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>
/// TOTP 绑定信息。密钥仅在绑定流程中返回一次。
/// </summary>
/// <param name="Secret">Base32 密钥，供手工录入。</param>
/// <param name="OtpAuthUri">otpauth:// URI，供验证器扫码。</param>
public sealed record TotpSetupDto(string Secret, string OtpAuthUri);

/// <summary>
/// 启用 TOTP 请求：提交绑定流程返回的密钥与当前验证码。
/// </summary>
/// <param name="Secret">绑定流程返回的密钥。</param>
/// <param name="Code">6 位验证码。</param>
public sealed record TotpEnableRequest(string Secret, string Code);

/// <summary>
/// 二次验证状态。
/// </summary>
/// <param name="Enabled">当前账号是否已绑定并启用。</param>
/// <param name="Required">服务端是否强制启用（配置项）。</param>
public sealed record TotpStatusDto(bool Enabled, bool Required);

/// <summary>
/// 权限快照（不含用户资料的轻量版本），前端用于裁剪路由、菜单与按钮。
/// </summary>
/// <param name="RoleId">角色 Id。</param>
/// <param name="RoleName">角色显示名。</param>
/// <param name="DataScope">数据范围：all / watchlist。</param>
/// <param name="FunctionPoints">功能点编码集合。</param>
/// <param name="Quotas">操作级权限参数。</param>
/// <param name="MustChangePwd">是否需强制改密。</param>
public sealed record PermissionsDto(
    string RoleId,
    string RoleName,
    string DataScope,
    IReadOnlyList<string> FunctionPoints,
    IReadOnlyDictionary<string, int> Quotas,
    bool MustChangePwd);
