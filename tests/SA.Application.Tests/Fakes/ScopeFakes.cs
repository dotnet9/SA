using SA.Application.Abstractions;

namespace SA.Application.Tests;

/// <summary>
/// 数据范围相关的测试替身。
/// </summary>
/// <remarks>
/// 默认返回「不受限」（<c>null</c>）：绝大多数用例关心的是领域逻辑本身，
/// 数据范围过滤由专门的范围用例覆盖。
/// </remarks>
internal sealed class FakeUserContext(string? userId = "test-user") : SA.Application.Authorization.IUserContext
{
    /// <inheritdoc />
    public string? UserId { get; } = userId;
}

/// <summary>数据范围判定替身。</summary>
internal sealed class FakeDataScopeService(IReadOnlySet<string>? allowed = null) : IDataScopeService
{
    /// <inheritdoc />
    public Task<IReadOnlySet<string>?> AllowedCodesAsync(
        string userId,
        CancellationToken cancellationToken = default) => Task.FromResult(allowed);

    /// <inheritdoc />
    public Task<bool> IsAllowedAsync(
        string userId,
        string code,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(allowed is null || allowed.Contains(code));
}
