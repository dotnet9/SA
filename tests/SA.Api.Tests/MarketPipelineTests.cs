using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SA.Api.Tests.Fakes;
using SA.Application.Abstractions;
using SA.Application.Market;
using SA.Infrastructure.Collect.Jobs;

namespace SA.Api.Tests;

/// <summary>
/// 采集 → 落库 → 聚合 → 接口的端到端链路。
/// </summary>
/// <remarks>
/// 采集源被替换为返回固定样本的替身，因此本测试<b>不依赖上游可用性</b>：
/// 实测东财 push2 主机在被连续请求后会临时拒服，若把回归建立在真实上游上，结果将不可重复。
/// 真实字段映射由 <see cref="UpstreamParsingTests"/> 用真实响应样本固化，两者互补。
/// </remarks>
public class MarketPipelineTests : IClassFixture<MarketApiFactory>
{
    private readonly MarketApiFactory _factory;

    public MarketPipelineTests(MarketApiFactory factory) => _factory = factory;

    [Fact]
    public async Task 采集后市场概览返回真实聚合结果()
    {
        await _factory.RunCollectionAsync();

        using var client = await _factory.CreateAdminClientAsync();

        var overview = await GetAsync<JsonElement>(client, "/api/market/overview");

        // 口径日来自指数源的上游时间戳（2026-09-18），而不是本机日期
        Assert.Equal("2026-09-18", overview.GetProperty("status").GetProperty("asOf").GetString());
        Assert.True(overview.GetProperty("status").GetProperty("isReady").GetBoolean());

        // 指数卡：只展示 5 张（沪深 300 采集但不展示）
        var indices = overview.GetProperty("indices");
        Assert.Equal(5, indices.GetArrayLength());
        Assert.Equal("000001", indices[0].GetProperty("code").GetString());
        Assert.Equal(3911.87m, indices[0].GetProperty("price").GetDecimal());
        Assert.Equal(9941.69m, indices[0].GetProperty("amount").GetDecimal());

        // 涨跌家数：6 只里停牌 1 只不进快照 → 5 只参与统计（涨 3 / 跌 1 / 平 1）
        var breadth = overview.GetProperty("breadth");
        Assert.Equal(3, breadth.GetProperty("up").GetInt32());
        Assert.Equal(1, breadth.GetProperty("down").GetInt32());
        Assert.Equal(1, breadth.GetProperty("flat").GetInt32());
        Assert.Equal(5, breadth.GetProperty("total").GetInt32());

        // 成交额 = (98.6 + 115.3766464791 + 14.1983168434 + 1.2 + 38.2) 亿
        Assert.Equal(267.57m, breadth.GetProperty("turnover").GetDecimal());

        // 涨跌停来自专用口径，两融来自沪深合计
        Assert.Equal(47, breadth.GetProperty("limitUp").GetInt32());
        Assert.Equal(1, breadth.GetProperty("limitDown").GetInt32());
        Assert.Equal(26015.26m, breadth.GetProperty("marginBalance").GetDecimal());
        Assert.Equal("2026-09-17", breadth.GetProperty("marginAsOf").GetString());

        // 行业热力：3 个板块按涨跌幅倒序，主力净流入换算为亿元
        var industries = overview.GetProperty("industries").EnumerateArray().ToList();
        Assert.Equal(3, industries.Count);
        Assert.Equal("BK1033", industries[0].GetProperty("code").GetString());
        Assert.Equal(3.86m, industries[0].GetProperty("pct").GetDecimal());
        Assert.Equal(28.6m, industries[0].GetProperty("flow").GetDecimal());

        var electronics = industries.Single(i => i.GetProperty("code").GetString() == "BK1201");
        Assert.Equal(198.54m, electronics.GetProperty("flow").GetDecimal());
        Assert.Equal(45.6m, electronics.GetProperty("pe").GetDecimal());

        // 市盈率缺失的板块返回 null 而不是 0
        var realEstate = industries.Single(i => i.GetProperty("code").GetString() == "BK0475");
        Assert.Equal(JsonValueKind.Null, realEstate.GetProperty("pe").ValueKind);

        // 榜单：涨幅榜剔除 ST，成交额榜保留
        var rankings = overview.GetProperty("rankings");
        var gainers = rankings.GetProperty("gainers");
        Assert.DoesNotContain(
            gainers.EnumerateArray().Select(g => g.GetProperty("code").GetString()),
            code => code == "000005");

        var amount = rankings.GetProperty("amount");
        Assert.Equal("300750", amount[0].GetProperty("code").GetString());
        Assert.Equal(115.38m, amount[0].GetProperty("amount").GetDecimal());
        Assert.Contains(
            amount.EnumerateArray().Select(a => a.GetProperty("code").GetString()),
            code => code == "000005");
    }

