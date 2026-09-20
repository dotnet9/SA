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
app.MapWatchlistEndpoints();

app.Run();

/// <summary>供集成测试通过 WebApplicationFactory 引用宿主。</summary>
public partial class Program;
