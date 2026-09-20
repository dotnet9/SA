namespace SA.Application.Common;

/// <summary>
/// 展示精度的统一收敛点。
/// </summary>
/// <remarks>
/// 报价类字段在 SQLite 里是 REAL（双精度），十进制小数往返后会带出
/// <c>3911.8699999999998908606357872</c> 这类尾数。接口是面向展示的，
/// 因此在组装响应时统一收敛到 2 位小数，而不是把浮点尾数透给前端。
/// 原始精度由上游决定（价 2 位、比率 2 位），收敛不会丢失有效信息。
/// </remarks>
public static class Display
{
    /// <summary>收敛到指定小数位（默认 2 位，与详细设计 §1.3 的口径一致）。</summary>
    public static decimal Round(decimal value, int decimals = 2) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    /// <summary>可空版本。</summary>
    public static decimal? Round(decimal? value, int decimals = 2) =>
        value is null ? null : Round(value.Value, decimals);

    /// <summary>元转亿元（金额统一以亿元呈现，详细设计 §1.3）。</summary>
    public static decimal ToYi(decimal yuan) => Round(yuan / 100_000_000m);
}