    [Fact]
    public async Task 市场资金流与北向的降级说明一并返回()
    {
        await _factory.RunCollectionAsync();

        using var client = await _factory.CreateAdminClientAsync();
        var breadth = await GetAsync<JsonElement>(client, "/api/market/breadth");

        // 北向不可得时必须显式说明原因，而不是留空
        Assert.Equal(JsonValueKind.Null, breadth.GetProperty("northbound").ValueKind);
        Assert.Contains("不再公开披露", breadth.GetProperty("northboundNote").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 搜索支持代码名称拼音与行业四种入口()
    {
        await _factory.RunCollectionAsync();

        using var client = await _factory.CreateAdminClientAsync();

        var byCode = await SearchAsync(client, "300750");
        Assert.Equal("宁德时代", byCode[0].GetProperty("name").GetString());
        Assert.Equal("code", byCode[0].GetProperty("matchedBy").GetString());
        Assert.Equal(301.95m, byCode[0].GetProperty("price").GetDecimal());
        Assert.Equal(13971.93m, byCode[0].GetProperty("cap").GetDecimal());

        var byName = await SearchAsync(client, "宁德");
        Assert.Equal("300750", byName[0].GetProperty("code").GetString());
        Assert.Equal("name", byName[0].GetProperty("matchedBy").GetString());

        var byPinyin = await SearchAsync(client, "ndsd");
        Assert.Equal("300750", byPinyin[0].GetProperty("code").GetString());
        Assert.Equal("pinyin", byPinyin[0].GetProperty("matchedBy").GetString());

        var byIndustry = await SearchAsync(client, "电池");
        Assert.Equal("300750", byIndustry[0].GetProperty("code").GetString());
        Assert.Equal("industry", byIndustry[0].GetProperty("matchedBy").GetString());

        // 停牌标的仍可被搜到，但不带行情（价格为 null 而不是 0）
        var suspended = await SearchAsync(client, "000003");
        Assert.Equal(JsonValueKind.Null, suspended[0].GetProperty("price").ValueKind);
    }

    [Fact]
    public async Task 无功能点的角色访问市场接口返回2002并点名缺失功能点()
    {
        using var client = await _factory.CreateClientForAsync(
            MarketApiFactory.NoPermissionUser, MarketApiFactory.NoPermissionPassword);

        using var response = await client.GetAsync("/api/market/overview");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2002, document.RootElement.GetProperty("code").GetInt32());

        // 报错信息要能直接指导排查（列出缺失的功能点）
        Assert.Contains("market.view", document.RootElement.GetProperty("message").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 访客角色可以看市场概览但不能看自选与后台()
    {
        await _factory.RunCollectionAsync();

        using var client = await _factory.CreateClientForAsync(
            MarketApiFactory.GuestUser, MarketApiFactory.GuestPassword);

        // 访客的功能点里含 market.view（需求规格 §7.2），因此市场概览必须放行
        using var allowed = await client.GetAsync("/api/market/indices");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    /* ------------------------------------------------------------------
       工具
       ------------------------------------------------------------------ */

    private static async Task<JsonElement> GetAsync<T>(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, document.RootElement.GetProperty("code").GetInt32());
        return document.RootElement.GetProperty("data").Clone();
    }

    private static async Task<IReadOnlyList<JsonElement>> SearchAsync(HttpClient client, string query)
    {
        var data = await GetAsync<JsonElement>(client, $"/api/search?q={Uri.EscapeDataString(query)}");
        return data.GetProperty("rows").EnumerateArray().ToList();
    }
}

/// <summary>
/// 用替身采集源构建的测试宿主：关闭调度（避免真实网络请求），并把全部采集源换成固定样本。
/// </summary>
public sealed class MarketApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), "sa-tests-market", Guid.NewGuid().ToString("N"));

    /// <summary>测试管理员密码。</summary>
    public const string AdminPassword = "SaTest!2026Pass";

    /// <summary>访客（内置角色，含 market.view 与 data.scope.watchlist）。</summary>
    public const string GuestUser = "guest";

    /// <summary>访客密码。</summary>
    public const string GuestPassword = "SaGuest!2026Pass";

    /// <summary>无任何功能点的账号，用于验证「无权限 → 2002」契约。</summary>
    public const string NoPermissionUser = "nobody";

