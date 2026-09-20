using System.Text.Json;
using SA.Application.Abstractions;
using SA.Application.Auth;
using SA.Application.Authorization;
using SA.Application.Common;
using SA.Contracts.Common;
using SA.Contracts.Me;

namespace SA.Application.Services;

/// <summary>
/// 当前用户信息、权限快照与个人设置。
/// </summary>
public sealed class MeService(
    IUserStore users,
    ISettingsStore settings,
    PermissionService permissions,
    AuthOptions options,
    IUnitOfWork unitOfWork)
{
    private readonly IUserStore _users = users;
    private readonly ISettingsStore _settings = settings;
    private readonly PermissionService _permissions = permissions;
    private readonly AuthOptions _options = options;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    /// <summary>
    /// 取当前用户与权限快照。
    /// </summary>
    public async Task<ServiceResult<MeDto>> GetMeAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return ServiceResult<MeDto>.Fail(ErrorCode.Unauthenticated, "登录已失效，请重新登录");
        }

        var resolved = await _permissions.ResolveAsync(userId, cancellationToken).ConfigureAwait(false);
        if (resolved is null)
        {
            return ServiceResult<MeDto>.Fail(ErrorCode.Unexpected, "角色配置异常，请联系管理员");
        }

        return ServiceResult<MeDto>.Success(new MeDto(
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
            resolved.Quotas));
    }

    /// <summary>
    /// 取个人设置。未设置过时返回空对象，前端用本地默认值兜底。
    /// </summary>
    public async Task<ServiceResult<JsonElement>> GetSettingsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var json = await _settings.GetUserSettingsAsync(userId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            using var empty = JsonDocument.Parse("{}");
            return ServiceResult<JsonElement>.Success(empty.RootElement.Clone());
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return ServiceResult<JsonElement>.Success(document.RootElement.Clone());
        }
        catch (JsonException)
        {
            // 库中数据损坏时不应让整个个人设置不可用，按空设置返回
            using var empty = JsonDocument.Parse("{}");
            return ServiceResult<JsonElement>.Success(empty.RootElement.Clone());
        }
    }

    /// <summary>
    /// 覆盖保存个人设置（整体替换，前端提交完整对象）。
    /// </summary>
    public async Task<ServiceResult<JsonElement>> UpdateSettingsAsync(
        string userId,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return ServiceResult<JsonElement>.Fail(ErrorCode.InvalidParameter, "设置内容必须是 JSON 对象");
        }

        var json = payload.GetRawText();
        if (json.Length > 32 * 1024)
        {
            return ServiceResult<JsonElement>.Fail(ErrorCode.InvalidParameter, "设置内容过大");
        }

        await _settings.SetUserSettingsAsync(userId, json, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ServiceResult<JsonElement>.Success(payload.Clone());
    }
}
