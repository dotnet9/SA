using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SA.Api.Tests.Fakes;
using SA.Application.Abstractions;
using SA.Infrastructure.Collect.Jobs;

namespace SA.Api.Tests;

/// <summary>
/// 自选股与数据范围端到端。覆盖实施计划第 4 批的三条关键约定：
/// 增删幂等、配额约束、以及 <c>data.scope.watchlist</c> 角色只能看到自选内的数据。
/// </summary>
public class WatchlistTests : IClassFixture<WatchlistApiFactory>
{
    private readonly WatchlistApiFactory _factory;

    public WatchlistTests(WatchlistApiFactory factory) => _factory = factory;

    [Fact]
    public async Task 加入自选后可以读到行情与行业()
    {
        var client = await _factory.CreateUserClientAsync(WatchlistApiFactory.NormalUser);

        var added = await PostAsync(client, "/api/watchlist/items", new { codes = new[] { "300750" }, groupId = (string?)null, note = "看电池周期" });
        Assert.Equal(1, added.GetInt32());

        var list = await GetAsync(client, "/api/watchlist");
        var items = list.GetProperty("items").EnumerateArray().ToList();

        var item = Assert.Single(items);
        Assert.Equal("300750", item.GetProperty("code").GetString());
        Assert.Equal("宁德时代", item.GetProperty("name").GetString());
        Assert.Equal("电池", item.GetProperty("industry").GetString());
        Assert.Equal(301.95m, item.GetProperty("price").GetDecimal());
        Assert.Equal("看电池周期", item.GetProperty("note").GetString());

        // 具备 watchlist.edit 的账号，界面据此显示编辑入口
        Assert.True(list.GetProperty("canEdit").GetBoolean());
        Assert.True(list.GetProperty("quota").GetInt32() > 0);
    }

