using SA.Application.Abstractions;

namespace SA.Application.Auth;

/// <summary>
/// 密码策略校验。规则依据需求规格 §9「安全设计」：最小长度、四类字符、不与最近 N 次重复。
/// </summary>
public sealed class PasswordPolicy(AuthOptions options, IPasswordHasher hasher)
{
    private const int HistoryDepth = 5;
    private readonly AuthOptions _options = options;
    private readonly IPasswordHasher _hasher = hasher;

    /// <summary>
    /// 校验密码是否符合策略。返回 null 表示通过，否则返回面向用户的错误说明。
    /// </summary>
    public string? Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return "密码不能为空";
        }

        if (password.Length < _options.PasswordMinLength)
        {
            return $"密码长度至少 {_options.PasswordMinLength} 位";
        }

        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsAsciiDigit);
        var hasSymbol = password.Any(c => !char.IsLetterOrDigit(c));

        if (!(hasUpper && hasLower))
        {
            return "密码需同时包含大写与小写字母";
        }

        if (!hasDigit)
        {
            return "密码需包含数字";
        }

        if (!hasSymbol)
        {
            return "密码需包含符号";
        }

        return null;
    }

    /// <summary>
    /// 校验新密码是否与最近若干次历史重复。需要历史条目由调用方从存储读取。
    /// </summary>
    public bool IsReused(string newPassword, IReadOnlyList<PasswordHistoryEntry> history)
    {
        foreach (var entry in history.Take(HistoryDepth))
        {
            if (_hasher.Verify(newPassword, entry.Hash, entry.Salt, entry.Iterations))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>历史保留深度，供调用方决定读取条数。</summary>
    public int HistoryRetention => HistoryDepth;
}
