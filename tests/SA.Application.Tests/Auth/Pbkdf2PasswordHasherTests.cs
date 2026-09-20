using SA.Application.Auth;

namespace SA.Application.Tests.Auth;

/// <summary>
/// 密码哈希。重点是「同密码不同盐得到不同哈希」与「校验用固定时间比较」两件事，
/// 以及库中数据损坏时不能抛异常（否则一个坏行就能让登录接口 500）。
/// </summary>
public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void 哈希结果与明文无关且带盐()
    {
        var result = _hasher.Hash("SaTest!2026Pass");

        Assert.NotEqual("SaTest!2026Pass", result.Hash);
        Assert.False(string.IsNullOrWhiteSpace(result.Salt));
        Assert.Equal(Pbkdf2PasswordHasher.DefaultIterations, result.Iterations);
    }

    [Fact]
    public void 同一密码两次哈希不同()
    {
        var first = _hasher.Hash("SaTest!2026Pass");
        var second = _hasher.Hash("SaTest!2026Pass");

        Assert.NotEqual(first.Hash, second.Hash);
        Assert.NotEqual(first.Salt, second.Salt);
    }

    [Fact]
    public void 正确密码校验通过()
    {
        var result = _hasher.Hash("SaTest!2026Pass");

        Assert.True(_hasher.Verify("SaTest!2026Pass", result.Hash, result.Salt, result.Iterations));
    }

    [Fact]
    public void 错误密码校验失败()
    {
        var result = _hasher.Hash("SaTest!2026Pass");

        Assert.False(_hasher.Verify("SaTest!2026Pas", result.Hash, result.Salt, result.Iterations));
        Assert.False(_hasher.Verify("satest!2026pass", result.Hash, result.Salt, result.Iterations));
        Assert.False(_hasher.Verify(string.Empty, result.Hash, result.Salt, result.Iterations));
    }

    [Fact]
    public void 盐不同则同一密码的哈希不可互用()
    {
        var first = _hasher.Hash("SaTest!2026Pass");
        var second = _hasher.Hash("SaTest!2026Pass");

        Assert.False(_hasher.Verify("SaTest!2026Pass", first.Hash, second.Salt, second.Iterations));
    }

    [Theory]
    [InlineData("not-base64", "not-base64", 1000)]
    [InlineData("QQ==", "not-base64", 1000)]
    [InlineData("", "", 0)]
    public void 库中数据损坏时判定为校验失败而非抛异常(string hash, string salt, int iterations)
    {
        Assert.False(_hasher.Verify("SaTest!2026Pass", hash, salt, iterations));
    }
}
