namespace SA.Domain.Entities.System;

/// <summary>
/// 系统级键值设置。对应新增表 <c>AppSetting</c>（详细设计 DDL 未列，实施计划 §5.3 登记）。
/// 用于安全策略、VAPID 密钥、系统参数等不适合放配置文件的运行期设置。
/// </summary>
public class AppSetting
{
    /// <summary>键，取值见 <see cref="AppSettingKeys"/>。</summary>
    public required string Key { get; set; }

    /// <summary>值（统一存字符串，复杂结构自行序列化）。</summary>
    public required string Value { get; set; }

    /// <summary>更新时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// <c>AppSetting</c> 的键。
/// </summary>
public static class AppSettingKeys
{
    /// <summary>安全策略（JSON：密码策略、锁定策略、会话策略）。</summary>
    public const string SecurityPolicy = "security.policy";

    /// <summary>Web Push 公私钥（JSON）。</summary>
    public const string WebPushKeys = "webpush.keys";
}

/// <summary>
/// 个人设置。对应新增表 <c>UserSetting</c>（实施计划 §5.3）。
/// 主题、涨跌色、密度、推送间隔等以 JSON 整体存取，便于随原型演进增删字段。
/// </summary>
public class UserSetting
{
    /// <summary>用户 Id。</summary>
    public required string UserId { get; set; }

    /// <summary>设置内容（JSON）。</summary>
    public required string Json { get; set; }

    /// <summary>更新时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// 配额用量。对应新增表 <c>QuotaUsage</c>（实施计划 §5.3）。内存计数 + 落库，重启不丢。
/// </summary>
public class QuotaUsage
{
    /// <summary>用户 Id。</summary>
    public required string UserId { get; set; }

    /// <summary>业务日（Asia/Shanghai 的 yyyy-MM-dd）。</summary>
    public required string Day { get; set; }

    /// <summary>配额种类，取值见 <see cref="QuotaUsageKinds"/>。</summary>
    public required string Kind { get; set; }

    /// <summary>已用次数。</summary>
    public int Used { get; set; }
}

/// <summary>
/// 配额用量的种类。
/// </summary>
public static class QuotaUsageKinds
{
    /// <summary>分析查询次数（对应 <c>quota.daily</c>）。</summary>
    public const string Query = "query";
}

/// <summary>
/// 导出记录。对应新增表 <c>ExportLog</c>（实施计划 §5.3；架构设计 §9 要求导出记录写入日志）。
/// </summary>
public class ExportLog
{
    /// <summary>自增主键。</summary>
    public long Id { get; set; }

    /// <summary>操作人。</summary>
    public required string UserId { get; set; }

    /// <summary>导出数据集标识，如 market-rankings。</summary>
    public required string Dataset { get; set; }

    /// <summary>格式，如 csv。</summary>
    public required string Format { get; set; }

    /// <summary>导出行数。</summary>
    public int Rows { get; set; }

    /// <summary>导出时间。</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
