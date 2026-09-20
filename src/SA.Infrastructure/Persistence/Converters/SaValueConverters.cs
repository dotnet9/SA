using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SA.Domain.Common;

namespace SA.Infrastructure.Persistence.Converters;

/// <summary>
/// 业务时间与 SQLite TEXT 的互转。约定见 docs/详细设计.md §1.3：
/// 时间一律以 <c>Asia/Shanghai</c> 的 <c>yyyy-MM-dd HH:mm:ss</c> 字符串入库，
/// 这样按字符串排序即等价于按时间排序，且用任意工具打开库都可读。
/// </summary>
public sealed class SaDateTimeOffsetConverter : ValueConverter<DateTimeOffset, string>
{
    /// <summary>数据库中的时间格式。</summary>
    public const string Format = "yyyy-MM-dd HH:mm:ss";

    /// <summary>构造转换器。</summary>
    public SaDateTimeOffsetConverter()
        : base(
            value => SaTime.ToLocal(value).ToString(Format, CultureInfo.InvariantCulture),
            text => Parse(text))
    {
    }

    /// <summary>把库中的字符串解析回业务时间。</summary>
    public static DateTimeOffset Parse(string text)
    {
        var local = DateTime.ParseExact(text, Format, CultureInfo.InvariantCulture, DateTimeStyles.None);
        return new DateTimeOffset(local, SaTime.Zone.GetUtcOffset(local));
    }
}

/// <summary>
/// 可空业务时间转换器。
/// </summary>
public sealed class SaNullableDateTimeOffsetConverter : ValueConverter<DateTimeOffset?, string?>
{
    /// <summary>构造转换器。</summary>
    public SaNullableDateTimeOffsetConverter()
        : base(
            value => value == null
                ? null
                : SaTime.ToLocal(value.Value).ToString(SaDateTimeOffsetConverter.Format, CultureInfo.InvariantCulture),
            text => string.IsNullOrWhiteSpace(text) ? null : SaDateTimeOffsetConverter.Parse(text))
    {
    }
}

/// <summary>
/// 业务日期与 TEXT 的互转（<c>yyyy-MM-dd</c>）。
/// </summary>
public sealed class SaDateOnlyConverter : ValueConverter<DateOnly, string>
{
    /// <summary>数据库中的日期格式。</summary>
    public const string Format = "yyyy-MM-dd";

    /// <summary>构造转换器。</summary>
    public SaDateOnlyConverter()
        : base(
            value => value.ToString(Format, CultureInfo.InvariantCulture),
            text => DateOnly.ParseExact(text, Format, CultureInfo.InvariantCulture))
    {
    }
}

/// <summary>
/// 可空业务日期转换器（如「两融口径日」可能与行情口径日不同，未采集时为 null）。
/// </summary>
public sealed class SaNullableDateOnlyConverter : ValueConverter<DateOnly?, string?>
{
    /// <summary>构造转换器。</summary>
    public SaNullableDateOnlyConverter()
        : base(
            value => value == null ? null : value.Value.ToString(SaDateOnlyConverter.Format, CultureInfo.InvariantCulture),
            text => string.IsNullOrWhiteSpace(text)
                ? null
                : DateOnly.ParseExact(text, SaDateOnlyConverter.Format, CultureInfo.InvariantCulture))
    {
    }
}
