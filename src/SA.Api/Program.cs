using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using SA.Api.Auth;
using SA.Api.Endpoints;
using SA.Api.Http;
using SA.Api.Middleware;
using SA.Application;
using SA.Application.Auth;
using SA.Contracts.Common;
using SA.Infrastructure;
using SA.Infrastructure.Persistence;
using SA.Infrastructure.Security;
using SA.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// 响应包字段名固定为 camelCase（docs/详细设计.md §1.3）
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SA.Application.Authorization.IUserContext, SA.Api.Auth.HttpUserContext>();

// 实时行情：订阅登记、在线跟踪、推送服务（详细设计 §7）
builder.Services.AddSignalR();
builder.Services.AddSingleton<SA.Api.Hubs.SubscriptionRegistry>();
builder.Services.AddSingleton<SA.Api.Hubs.PresenceTracker>();
builder.Services.AddHostedService<SA.Api.Realtime.QuotePushService>();
// 提醒评估：按固定节拍评估启用中的规则，触发即写通知并经 SignalR 推送
builder.Services.AddHostedService<SA.Api.Realtime.AlertEvaluationService>();
builder.Services.AddSaDataPaths(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddSaApplication(builder.Configuration);
builder.Services.AddSaMarket(builder.Configuration);
builder.Services.AddSaPersistence();
builder.Services.AddSaMarketStores();
builder.Services.AddSaHistory();
// 采集适配器、任务与调度宿主服务（实施计划 §5.4：单进程是默认形态）
builder.Services.AddSaCollect();
builder.Services.AddSaSecurity();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<AuthOptions, ISigningKeyProvider>((options, auth, signingKey) =>
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = SaJwtValidation.Create(auth, signingKey);

        // 浏览器建立 WebSocket 时无法自定义请求头，SignalR 官方做法是把令牌放进
        // access_token 查询参数。这里仅在 /hubs 路径下接受该来源，避免把整站的令牌
        // 校验放宽到查询串（查询串会进访问日志）。
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    });

// 本应用不做权限控制：所有功能对所有访问者开放（用户决定去掉登录与权限）。
// 因此不设 fallback policy —— 默认即匿名放行；也不再注册功能点授权处理器。
// 保留 AddAuthorization() 是因为 SignalR Hub 与部分中间件仍会解析授权元数据。
builder.Services.AddAuthorization();

var app = builder.Build();

// 反向代理（nginx）终止 TLS 后，应用侧看到的是 http。不加这一层，
// Request.IsHttps 恒为 false，刷新令牌 Cookie 的 Secure 标记就永远拿不到
// （AuthEndpoints.cs 里 Secure = context.Request.IsHttps），登录态少一层保护。
//
// 只信任回环与显式配置的代理（Sa:TrustedProxies）：默认的 KnownNetworks 会放宽到整个
// 内网段，而 X-Forwarded-For / X-Forwarded-Proto 是客户端可伪造的头，
// 无差别信任等于让外部请求自己声明来源与协议。
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownIPNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
forwardedOptions.KnownProxies.Add(System.Net.IPAddress.Loopback);
forwardedOptions.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);
foreach (var proxy in builder.Configuration.GetSection("Sa:TrustedProxies").Get<string[]>() ?? [])
{
    if (System.Net.IPAddress.TryParse(proxy, out var address))
    {
        forwardedOptions.KnownProxies.Add(address);
    }
}

// 必须排在最前：后面的中间件（含认证与 Cookie 写入）都要看到修正后的协议与来源 IP
app.UseForwardedHeaders(forwardedOptions);

// 启动初始化：建库迁移、开启 WAL、确定签名密钥、播种功能点与内置角色、创建初始管理员
using (var scope = app.Services.CreateScope())
{
    var paths = scope.ServiceProvider.GetRequiredService<DataPaths>();
    paths.EnsureCreated();
    app.Logger.LogInformation("数据目录：{DataRoot}", paths.Root);

    var initializer = scope.ServiceProvider.GetRequiredService<PersistenceInitializer>();
    await initializer.InitializeAsync();
}

app.UseMiddleware<TraceIdMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthentication();

app.UseAuthorization();

app.MapSystemEndpoints();
app.MapMarketEndpoints();
app.MapSearchEndpoints();
app.MapStockEndpoints();
app.MapFinanceEndpoints();
app.MapEquityEndpoints();
app.MapCapitalEndpoints();
app.MapIndustryEndpoints();
app.MapEventEndpoints();
app.MapRatingEndpoints();
app.MapRiskEndpoints();
app.MapAlertEndpoints();
app.MapScreenerEndpoints();
// 规则引擎推算：行业景气度与因果链/传导带宽（实施计划 §2 决策 13）
app.MapAnalysisEndpoints();
// Web Push 与提醒偏好（免打扰在服务端执行）
app.MapPushEndpoints();
app.MapAdminEndpoints();
// 站点信息（页头名称与全局公告）：只要求登录，页头在每个页面都要用
app.MapSiteEndpoints();
app.MapWatchlistEndpoints();

// 实时行情只推自选股，按连接节流（详细设计 §7）
app.MapHub<SA.Api.Hubs.QuoteHub>("/hubs/quotes");

app.Run();

/// <summary>供集成测试通过 WebApplicationFactory 引用宿主。</summary>
public partial class Program;
