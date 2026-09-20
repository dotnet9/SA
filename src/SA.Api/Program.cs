using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using SA.Api.Auth;
using SA.Api.Endpoints;
using SA.Api.Middleware;
using SA.Application;
using SA.Application.Auth;
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

// 访问令牌校验。签名密钥要到启动初始化才确定（配置 → 库 → 生成），因此这里用延迟配置：
// JwtBearerOptions 在首个请求时才解析，届时密钥已就绪。
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

// 默认要求已登录，避免新增端点时忘记声明认证而意外裸奔（需求规格 §7.3）
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddSingleton<IAuthorizationHandler, FunctionPointAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, SaAuthorizationResultHandler>();

var app = builder.Build();

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
app.MapAuthEndpoints();
app.MapMeEndpoints();
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
app.MapAdminEndpoints();
// 站点信息（页头名称与全局公告）：只要求登录，页头在每个页面都要用
app.MapSiteEndpoints();
app.MapWatchlistEndpoints();

// 实时行情只推自选股，按连接节流（详细设计 §7）
app.MapHub<SA.Api.Hubs.QuoteHub>("/hubs/quotes");

app.Run();

/// <summary>供集成测试通过 WebApplicationFactory 引用宿主。</summary>
public partial class Program;
