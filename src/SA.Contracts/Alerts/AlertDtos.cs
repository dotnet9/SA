namespace SA.Contracts.Alerts;

/// <summary>
/// 提醒规则。
/// </summary>
/// <param name="Id">规则 Id。</param>
/// <param name="Code">证券代码。</param>
/// <param name="Name">证券名称。</param>
/// <param name="RuleType">规则类型。</param>
/// <param name="RuleTypeName">类型中文名。</param>
/// <param name="Threshold">阈值。</param>
/// <param name="ThresholdUnit">阈值单位（元 / % / 日）。</param>
/// <param name="Note">备注。</param>
/// <param name="Enabled">是否启用。</param>
/// <param name="CreatedAt">创建时间。</param>
/// <param name="LastTriggeredAt">最近触发时间。</param>
/// <param name="TriggerCount">累计触发次数。</param>
public sealed record AlertRuleDto(
    string Id,
    string Code,
    string Name,
    string RuleType,
    string RuleTypeName,
    decimal? Threshold,
    string? ThresholdUnit,
    string? Note,
    bool Enabled,
    string CreatedAt,
    string? LastTriggeredAt,
    int TriggerCount);

/// <summary>
/// 提醒规则列表。
/// </summary>
/// <param name="Rules">规则。</param>
/// <param name="Total">规则总数（配额对照）。</param>
/// <param name="Quota">角色配额上限。</param>
/// <param name="Types">可选规则类型（界面据此渲染下拉框）。</param>
/// <param name="AsOf">行情时间。</param>
public sealed record AlertRuleListDto(
    IReadOnlyList<AlertRuleDto> Rules,
    int Total,
    int Quota,
    IReadOnlyList<AlertTypeOptionDto> Types,
    string? AsOf);

/// <summary>
/// 可选的规则类型。
/// </summary>
/// <param name="Type">类型值。</param>
/// <param name="Name">中文名。</param>
/// <param name="Unit">阈值单位；为空表示该类型不需要阈值。</param>
/// <param name="Hint">填写提示。</param>
public sealed record AlertTypeOptionDto(string Type, string Name, string? Unit, string Hint);

/// <summary>新建规则请求。</summary>
/// <param name="Code">证券代码。</param>
/// <param name="RuleType">规则类型。</param>
/// <param name="Threshold">阈值。</param>
/// <param name="Note">备注。</param>
public sealed record AlertRuleCreateRequest(string Code, string RuleType, decimal? Threshold, string? Note);

/// <summary>修改规则请求（只传需要变更的字段）。</summary>
/// <param name="Threshold">阈值。</param>
/// <param name="Note">备注。</param>
/// <param name="Enabled">是否启用。</param>
public sealed record AlertRuleUpdateRequest(decimal? Threshold, string? Note, bool? Enabled);

/// <summary>
/// 站内通知。
/// </summary>
/// <param name="Id">通知 Id。</param>
/// <param name="Title">标题。</param>
/// <param name="Body">正文。</param>
/// <param name="Level">级别。</param>
/// <param name="Code">相关证券代码。</param>
/// <param name="RuleId">触发它的规则 Id。</param>
/// <param name="IsRead">是否已读。</param>
/// <param name="CreatedAt">时间。</param>
public sealed record NotificationDto(
    long Id,
    string Title,
    string Body,
    string Level,
    string? Code,
    string? RuleId,
    bool IsRead,
    string CreatedAt);

/// <summary>
/// 通知列表。
/// </summary>
/// <param name="Items">通知。</param>
/// <param name="Unread">未读数量（界面角标）。</param>
/// <param name="Total">返回条数。</param>
public sealed record NotificationListDto(IReadOnlyList<NotificationDto> Items, int Unread, int Total);

/// <summary>标记已读请求（<c>ids</c> 为空表示全部已读）。</summary>
/// <param name="Ids">通知 Id 集合。</param>
public sealed record NotificationReadRequest(IReadOnlyList<long>? Ids);

/// <summary>VAPID 公钥。</summary>
/// <param name="PublicKey">base64url 编码的公钥；前端用它调用 <c>pushManager.subscribe</c>。</param>
public sealed record PushKeyDto(string PublicKey);

/// <summary>浏览器推送订阅请求。</summary>
/// <param name="Endpoint">推送端点。</param>
/// <param name="Keys">订阅密钥。</param>
public sealed record PushSubscribeRequest(string Endpoint, PushSubscribeKeys? Keys);

/// <summary>订阅密钥。</summary>
/// <param name="P256dh">客户端公钥（base64url）。</param>
/// <param name="Auth">认证密钥（base64url）。</param>
public sealed record PushSubscribeKeys(string? P256dh, string? Auth);

/// <summary>取消订阅请求。</summary>
/// <param name="Endpoint">推送端点。</param>
public sealed record PushUnsubscribeRequest(string Endpoint);

/// <summary>一条订阅（界面展示用；端点是长 URL，只回传主机名）。</summary>
/// <param name="Id">订阅 Id。</param>
/// <param name="Host">推送服务主机名。</param>
/// <param name="UserAgent">设备 UA（截断）。</param>
/// <param name="Enabled">是否启用。</param>
/// <param name="FailCount">连续失败次数。</param>
/// <param name="LastOkAt">最近成功时间。</param>
/// <param name="LastError">最近错误。</param>
/// <param name="CreatedAt">订阅时间。</param>
public sealed record PushSubscriptionRowDto(
    long Id,
    string Host,
    string? UserAgent,
    bool Enabled,
    int FailCount,
    string? LastOkAt,
    string? LastError,
    string CreatedAt);

/// <summary>提醒偏好（免打扰与渠道开关）。</summary>
/// <param name="DndEnabled">是否启用免打扰。</param>
/// <param name="DndFrom">开始时刻（HH:mm）。</param>
/// <param name="DndTo">结束时刻（HH:mm）。</param>
/// <param name="DndKeepInbox">免打扰期间是否仍写站内通知（默认是）。</param>
/// <param name="PushEnabled">是否启用浏览器推送。</param>
public sealed record NotifySettingsDto(
    bool DndEnabled,
    string DndFrom,
    string DndTo,
    bool DndKeepInbox,
    bool PushEnabled);

/// <summary>保存提醒偏好请求。</summary>
/// <param name="DndEnabled">是否启用免打扰。</param>
/// <param name="DndFrom">开始时刻（HH:mm）。</param>
/// <param name="DndTo">结束时刻（HH:mm）。</param>
/// <param name="DndKeepInbox">免打扰期间是否仍写站内通知。</param>
/// <param name="PushEnabled">是否启用浏览器推送。</param>
public sealed record NotifySettingsRequest(
    bool DndEnabled,
    string DndFrom,
    string DndTo,
    bool DndKeepInbox,
    bool PushEnabled);
