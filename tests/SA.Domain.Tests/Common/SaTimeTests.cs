using SA.Domain.Common;

namespace SA.Domain.Tests.Common;

/// <summary>
/// 业务时间。全系统只允许通过 SaTime 取时间，避免本地时区与 UTC 混用导致的「日期错一天」。
/// </summary>
public class SaTimeTests
{
    [Fact]
    public void 业务时区为东八区且无夏令时()
    {
        Assert.Equal(TimeSpan.FromHours(8), SaTime.Zone.BaseUtcOffset);
        Assert.False(SaTime.Zone.SupportsDaylightSavingTime);
    }

    [Fact]
    public void UTC下午四点归属当日业务时间()
    {
        // 2026-09-18T07:30Z = 北京时间 15:30，仍属 9 月 18 日
        var instant = new DateTimeOffset(2026, 9, 18, 7, 30, 0, TimeSpan.Zero);

        Assert.Equal("2026-09-18 15:30:00", SaTime.Format(instant));
        Assert.Equal(new DateOnly(2026, 9, 18), DateOnly.FromDateTime(SaTime.ToLocal(instant)));
    }

    [Fact]
    public void UTC晚间归属次日业务日期()
    {
        // 2026-09-18T16:30Z = 北京时间 2026-09-19 00:30，跨日
        var instant = new DateTimeOffset(2026, 9, 18, 16, 30, 0, TimeSpan.Zero);

        Assert.Equal("2026-09-19 00:30:00", SaTime.Format(instant));
        Assert.Equal(new DateOnly(2026, 9, 19), DateOnly.FromDateTime(SaTime.ToLocal(instant)));
    }

    [Fact]
    public void 日期格式为紧凑短横线()
    {
        Assert.Equal("2026-09-18", SaTime.Format(new DateOnly(2026, 9, 18)));
    }
}
