using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SA.Api.Auth;
using SA.Domain.Authorization;
using SA.Infrastructure.Security;

namespace SA.Api.Tests;

/// <summary>
/// 功能点授权判定。这是「后端逐接口校验」的核心逻辑，必须独立于 HTTP 管线被验证：
/// 有权限放行、缺少任一所需功能点即拒绝、未认证不放行。
/// </summary>
public class FunctionPointAuthorizationHandlerTests
{
    private readonly FunctionPointAuthorizationHandler _handler = new();

    [Fact]
    public async Task 具备全部所需功能点时放行()
    {
        var context = await EvaluateAsync(
            authenticated: true,
            granted: [FunctionPointCatalog.MarketView, FunctionPointCatalog.StockTrend],
            required: [FunctionPointCatalog.MarketView]);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task 缺少任一所需功能点时拒绝()
    {
        var context = await EvaluateAsync(
            authenticated: true,
            granted: [FunctionPointCatalog.MarketView],
            required: [FunctionPointCatalog.MarketView, FunctionPointCatalog.AdminUsers]);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task 未认证时不放行()
    {
        var context = await EvaluateAsync(
            authenticated: false,
            granted: [FunctionPointCatalog.AdminUsers],
            required: [FunctionPointCatalog.AdminUsers]);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task 完全没有任何功能点时拒绝()
    {
        var context = await EvaluateAsync(
            authenticated: true,
            granted: [],
            required: [FunctionPointCatalog.MarketView]);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task 兼容以逗号分隔的单条声明形式()
    {
        // 某些反代或旧令牌会把多值声明压成逗号分隔的单条声明
        var context = await EvaluateAsync(
            authenticated: true,
            granted: [FunctionPointCatalog.MarketView, FunctionPointCatalog.StockTrend],
            required: [FunctionPointCatalog.StockTrend],
            collapseToSingleClaim: true);

        Assert.True(context.HasSucceeded);
    }

    /// <summary>
    /// 构造上下文并执行判定。
    /// </summary>
    private async Task<AuthorizationHandlerContext> EvaluateAsync(
        bool authenticated,
        string[] granted,
        string[] required,
        bool collapseToSingleClaim = false)
    {
        var claims = new List<Claim>();
        if (authenticated)
        {
            claims.Add(new Claim(SaClaims.Subject, "u1"));
        }

        if (collapseToSingleClaim)
        {
            if (granted.Length > 0)
            {
                claims.Add(new Claim(SaClaims.FunctionPoint, string.Join(',', granted)));
            }
        }
        else
        {
            claims.AddRange(granted.Select(fp => new Claim(SaClaims.FunctionPoint, fp)));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null));
        var context = new AuthorizationHandlerContext(
            [new FunctionPointRequirement(required)],
            principal,
            resource: null);

        await _handler.HandleAsync(context).ConfigureAwait(false);
        return context;
    }
}
