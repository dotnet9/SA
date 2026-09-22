using SA.Domain.Common;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// F10（<c>emweb.securities.eastmoney.com/PC_HSF10/*</c>）的代码参数拼装。
/// </summary>
/// <remarks>
/// F10 端点要的是「后缀在前」的形态（<c>SH688110</c>），与
/// <see cref="MarketCodes.SecUCode"/> 的 <c>688110.SH</c> 正好相反。
/// 集中在这里而不是每个适配器各拼一次：拼错的后果是拿到空数据而不报错。
/// </remarks>
internal static class F10Codes
{
    /// <summary>拼出 F10 的 <c>code</c> 参数：<c>688110</c> → <c>SH688110</c>。</summary>
    public static string Of(string code)
    {
        var secUCode = MarketCodes.SecUCode(code);
        var dot = secUCode.LastIndexOf('.');
        return dot < 0 ? secUCode : secUCode[(dot + 1)..] + secUCode[..dot];
    }
}
