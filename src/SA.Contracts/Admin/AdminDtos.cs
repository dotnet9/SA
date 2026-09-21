namespace SA.Contracts.Admin;

/// <summary>数据源状态一行。</summary>
/// <param name="Name">数据源名。</param>
/// <param name="Type">主源 / 备源。</param>
/// <param name="Domains">承载的域。</param>
/// <param name="Status">ok / warn / err / idle。</param>
/// <param name="LastOkAt">最近成功时间。</param>
/// <param name="LatencyMs">最近耗时（毫秒）。</param>
/// <param name="FailCount">连续失败次数。</param>
/// <param name="LastError">最近错误摘要。</param>
public sealed record DataSourceStatusRowDto(
    string Name,
    string? Type,
    string? Domains,
    string Status,
    string? LastOkAt,
    int? LatencyMs,
    int FailCount,
    string? LastError);

/// <summary>采集任务一行。</summary>
/// <param name="Id">自增 Id。</param>
/// <param name="TaskName">任务名。</param>
/// <param name="Source">数据源。</param>
/// <param name="Status">ok / warn / err。</param>
/// <param name="StartedAt">开始时间。</param>
/// <param name="CostMs">耗时（毫秒）。</param>
/// <param name="RowsWritten">写入行数。</param>
/// <param name="Error">错误摘要。</param>
public sealed record CollectTaskRowDto(
    long Id,
    string TaskName,
    string? Source,
    string Status,
    string StartedAt,
    int? CostMs,
    int? RowsWritten,
    string? Error);

/// <summary>数据源监控。</summary>
/// <param name="Sources">各数据源状态。</param>
/// <param name="Tasks">最近采集任务。</param>
/// <param name="DegradedCount">降级或不可用的数据源数量。</param>
public sealed record DataSourceMonitorDto(
    IReadOnlyList<DataSourceStatusRowDto> Sources,
    IReadOnlyList<CollectTaskRowDto> Tasks,
    int DegradedCount);

/// <summary>用户一行。</summary>
/// <param name="Id">用户 Id。</param>
/// <param name="Username">用户名。</param>
/// <param name="Nickname">昵称。</param>
/// <param name="RoleId">角色 Id。</param>
/// <param name="RoleName">角色名。</param>
/// <param name="Status">active / disabled / locked。</param>
/// <param name="MustChangePwd">是否需强制改密。</param>
/// <param name="HasTotp">是否已绑定 TOTP。</param>
/// <param name="LastLoginAt">最近登录时间。</param>
/// <param name="CreatedAt">创建时间。</param>
public sealed record UserRowDto(
    string Id,
    string Username,
    string Nickname,
    string RoleId,
    string RoleName,
    string Status,
    bool MustChangePwd,
    bool HasTotp,
    string? LastLoginAt,
    string CreatedAt);

/// <summary>用户列表。</summary>
/// <param name="Items">用户。</param>
/// <param name="Total">返回条数。</param>
/// <param name="ActiveCount">启用数。</param>
/// <param name="DisabledCount">停用数。</param>
/// <param name="LockedCount">锁定数。</param>
public sealed record UserListDto(
    IReadOnlyList<UserRowDto> Items,
    int Total,
    int ActiveCount,
    int DisabledCount,
    int LockedCount);

/// <summary>改用户请求（只传需要变更的字段）。</summary>
/// <param name="Nickname">昵称。</param>
/// <param name="RoleId">角色。</param>
/// <param name="Status">状态：active / disabled。</param>
/// <param name="MustChangePwd">是否强制改密。</param>
public sealed record UserUpdateRequest(
    string? Nickname,
    string? RoleId,
    string? Status,
    bool? MustChangePwd);

/// <summary>创建用户请求。</summary>
/// <param name="Username">用户名。</param>
/// <param name="Nickname">昵称。</param>
/// <param name="RoleId">角色。</param>
/// <param name="Password">初始密码；留空时由系统生成并返回一次。</param>
public sealed record UserCreateRequest(string Username, string? Nickname, string RoleId, string? Password);

/// <summary>重置密码请求。</summary>
/// <param name="NewPassword">新密码；留空时由系统生成并返回一次。</param>
public sealed record PasswordResetRequest(string? NewPassword);

/// <summary>功能点描述。</summary>
/// <param name="Code">功能点编码。</param>
/// <param name="Name">名称。</param>
/// <param name="Group">分组（权限矩阵的展示顺序）。</param>
/// <param name="IsPublic">
/// 是否公开功能点。公开功能点对应匿名即可访问的接口，因此不参与授权判定；
/// 界面必须据此标注为「公开」，否则会呈现一个关不掉的假开关。
/// </param>
public sealed record FunctionPointDto(string Code, string Name, string Group, bool IsPublic);

