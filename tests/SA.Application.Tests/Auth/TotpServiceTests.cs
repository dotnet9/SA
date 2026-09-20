using SA.Application.Auth;

namespace SA.Application.Tests.Auth;

/// <summary>
/// TOTP 实现。用 RFC 6238 附录 B 的官方测试向量做锚点：
/// 密钥为 ASCII「12345678901234567890」（Base32：GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ），
/// SHA1 / 6 位 / 30 秒时，各时间点的期望验证码如下。
/// </summary>
public class TotpServiceTests
{
    private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    /// <summary>T=59 的官方验证码，用于验证窗口行为。</summary>
    private const string CodeAt59 = "287082";

    private readonly TotpService _totp = new();

    private static DateTimeOffset At(long unixSeconds) => DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void 与RFC6238测试向量一致(long unixSeconds, string expected)
    {
        Assert.True(_totp.Verify(RfcSecret, expected, window: 0, now: At(unixSeconds)), $"T={unixSeconds} 期望 {expected}");
    }

    [Fact]
    public void 窗口允许相邻时间步但拒绝更远的时间步()
    {
        // T=59 位于第 1 个时间步；T=89 位于第 2 步，T=119 位于第 3 步
        Assert.True(_totp.Verify(RfcSecret, CodeAt59, window: 0, now: At(59)));
        Assert.False(_totp.Verify(RfcSecret, CodeAt59, window: 0, now: At(89)));

        Assert.True(_totp.Verify(RfcSecret, CodeAt59, window: 1, now: At(89)));
        Assert.False(_totp.Verify(RfcSecret, CodeAt59, window: 1, now: At(119)));

        // 反向漂移：T=29 位于第 0 步，与 T=59 相差 1 步
        Assert.True(_totp.Verify(RfcSecret, CodeAt59, window: 1, now: At(29)));
        Assert.False(_totp.Verify(RfcSecret, CodeAt59, window: 0, now: At(29)));
    }

    [Fact]
    public void 位数不足或含非数字一律拒绝()
    {
        Assert.False(_totp.Verify(RfcSecret, "28708", window: 1, now: At(59)));
        Assert.False(_totp.Verify(RfcSecret, "2870821", window: 1, now: At(59)));
        Assert.False(_totp.Verify(RfcSecret, "28708a", window: 1, now: At(59)));
        Assert.False(_totp.Verify(RfcSecret, string.Empty, window: 1, now: At(59)));
    }

    [Fact]
    public void 密钥非法时拒绝而不是抛异常()
    {
        Assert.False(_totp.Verify("不是合法的 Base32", CodeAt59, window: 1, now: At(59)));
        Assert.False(_totp.Verify("0189", CodeAt59, window: 1, now: At(59)));
        Assert.False(_totp.Verify(string.Empty, CodeAt59, window: 1, now: At(59)));
    }

    [Fact]
    public void 换一个密钥后同一验证码不再通过()
    {
        var otherSecret = _totp.CreateSecret();

        Assert.False(_totp.Verify(otherSecret, CodeAt59, window: 1, now: At(59)));
    }

    [Fact]
    public void 生成的密钥为Base32且长度符合约定()
    {
        var secret = _totp.CreateSecret();

        Assert.Equal(32, secret.Length);
        Assert.All(secret, c => Assert.Contains(c, "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"));
    }

    [Fact]
    public void 绑定URI包含密钥与标准参数()
    {
        var uri = _totp.BuildOtpAuthUri("SA 股析", "admin", "ABCDEFGHIJKLMNOPQRSTUV");

        Assert.StartsWith("otpauth://totp/", uri, StringComparison.Ordinal);
        Assert.Contains("secret=ABCDEFGHIJKLMNOPQRSTUV", uri, StringComparison.Ordinal);
        Assert.Contains("algorithm=SHA1", uri, StringComparison.Ordinal);
        Assert.Contains("digits=6", uri, StringComparison.Ordinal);
        Assert.Contains("period=30", uri, StringComparison.Ordinal);
    }
}