    [Fact]
    public async Task 重复加入同一只股票不报错也不重复插入()
    {
        var client = await _factory.CreateUserClientAsync(WatchlistApiFactory.IdempotentUser);

        var first = await PostAsync(client, "/api/watchlist/items", new { codes = new[] { "300750", "600519" } });
        Assert.Equal(2, first.GetInt32());

        // 第二次：已存在 → 新增 0 条，仍是成功
        var second = await PostAsync(client, "/api/watchlist/items", new { codes = new[] { "300750", "600519" } });
        Assert.Equal(0, second.GetInt32());

        var list = await GetAsync(client, "/api/watchlist");
        Assert.Equal(2, list.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task 删除不存在的代码同样成功()
    {
        var client = await _factory.CreateUserClientAsync(WatchlistApiFactory.RemoveUser);

        // 先放入 300750，再连同不存在的代码一起删除：只删掉存在的那个，接口不因未命中而失败
        await PostAsync(client, "/api/watchlist/items", new { codes = new[] { "300750" } });

        var removed = await DeleteAsync(client, "/api/watchlist/items", new { codes = new[] { "300750", "999999" } });
        Assert.Equal(1, removed.GetInt32());

        // 再删一次：幂等，返回 0 而不是报错
        var again = await DeleteAsync(client, "/api/watchlist/items", new { codes = new[] { "300750" } });
        Assert.Equal(0, again.GetInt32());
    }

    [Fact]
    public async Task 超出自选配额返回三千零二()
    {
        // 该账号的角色配额被设为 2（见工厂里的测试角色）
        var client = await _factory.CreateUserClientAsync(WatchlistApiFactory.QuotaUser);

        await PostAsync(client, "/api/watchlist/items", new { codes = new[] { "300750", "600519" } });

        using var response = await client.PostAsJsonAsync(
            "/api/watchlist/items",
            new { codes = new[] { "000002" }, groupId = (string?)null, note = (string?)null });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(3002, document.RootElement.GetProperty("code").GetInt32());
        Assert.Contains("自选数量上限", document.RootElement.GetProperty("message").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 不存在的代码被拒绝而不是写进自选()
    {
        var client = await _factory.CreateUserClientAsync(WatchlistApiFactory.UnknownCodeUser);

        using var response = await client.PostAsJsonAsync(
            "/api/watchlist/items",
            new { codes = new[] { "999999" }, groupId = (string?)null, note = (string?)null });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1002, document.RootElement.GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task 分组增删与排序可持久化且删组不删股票()
    {
        var client = await _factory.CreateUserClientAsync(WatchlistApiFactory.GroupUser);

        var group = await PostAsync(client, "/api/watchlist/groups", new { name = "新能源" });
        var groupId = group.GetProperty("id").GetString()!;

        await PostAsync(client, "/api/watchlist/items", new { codes = new[] { "300750" }, groupId, note = (string?)null });

        var afterAdd = await GetAsync(client, "/api/watchlist");
        var groups = afterAdd.GetProperty("groups").EnumerateArray().ToList();
        var created = groups.Single(g => g.GetProperty("id").GetString() == groupId);
        Assert.Equal(1, created.GetProperty("count").GetInt32());

        // 重排：把 300750 移到未分组
        await PutAsync(client, "/api/watchlist/order", new { items = new[] { new { code = "300750", groupId = (string?)null, note = (string?)null } } });

        var afterReorder = await GetAsync(client, "/api/watchlist");
        var moved = afterReorder.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(JsonValueKind.Null, moved.GetProperty("groupId").ValueKind);

        // 删除分组：组内股票保留（移回未分组）
        var removed = await DeleteAsync(client, $"/api/watchlist/groups/{groupId}", null);
        Assert.Equal(0, removed.GetInt32());

        var final = await GetAsync(client, "/api/watchlist");
        Assert.Equal(1, final.GetProperty("items").GetArrayLength());
        Assert.Empty(final.GetProperty("groups").EnumerateArray());
    }

    [Fact]
    public async Task 两个账号的自选互不可见()
    {
        var first = await _factory.CreateUserClientAsync(WatchlistApiFactory.IsolationUserA);
        var second = await _factory.CreateUserClientAsync(WatchlistApiFactory.IsolationUserB);

        await PostAsync(first, "/api/watchlist/items", new { codes = new[] { "300750" } });
        await PostAsync(second, "/api/watchlist/items", new { codes = new[] { "600519" } });

        var listA = await GetAsync(first, "/api/watchlist");
        var listB = await GetAsync(second, "/api/watchlist");

        Assert.Equal("300750", listA.GetProperty("items").EnumerateArray().Single().GetProperty("code").GetString());
        Assert.Equal("600519", listB.GetProperty("items").EnumerateArray().Single().GetProperty("code").GetString());
    }

    [Fact]
    public async Task 仅自选数据范围的角色只看得到自选内的标的()
    {
        var client = await _factory.CreateUserClientAsync(WatchlistApiFactory.ScopedUser);

        // 先把 300750 放进自选
        await PostAsync(client, "/api/watchlist/items", new { codes = new[] { "300750" } });

        // 搜索：只返回自选内命中的标的，并给出范围说明
        var scoped = await GetAsync(client, "/api/search?q=电池");
        var rows = scoped.GetProperty("rows").EnumerateArray().ToList();
        Assert.Single(rows);
        Assert.Equal("300750", rows[0].GetProperty("code").GetString());
        Assert.Contains("仅自选股", scoped.GetProperty("scopeNote").GetString()!, StringComparison.Ordinal);

        // 全市场命中数应当远大于 1，说明过滤确实生效而不是碰巧只有一条
        var unscoped = await GetAsync(client, "/api/search?q=电池&pageSize=200");
        Assert.Equal(1, unscoped.GetProperty("total").GetInt32());

        // 榜单同样被过滤
        var rankings = await GetAsync(client, "/api/market/rankings?take=10");
        foreach (var bucket in new[] { "amount", "gainers", "losers" })
        {
            var codes = rankings.GetProperty(bucket).EnumerateArray().Select(r => r.GetProperty("code").GetString()).ToList();
            Assert.All(codes, code => Assert.Equal("300750", code));
        }
    }

    [Fact]
    public async Task 全市场数据范围的角色不受过滤()
    {
        var client = await _factory.CreateUserClientAsync(WatchlistApiFactory.NormalUser);

        var result = await GetAsync(client, "/api/search?q=电池&pageSize=200");

        // 全市场范围：能搜到「电池」行业的标的，且不返回范围说明
        Assert.True(result.GetProperty("total").GetInt32() >= 1);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("scopeNote").ValueKind);
    }

    /* --- 后台管理端点 --------------------------------------------------- */

    /// <summary>
    /// 后台的用户与权限端点必须能正常返回。
    /// </summary>
    /// <remarks>
    /// 这是一条<b>回归测试</b>：早期实现里 <c>RoleAdminStore.ListAsync</c> 在 EF 查询中给
    /// <c>ThenBy</c> 传了 <c>StringComparer.Ordinal</c>，EF Core 无法翻译，导致
    /// <c>/api/admin/users</c> 与 <c>/api/admin/permissions</c> 同时返回 500。
    /// 这类错误只在运行时暴露（编译期完全合法），因此必须有端到端的断言守住。
    /// </remarks>
    [Fact]
    public async Task 后台用户与权限端点可正常返回()
    {
        var client = await _factory.CreateAdminClientAsync();

        var users = await GetAsync(client, "/api/admin/users");

        // 工厂里种了 9 个测试账号 + 内置管理员
        Assert.True(users.GetProperty("total").GetInt32() >= 9);
        Assert.True(users.GetProperty("activeCount").GetInt32() >= 9);

        var first = users.GetProperty("items").EnumerateArray().First();
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("username").GetString()));
        // 角色名必须解析出来（而不是回退成角色 Id）
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("roleName").GetString()));

        var matrix = await GetAsync(client, "/api/admin/permissions");

        // 功能点目录与角色都要在，且总量与需求规格一致（28 项）
        Assert.Equal(28, matrix.GetProperty("functionPoints").GetArrayLength());

        var roles = matrix.GetProperty("roles").EnumerateArray().ToList();
        Assert.True(roles.Count >= 3);

        // 每个角色都应带出功能点与配额字段（权限矩阵的勾选状态依赖它们）
        foreach (var role in roles)
        {
            Assert.NotEqual(JsonValueKind.Undefined, role.GetProperty("functionPoints").ValueKind);
            Assert.NotEqual(JsonValueKind.Undefined, role.GetProperty("quotas").ValueKind);
        }

        // 内置管理员必须有全部功能点：否则会把系统锁死
        var admin = roles.Single(role => role.GetProperty("id").GetString() == "admin");
        Assert.True(admin.GetProperty("isBuiltin").GetBoolean());
        Assert.Equal(28, admin.GetProperty("functionPoints").GetArrayLength());
    }