/// <summary>角色一行（含功能点与配额）。</summary>
/// <param name="Id">角色 Id。</param>
/// <param name="Name">角色名。</param>
/// <param name="Description">说明。</param>
/// <param name="IsBuiltin">是否内置（内置角色不可删除）。</param>
/// <param name="FunctionPoints">功能点编码集合。</param>
/// <param name="Quotas">配额。</param>
/// <param name="UserCount">该角色下的用户数。</param>
public sealed record RoleRowDto(
    string Id,
    string Name,
    string? Description,
    bool IsBuiltin,
    IReadOnlyList<string> FunctionPoints,
    IReadOnlyDictionary<string, int> Quotas,
    int UserCount);

/// <summary>权限矩阵。</summary>
/// <param name="FunctionPoints">全部功能点（含名称与分组）。</param>
/// <param name="Roles">全部角色。</param>
/// <param name="QuotaKeys">可配置的配额键。</param>
/// <param name="DataScopes">数据范围选项。</param>
public sealed record PermissionMatrixDto(
    IReadOnlyList<FunctionPointDto> FunctionPoints,
    IReadOnlyList<RoleRowDto> Roles,
    IReadOnlyList<string> QuotaKeys,
    IReadOnlyList<DataScopeOptionDto> DataScopes);

/// <summary>数据范围选项。</summary>
/// <param name="Code">功能点编码。</param>
/// <param name="Name">名称。</param>
/// <param name="Description">说明。</param>
public sealed record DataScopeOptionDto(string Code, string Name, string Description);

/// <summary>功能点更新请求。</summary>
/// <param name="Codes">功能点编码集合（全量覆盖）。</param>
public sealed record RoleFunctionPointsRequest(IReadOnlyList<string> Codes);

/// <summary>角色配额更新请求。</summary>
/// <param name="Quotas">配额键值对（只更新传入的键）。</param>
public sealed record RoleQuotasRequest(IReadOnlyDictionary<string, int> Quotas);

/// <summary>登录日志一行。</summary>
/// <param name="Id">Id。</param>
/// <param name="UserName">登录名（失败尝试也记录，含不存在的用户名）。</param>
/// <param name="Result">结果：success / failed / denied。</param>
/// <param name="Ip">来源 IP。</param>
/// <param name="Device">设备标识。</param>
/// <param name="Note">备注（失败原因等）。</param>
/// <param name="CreatedAt">时间。</param>
/// <param name="Success">是否成功（由 <paramref name="Result"/> 推导，便于界面直接渲染）。</param>
public sealed record LoginLogRowDto(
    long Id,
    string? UserName,
    string Result,
    string? Ip,
    string? Device,
    string? Note,
    string CreatedAt,
    bool Success);

/// <summary>会话与登录日志。</summary>
/// <param name="Logs">登录日志。</param>
/// <param name="ActiveSessionCount">当前有效会话总数。</param>
/// <param name="FailedToday">今日失败登录次数。</param>
public sealed record SessionListDto(
    IReadOnlyList<LoginLogRowDto> Logs,
    int ActiveSessionCount,
    int FailedToday);

/// <summary>设置项。</summary>
/// <param name="Key">键。</param>
/// <param name="Name">名称。</param>
/// <param name="Description">说明。</param>
/// <param name="Value">当前值。</param>
/// <param name="Configured">是否已在库中显式配置过。</param>
public sealed record SettingRowDto(
    string Key,
    string Name,
    string Description,
    string Value,
    bool Configured);

/// <summary>系统状态与存储占用。</summary>
/// <param name="DataRoot">数据根目录。</param>
/// <param name="DatabasePath">数据库文件。</param>
/// <param name="ParquetPath">Parquet 目录。</param>
/// <param name="LogPath">日志目录。</param>
/// <param name="DatabaseSizeBytes">数据库大小（字节）。</param>
/// <param name="ParquetSizeBytes">Parquet 占用（字节）。</param>
/// <param name="LogSizeBytes">日志占用（字节）。</param>
/// <param name="Settings">可修改的设置项。</param>
public sealed record SystemStateDto(
    string DataRoot,
    string DatabasePath,
    string ParquetPath,
    string LogPath,
    long DatabaseSizeBytes,
    long ParquetSizeBytes,
    long LogSizeBytes,
    IReadOnlyList<SettingRowDto> Settings);

/// <summary>设置更新请求。</summary>
/// <param name="Values">键值对。</param>
public sealed record SettingsUpdateRequest(IReadOnlyDictionary<string, string> Values);

/// <summary>审计日志一行。</summary>
/// <param name="Id">Id。</param>
/// <param name="Username">操作人。</param>
/// <param name="Action">动作。</param>
/// <param name="Target">目标。</param>
/// <param name="Detail">变更摘要。</param>
/// <param name="Ip">来源 IP。</param>
/// <param name="CreatedAt">时间。</param>
public sealed record AuditRowDto(
    long Id,
    string? Username,
    string Action,
    string? Target,
    string? Detail,
    string? Ip,
    string CreatedAt);
