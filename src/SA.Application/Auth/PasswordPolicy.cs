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

    /// <summary>
    /// 生成一个满足策略的随机密码，供管理员重置密码时使用。
    /// </summary>
    /// <remarks>
    /// 刻意排除易混淆字符（<c>0/O</c>、<c>1/l/I</c>）：管理员需要把这个密码口头或书面转交给用户，
    /// 混淆字符会显著提高转述出错率。字符集仍覆盖大小写字母、数字与符号，满足复杂度策略。
    /// </remarks>
    public static string Generate()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*-_=+";

        // 每类至少一个，保证必然满足「必须包含大小写字母与数字」的策略
        var chars = new List<char>
        {
            upper[Random.Shared.Next(upper.Length)],
            lower[Random.Shared.Next(lower.Length)],
            digits[Random.Shared.Next(digits.Length)],
            symbols[Random.Shared.Next(symbols.Length)]
        };

        var pool = upper + lower + digits + symbols;
        while (chars.Count < 16)
        {
            chars.Add(pool[Random.Shared.Next(pool.Length)]);
        }

        // 打乱位置：否则前四位的规律会泄露生成方式
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string([.. chars]);
    }
}