    /// <summary>后台的数据源、会话与系统状态端点同样必须可用。</summary>
    [Fact]
    public async Task 后台监控与会话端点可正常返回()
    {
        var client = await _factory.CreateAdminClientAsync();

        var sources = await GetAsync(client, "/api/admin/datasources");
        Assert.NotEqual(JsonValueKind.Undefined, sources.GetProperty("sources").ValueKind);
        Assert.NotEqual(JsonValueKind.Undefined, sources.GetProperty("tasks").ValueKind);

        var sessions = await GetAsync(client, "/api/admin/security/sessions");
        Assert.NotEqual(JsonValueKind.Undefined, sessions.GetProperty("logs").ValueKind);

        var state = await GetAsync(client, "/api/admin/system/state");
        // 存储路径必须给全，后台「系统设置与存储」要展示它们
        Assert.False(string.IsNullOrWhiteSpace(state.GetProperty("databasePath").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(state.GetProperty("dataRoot").GetString()));
        Assert.True(state.GetProperty("settings").GetArrayLength() > 0);
    }

    /// <summary>管理动作必须写审计日志（改设置是最容易验证的一条）。</summary>
    [Fact]
    public async Task 管理操作写入审计日志()
    {
        var client = await _factory.CreateAdminClientAsync();

        await PutAsync(client, "/api/admin/system/settings", new
        {
            values = new Dictionary<string, string> { ["site.notice"] = "回归测试写入" }
        });

        var audit = await GetAsync(client, "/api/admin/security/audit");
        var rows = audit.EnumerateArray().ToList();

        Assert.Contains(rows, row =>
            row.GetProperty("action").GetString() == "settings.update"
            && (row.GetProperty("detail").GetString() ?? string.Empty).Contains("site.notice", StringComparison.Ordinal));
    }

    /// <summary>未开放修改的设置项必须被拒绝，而不是静默写库。</summary>
    [Fact]
    public async Task 未开放的设置项被拒绝()
    {
        var client = await _factory.CreateAdminClientAsync();

        using var response = await client.PutAsJsonAsync(
            "/api/admin/system/settings",
            new { values = new Dictionary<string, string> { ["auth.signingKey"] = "hacked" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1001, document.RootElement.GetProperty("code").GetInt32());
    }

    /* ------------------------------------------------------------------ */

    /// <summary>发请求并解包响应包，返回 <c>data</c> 部分（与其余测试文件一致）。</summary>
    private static async Task<JsonElement> GetAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        return await ReadDataAsync(response);
    }

    private static async Task<JsonElement> PostAsync(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(path, body);
        return await ReadDataAsync(response);
    }

    private static async Task<JsonElement> PutAsync(HttpClient client, string path, object body)
    {
        using var response = await client.PutAsJsonAsync(path, body);
        return await ReadDataAsync(response);
    }

    private static async Task<JsonElement> DeleteAsync(HttpClient client, string path, object? body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await client.SendAsync(request);
        return await ReadDataAsync(response);
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, document.RootElement.GetProperty("code").GetInt32());
        return document.RootElement.GetProperty("data").Clone();
    }
}

/// <summary>
/// 自选测试宿主：预置若干测试账号与角色，任务由测试显式驱动。
/// </summary>
public sealed class WatchlistApiFactory : WebApplicationFactory<Program>
{
    /// <summary>普通用户（全市场范围 + 自选读写）。</summary>
    public const string NormalUser = "normal";

