using System.Net;
using System.Text.Json;

namespace SA.Api.Tests;

/// <summary>
/// 统一响应包与 traceId 的契约测试。所有端点都依赖这两点，
/// 因此这里锁定的是「前端能否稳定解包」的前提（实施计划 §5.1）。
/// </summary>
public class HealthEndpointTests(ApiFactory factory)
    : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task 健康检查返回统一响应包与追踪标识()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Trace-Id", out var traceHeaders), "响应缺少 X-Trace-Id 头");
        var headerTraceId = Assert.Single(traceHeaders);
        Assert.False(string.IsNullOrWhiteSpace(headerTraceId));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        // 字段名必须是 camelCase 且四个字段齐全
        Assert.Equal(0, root.GetProperty("code").GetInt32());
        Assert.Equal("ok", root.GetProperty("message").GetString());
        Assert.Equal(headerTraceId, root.GetProperty("traceId").GetString());

        var data = root.GetProperty("data");
        Assert.Equal("ok", data.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("version").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("environment").GetString()));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$", data.GetProperty("serverTime").GetString()!);
    }

    [Fact]
    public async Task 透传入站追踪标识用于跨端对照()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("X-Trace-Id", "probe-1234");

        using var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("X-Trace-Id", out var traceHeaders));
        Assert.Equal("probe-1234", Assert.Single(traceHeaders));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("probe-1234", document.RootElement.GetProperty("traceId").GetString());
    }

}
