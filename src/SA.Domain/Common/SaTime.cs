namespace SA.Domain.Common;

/// <summary>
/// 业务时间的唯一来源。全系统统一使用 Asia/Shanghai，禁止直接使用本地时区或 UTC 拼装业务日期
/// （实施计划 §10「时区与精度」）。
/// </summary>
public static class SaTime
{
    private const string IanaZoneId = "Asia/Shanghai";
    private const string WindowsZoneId = "China Standard Time";

    /// <summary>业务时区。</summary>
    public static TimeZoneInfo Zone { get; } = ResolveZone();

    /// <summary>当前业务时间。</summary>
    public static DateTimeOffset Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone);

    /// <summary>当前业务日期。</summary>
    public static DateOnly Today => DateOnly.FromDateTime(Now.DateTime);

    /// <summary>
    /// 将任意时刻转换为业务时区的本地时间。
    /// </summary>
    public static DateTime ToLocal(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone).DateTime;

    /// <summary>
    /// 按约定的 <c>yyyy-MM-dd HH:mm:ss</c> 格式输出业务时间。
    /// </summary>
    public static string Format(DateTimeOffset instant) => ToLocal(instant).ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// 按约定的 <c>yyyy-MM-dd</c> 格式输出日期。
    /// </summary>
    public static string Format(DateOnly date) => date.ToString("yyyy-MM-dd");

    private static TimeZoneInfo ResolveZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(IanaZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // 老旧 Windows 或裁剪过的 tzdata 上回退到 Windows 时区 ID
            return TimeZoneInfo.FindSystemTimeZoneById(WindowsZoneId);
        }
    }
}