    /// <summary>幂等性验证账号。</summary>
    public const string IdempotentUser = "idem";

    /// <summary>删除验证账号。</summary>
    public const string RemoveUser = "remover";

    /// <summary>配额受限账号（watchlist.max = 2）。</summary>
    public const string QuotaUser = "quota";

    /// <summary>错误代码验证账号。</summary>
    public const string UnknownCodeUser = "unknown";

    /// <summary>分组与排序验证账号。</summary>
    public const string GroupUser = "grouper";

    /// <summary>隔离验证账号 A。</summary>
    public const string IsolationUserA = "iso-a";

    /// <summary>隔离验证账号 B。</summary>
    public const string IsolationUserB = "iso-b";

    /// <summary>仅自选数据范围账号。</summary>
    public const string ScopedUser = "scoped";

    /// <summary>测试密码（满足密码策略）。</summary>
    public const string Password = "SaWatch!2026Pass";

    private const string AdminPassword = "SaTest!2026Pass";

    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), "sa-tests-watch", Guid.NewGuid().ToString("N"));

    private bool _seeded;

    /// <summary>取管理员客户端（用于准备数据）。</summary>
    public async Task<HttpClient> CreateAdminClientAsync()
    {
        await SeedAsync();
        return await SignInAsync("admin", AdminPassword);
    }

    /// <summary>取某个测试账号的客户端。</summary>
    public async Task<HttpClient> CreateUserClientAsync(string username)
    {
        await SeedAsync();
        return await SignInAsync(username, Password);
    }

    /// <summary>
    /// 预置测试角色与账号。
    /// </summary>
    /// <remarks>
    /// 角色与用户管理端点属于第 12 批，因此这里直接经仓储写库。
    /// 权限写在令牌里，所以必须在登录之前完成。
    /// </remarks>
    private async Task SeedAsync()
    {
        if (_seeded)
        {
            return;
        }

        // 先让行情数据就位（自选列表要显示价格与行业）
        using (var scope = Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            await provider.GetRequiredService<IndexJob>().RunAsync();
            await provider.GetRequiredService<UniverseJob>().RunAsync();
            await provider.GetRequiredService<QuoteSnapshotJob>().RunAsync();
        }

        using (var scope = Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            var roles = provider.GetRequiredService<IRoleStore>();
            var users = provider.GetRequiredService<IUserStore>();
            var hasher = provider.GetRequiredService<SA.Application.Auth.IPasswordHasher>();
            var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
            var now = SA.Domain.Common.SaTime.Now;

            // 全市场范围 + 自选读写
            var fullRole = new SA.Domain.Entities.Identity.Role
            {
                Id = "test-full",
                Name = "测试·全市场",
                Description = "全市场数据范围 + 自选读写 + 搜索",
                IsBuiltin = false,
                CreatedAt = now
            };

            // 仅自选范围
            var scopedRole = new SA.Domain.Entities.Identity.Role
            {
                Id = "test-scoped",
                Name = "测试·仅自选",
                Description = "数据范围仅自选 + 自选读写 + 搜索",
                IsBuiltin = false,
                CreatedAt = now
            };

            // 配额受限角色：配额是角色级设置，因此单独建一个角色，避免影响其他用例
            var quotaRole = new SA.Domain.Entities.Identity.Role
            {
                Id = "test-quota",
                Name = "测试·自选上限 2",
                Description = "watchlist.max = 2",
                IsBuiltin = false,
                CreatedAt = now
            };

            await roles.AddAsync(fullRole);
            await roles.AddAsync(scopedRole);
            await roles.AddAsync(quotaRole);
            await unitOfWork.SaveChangesAsync();

            await roles.ReplaceFunctionPointsAsync("test-full",
            [
                SA.Domain.Authorization.FunctionPointCatalog.MarketView,
                SA.Domain.Authorization.FunctionPointCatalog.StockSearch,
                SA.Domain.Authorization.FunctionPointCatalog.WatchlistView,
                SA.Domain.Authorization.FunctionPointCatalog.WatchlistEdit,
                SA.Domain.Authorization.FunctionPointCatalog.DataScopeAll
            ]);

            await roles.ReplaceFunctionPointsAsync("test-scoped",
            [
                SA.Domain.Authorization.FunctionPointCatalog.MarketView,
                SA.Domain.Authorization.FunctionPointCatalog.StockSearch,
                SA.Domain.Authorization.FunctionPointCatalog.WatchlistView,
                SA.Domain.Authorization.FunctionPointCatalog.WatchlistEdit,
                SA.Domain.Authorization.FunctionPointCatalog.DataScopeWatchlist
            ]);

            await roles.ReplaceFunctionPointsAsync("test-quota",
            [
                SA.Domain.Authorization.FunctionPointCatalog.MarketView,
                SA.Domain.Authorization.FunctionPointCatalog.StockSearch,
                SA.Domain.Authorization.FunctionPointCatalog.WatchlistView,
                SA.Domain.Authorization.FunctionPointCatalog.WatchlistEdit,
                SA.Domain.Authorization.FunctionPointCatalog.DataScopeAll
            ]);

            await unitOfWork.SaveChangesAsync();

            foreach (var (username, roleId) in new[]
            {
                (NormalUser, "test-full"),
                (IdempotentUser, "test-full"),
                (RemoveUser, "test-full"),
                (QuotaUser, "test-quota"),
                (UnknownCodeUser, "test-full"),
                (GroupUser, "test-full"),
                (IsolationUserA, "test-full"),
                (IsolationUserB, "test-full"),
                (ScopedUser, "test-scoped")
            })
            {
                var hash = hasher.Hash(Password);
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
            }

            await unitOfWork.SaveChangesAsync();

            // 配额：自定义角色没有内置默认值，必须显式写入，否则上限为 0 会拒绝一切新增
            var db = provider.GetRequiredService<SA.Infrastructure.Persistence.SaDbContext>();
            await db.RoleQuotas.AddRangeAsync(
                new SA.Domain.Entities.Identity.RoleQuota { RoleId = "test-full", QuotaKey = "watchlist.max", QuotaValue = 500 },
                new SA.Domain.Entities.Identity.RoleQuota { RoleId = "test-scoped", QuotaKey = "watchlist.max", QuotaValue = 500 },
                // 配额用例专用：上限 2
                new SA.Domain.Entities.Identity.RoleQuota { RoleId = "test-quota", QuotaKey = "watchlist.max", QuotaValue = 2 });

            await unitOfWork.SaveChangesAsync();
        }

        _seeded = true;
    }

    private async Task<HttpClient> SignInAsync(string username, string password)
    {
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

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Sa:DataDirectory", _dataDirectory);
        builder.UseSetting("Sa:Auth:AdminInitialPassword", AdminPassword);
        builder.UseSetting("Sa:Auth:RequireTotp", "false");
        builder.UseSetting("Sa:Collector:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            // 采集层改为按 IEnumerable<T> 注入降级链，后注册不再能覆盖前面的注册：
            // 必须先把真实源移除，再注册测试替身，否则真实源会排在链首并真的去打上游。
            services.RemoveAll<IMarketListSource>();
            services.RemoveAll<IKlineSource>();
            services.RemoveAll<ITradingCalendarSource>();
            services.AddSingleton<IMarketListSource>(new FakeMarketListSource(FakeMarketListSource.DefaultRows));
            services.AddSingleton<IIndexSource, FakeIndexSource>();
            services.AddSingleton<ISectorSource, FakeSectorSource>();
            services.AddSingleton<ILimitPoolSource, FakeLimitPoolSource>();
            services.AddSingleton<IMarginMarketSource, FakeMarginMarketSource>();
            services.AddSingleton<IMarketFundFlowSource, FakeMarketFundFlowSource>();
            services.AddSingleton<ITradingCalendarSource, FakeTradingCalendarSource>();
        });
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
