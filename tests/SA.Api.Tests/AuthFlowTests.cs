using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SA.Api.Tests;

/// <summary>
/// 登录、令牌轮换、登出、改密与锁定。这些是「权限裁剪链路」的地基：
/// 前端所有路由判断都依赖登录后拿到的权限快照。
/// </summary>
public class AuthFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string RefreshCookieName = "sa.rt";

    [Fact]
    public async Task 管理员登录成功并拿到权限快照()
    {
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var (response, document) = await LoginAsync(client, "admin", ApiFactory.AdminPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, document.RootElement.GetProperty("code").GetInt32());

        var data = document.RootElement.GetProperty("data");
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("accessToken").GetString()));
        Assert.Equal("Bearer", data.GetProperty("tokenType").GetString());

        var user = data.GetProperty("user");
        Assert.Equal("admin", user.GetProperty("username").GetString());
        Assert.Equal("admin", user.GetProperty("roleId").GetString());
        Assert.Equal("all", user.GetProperty("dataScope").GetString());

        // 管理员应拿到全部功能点（首启播种 + 目录同步）
        var fps = user.GetProperty("functionPoints").EnumerateArray().Select(x => x.GetString()!).ToList();
        Assert.Contains("admin.users", fps);
        Assert.Contains("market.view", fps);
        Assert.Contains("export.data", fps);

        // 操作级参数必须随之下发，否则前端无法展示剩余额度
        var quotas = user.GetProperty("quotas");
        Assert.True(quotas.TryGetProperty("quota.daily", out var daily));
        Assert.True(daily.GetInt32() > 0);

        // 刷新令牌只走 HttpOnly Cookie
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(RefreshCookieName, StringComparison.Ordinal));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task 密码错误返回两千零一错误码()
    {
        using var client = factory.CreateClient();

        var (response, document) = await LoginAsync(client, "admin", "Wrong!Password123");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(2001, document.RootElement.GetProperty("code").GetInt32());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task 不存在的用户名与密码错误返回同一提示避免枚举账号()
    {
        using var client = factory.CreateClient();

        var (_, missing) = await LoginAsync(client, "nobody", "Wrong!Password123");
        var (_, wrong) = await LoginAsync(client, "admin", "Wrong!Password123");

        Assert.Equal(
            wrong.RootElement.GetProperty("message").GetString(),
            missing.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task 登录后可访问当前用户接口()
    {
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var token = await LoginAndGetTokenAsync(client, "admin", ApiFactory.AdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("admin", document.RootElement.GetProperty("data").GetProperty("username").GetString());
    }

    [Fact]
    public async Task 刷新令牌轮换后旧令牌失效()
    {
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var (loginResponse, _) = await LoginAsync(client, "admin", ApiFactory.AdminPassword);
        var firstCookie = ExtractRefreshCookie(loginResponse);

        // 第一次刷新：应成功并给出新 Cookie
        using var firstRefresh = await RefreshAsync(client, firstCookie);
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);
        var secondCookie = ExtractRefreshCookie(firstRefresh);
        Assert.NotEqual(firstCookie, secondCookie);

        // 旧 Cookie 已被吊销：再刷一次必须失败
        using var replay = await RefreshAsync(client, firstCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        using var replayDocument = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Assert.Equal(2001, replayDocument.RootElement.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task 登出后刷新令牌不可再用()
    {
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var (loginResponse, _) = await LoginAsync(client, "admin", ApiFactory.AdminPassword);
        var cookie = ExtractRefreshCookie(loginResponse);

        using var logout = await PostWithCookieAsync(client, "/api/auth/logout", cookie);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        using var refresh = await RefreshAsync(client, cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task 改密后旧密码不可用且历史密码不可复用()
    {
        using var isolated = new ApiFactory();
        using var client = isolated.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var token = await LoginAndGetTokenAsync(client, "admin", ApiFactory.AdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 当前密码错误
        using var wrongCurrent = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new { currentPassword = "Wrong!Password123", newPassword = ApiFactory.NewAdminPassword });
        Assert.Equal(HttpStatusCode.BadRequest, wrongCurrent.StatusCode);
        using (var document = JsonDocument.Parse(await wrongCurrent.Content.ReadAsStringAsync()))
        {
            Assert.Equal(1001, document.RootElement.GetProperty("code").GetInt32());
        }

        // 新密码不符合策略
        using var weak = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new { currentPassword = ApiFactory.AdminPassword, newPassword = "alllowercase" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        // 正常改密
        using var changed = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new { currentPassword = ApiFactory.AdminPassword, newPassword = ApiFactory.NewAdminPassword });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        // 历史密码不可复用
        using var reused = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new { currentPassword = ApiFactory.NewAdminPassword, newPassword = ApiFactory.AdminPassword });
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        using (var document = JsonDocument.Parse(await reused.Content.ReadAsStringAsync()))
        {
            Assert.Contains("不能与最近", document.RootElement.GetProperty("message").GetString()!, StringComparison.Ordinal);
        }

        // 旧密码失效、新密码可用
        client.DefaultRequestHeaders.Authorization = null;
        using var fresh = isolated.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        var (oldLogin, _) = await LoginAsync(fresh, "admin", ApiFactory.AdminPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var (newLogin, _) = await LoginAsync(fresh, "admin", ApiFactory.NewAdminPassword);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task 连续失败达阈值后账号被锁定()
    {
        using var strict = new StrictLockApiFactory();
        using var client = strict.CreateClient();

        // 阈值 2：第一次是普通失败
        var (first, firstDocument) = await LoginAsync(client, "admin", "Wrong!Password123");
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(2001, firstDocument.RootElement.GetProperty("code").GetInt32());

        // 第二次失败即触达阈值，该次响应直接告知已锁定（2004）
        var (second, secondDocument) = await LoginAsync(client, "admin", "Wrong!Password123");
        Assert.Equal(HttpStatusCode.Locked, second.StatusCode);
        Assert.Equal(2004, secondDocument.RootElement.GetProperty("code").GetInt32());

        // 锁定期内即使密码正确也被拒
        var (locked, lockedDocument) = await LoginAsync(client, "admin", ApiFactory.AdminPassword);
        Assert.Equal(HttpStatusCode.Locked, locked.StatusCode);
        Assert.Equal(2004, lockedDocument.RootElement.GetProperty("code").GetInt32());
        Assert.Contains("锁定", lockedDocument.RootElement.GetProperty("message").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 权限快照接口返回角色与功能点()
    {
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var token = await LoginAndGetTokenAsync(client, "admin", ApiFactory.AdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/api/me/permissions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");
        Assert.Equal("admin", data.GetProperty("roleId").GetString());
        Assert.Equal("管理员", data.GetProperty("roleName").GetString());
        Assert.Equal("all", data.GetProperty("dataScope").GetString());
        Assert.True(data.GetProperty("functionPoints").GetArrayLength() > 20);
    }

    [Fact]
    public async Task 无令牌访问受保护接口返回两千零一()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/me/settings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2001, document.RootElement.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task 伪造令牌被拒绝()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");

        using var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 个人设置可保存并读回()
    {
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var token = await LoginAndGetTokenAsync(client, "admin", ApiFactory.AdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 未设置时返回空对象
        using (var empty = await client.GetAsync("/api/me/settings"))
        {
            using var document = JsonDocument.Parse(await empty.Content.ReadAsStringAsync());
            Assert.Equal(JsonValueKind.Object, document.RootElement.GetProperty("data").ValueKind);
            Assert.Empty(document.RootElement.GetProperty("data").EnumerateObject());
        }

        using var saved = await client.PutAsJsonAsync("/api/me/settings", new { theme = "light", updown = "green-up" });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using var fetched = await client.GetAsync("/api/me/settings");
        using var fetchedDocument = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        var data = fetchedDocument.RootElement.GetProperty("data");
        Assert.Equal("light", data.GetProperty("theme").GetString());
        Assert.Equal("green-up", data.GetProperty("updown").GetString());
    }

    [Fact]
    public async Task 设置内容必须是对象()
    {
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var token = await LoginAndGetTokenAsync(client, "admin", ApiFactory.AdminPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.PutAsJsonAsync("/api/me/settings", new[] { 1, 2, 3 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1001, document.RootElement.GetProperty("code").GetInt32());
    }

    private static async Task<(HttpResponseMessage Response, JsonDocument Document)> LoginAsync(
        HttpClient client,
        string username,
        string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password, totpCode = (string?)null, rememberMe = true });

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (response, document);
    }

    private static async Task<string> LoginAndGetTokenAsync(HttpClient client, string username, string password)
    {
        var (response, document) = await LoginAsync(client, username, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return document.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    private static string ExtractRefreshCookie(HttpResponseMessage response)
    {
        var setCookie = response.Headers
            .GetValues("Set-Cookie")
            .Single(c => c.StartsWith(RefreshCookieName + "=", StringComparison.Ordinal));

        var value = setCookie.Split(';')[0];
        return value;
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string cookie) =>
        PostWithCookieAsync(client, "/api/auth/refresh", cookie);

    private static Task<HttpResponseMessage> PostWithCookieAsync(HttpClient client, string url, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Cookie", cookie);
        return client.SendAsync(request);
    }
}
