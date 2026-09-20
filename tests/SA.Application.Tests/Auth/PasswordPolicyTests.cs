using SA.Application.Auth;

namespace SA.Application.Tests.Auth;

/// <summary>
/// 密码策略。规则来自需求规格 §9：最小长度、四类字符、不与最近 5 次重复。
/// </summary>
public class PasswordPolicyTests
{
    private static PasswordPolicy CreatePolicy(int minLength = 10)
    {
        var options = new AuthOptions { PasswordMinLength = minLength };
        return new PasswordPolicy(options, new Pbkdf2PasswordHasher());
    }

    [Theory]
    [InlineData("SaTest!2026Pass")]
    [InlineData("Abcdefghi1!")]
    public void 合规密码通过(string password)
    {
        Assert.Null(CreatePolicy().Validate(password));
    }

    [Theory]
    [InlineData("", "密码不能为空")]
    [InlineData("Ab1!x", "密码长度至少 10 位")]
    public void 长度不足被拒绝(string password, string expectedMessage)
    {
        Assert.Equal(expectedMessage, CreatePolicy().Validate(password));
    }

    [Fact]
    public void 缺少任一字符类别都被拒绝()
    {
        var policy = CreatePolicy();

        Assert.Equal("密码需同时包含大写与小写字母", policy.Validate("ALLUPPER1!XX"));
        Assert.Equal("密码需同时包含大写与小写字母", policy.Validate("alllower1!xx"));
        Assert.Equal("密码需包含数字", policy.Validate("NoDigitsHere!x"));
        Assert.Equal("密码需包含符号", policy.Validate("NoSymbolsHere1x"));
    }

    [Fact]
    public void 与最近使用的密码重复被识别()
    {
        var policy = CreatePolicy();
        var hasher = new Pbkdf2PasswordHasher();

        var previous = hasher.Hash("SaTest!2026Pass");
        var history = new List<SA.Application.Abstractions.PasswordHistoryEntry>
        {
            new(previous.Hash, previous.Salt, previous.Iterations)
        };

        Assert.True(policy.IsReused("SaTest!2026Pass", history));
        Assert.False(policy.IsReused("SaTest!2026Other", history));
    }

    [Fact]
    public void 只与最近五条历史比较()
    {
        var policy = CreatePolicy();
        var hasher = new Pbkdf2PasswordHasher();

        var target = hasher.Hash("SaTest!2026Pass");
        var history = new List<SA.Application.Abstractions.PasswordHistoryEntry>();
        for (var i = 0; i < 5; i++)
        {
            var filler = hasher.Hash($"Filler!2026No{i}");
            history.Add(new(filler.Hash, filler.Salt, filler.Iterations));
        }

        // 目标密码排在第 6 位，超出保留深度，不应再判定为重复
        history.Add(new(target.Hash, target.Salt, target.Iterations));

        Assert.False(policy.IsReused("SaTest!2026Pass", history));
        Assert.Equal(5, policy.HistoryRetention);
    }
}
