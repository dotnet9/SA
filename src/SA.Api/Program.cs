using System.Text.Json;
using System.Text.Json.Serialization;
using SA.Api.Endpoints;
using SA.Api.Middleware;
using SA.Infrastructure;
using SA.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// 响应包字段名固定为 camelCase（docs/详细设计.md §1.3）
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

builder.Services.AddSaDataPaths(builder.Configuration, builder.Environment.ContentRootPath);

var app = builder.Build();

var paths = app.Services.GetRequiredService<DataPaths>();
paths.EnsureCreated();
app.Logger.LogInformation("数据目录：{DataRoot}", paths.Root);

app.UseMiddleware<TraceIdMiddleware>();

app.MapSystemEndpoints();

app.Run();

/// <summary>供集成测试通过 WebApplicationFactory 引用宿主。</summary>
public partial class Program;