    /// <summary>无功能点账号的密码。</summary>
    public const string NoPermissionPassword = "SaNobody!2026Pass";

    private bool _collected;

    /// <summary>
    /// 执行一次采集（启动时的四个任务 + 市场统计）。幂等，多个测试共用同一份结果。
    /// </summary>
    public async Task RunCollectionAsync()
    {
        if (_collected)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var provider = scope.ServiceProvider;

        await provider.GetRequiredService<TradingCalendarJob>().RunAsync();
        await provider.GetRequiredService<IndexJob>().RunAsync();
        await provider.GetRequiredService<UniverseJob>().RunAsync();
        await provider.GetRequiredService<SectorJob>().RunAsync();
        await provider.GetRequiredService<QuoteSnapshotJob>().RunAsync();
        await provider.GetRequiredService<MarketStatJob>().RunAsync();

        _collected = true;
    }

    /// <summary>以管理员身份登录并返回已带令牌的客户端。</summary>
    public Task<HttpClient> CreateAdminClientAsync() => CreateClientForAsync("admin", AdminPassword);

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Sa:DataDirectory", _dataDirectory);
        builder.UseSetting("Sa:Auth:AdminInitialPassword", AdminPassword);
        builder.UseSetting("Sa:Auth:RequireTotp", "false");

        // 调度关闭：测试自己驱动任务，绝不打真实上游
        builder.UseSetting("Sa:Collector:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IMarketListSource>(new FakeMarketListSource(FakeMarketListSource.DefaultRows));
            services.AddSingleton<IIndexSource, FakeIndexSource>();
            services.AddSingleton<ISectorSource, FakeSectorSource>();
            services.AddSingleton<ILimitPoolSource, FakeLimitPoolSource>();
            services.AddSingleton<IMarginMarketSource, FakeMarginMarketSource>();
            services.AddSingleton<IMarketFundFlowSource, FakeMarketFundFlowSource>();
            services.AddSingleton<ITradingCalendarSource, FakeTradingCalendarSource>();
        });
    }

    /// <summary>
    /// 按需播种测试账号并登录。
    /// </summary>
    /// <remarks>
    /// 用户管理端点属于第 12 批，因此这里直接经仓储写库，以便本批就能验证
    /// 「无权限 → 2002」与「访客可看市场」这两条前端依赖的契约。
    /// 功能点写在令牌里，所以角色必须在登录<b>之前</b>落库。
    /// </remarks>
    public async Task<HttpClient> CreateClientForAsync(string username, string password)
    {
        await SeedUserAsync(username, password);

        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password, totpCode = (string?)null, rememberMe = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("data").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// 建一个测试账号：访客用内置角色，无权限账号用「零功能点」的自定义角色。
    /// </summary>
    private async Task SeedUserAsync(string username, string password)
    {
        using var scope = Services.CreateScope();
        var provider = scope.ServiceProvider;
        var users = provider.GetRequiredService<IUserStore>();
        if (await users.FindByUsernameAsync(username) is not null)
        {
            return;
        }

        var roleId = username == GuestUser ? SA.Domain.Authorization.BuiltInRoleIds.Guest : "no-permission";
        if (roleId == "no-permission")
        {
            var roles = provider.GetRequiredService<IRoleStore>();
            if (await roles.FindAsync(roleId) is null)
            {
                await roles.AddAsync(new SA.Domain.Entities.Identity.Role
                {
                    Id = roleId,
                    Name = "无权限角色（测试用）",
                    Description = "功能点为空，用于验证接口层的 2002 契约",
                    IsBuiltin = false,
                    CreatedAt = SA.Domain.Common.SaTime.Now
                });
                await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
            }
        }

        var hasher = provider.GetRequiredService<SA.Application.Auth.IPasswordHasher>();
        var hash = hasher.Hash(password);
        var now = SA.Domain.Common.SaTime.Now;

        await users.AddAsync(new SA.Domain.Entities.Identity.User
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = username,
            Nickname = username,
            PasswordHash = hash.Hash,
            PasswordSalt = hash.Salt,
            PasswordIterations = hash.Iterations,
            PasswordChangedAt = now,
            MustChangePwd = false,
            Status = SA.Domain.Entities.Identity.UserStatus.Active,
            RoleId = roleId,
            CreatedAt = now
        });

        await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        try
        {
            if (Directory.Exists(_dataDirectory))
            {
                Directory.Delete(_dataDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // SQLite 连接释放有延迟，清理失败不影响测试结论
        }
    }
}
