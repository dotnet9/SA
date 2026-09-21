using Microsoft.Extensions.Logging.Abstractions;
using SA.Application.Abstractions;
using SA.Infrastructure.Collect.Http;

namespace SA.Api.Tests;

/// <summary>
/// 采集层防风控组件：按域名熔断与按域名限速（实施计划 §4.1、§4.4）。
/// </summary>
/// <remarks>
/// 这两条都有明确的<b>实测动机</b>，因此断言也针对动机而不是实现细节：
/// 熔断必须按 host（否则 <c>push2his</c> 的 K 线被拒会误杀同主机的资金流），
/// 限速必须按 host（否则「查财务」会把「查行情」的配额吃掉）。
/// </remarks>
public class HostGuardTests
{
    private static CollectOptions Options(Action<CollectOptions>? configure = null)
    {
        var options = new CollectOptions();
        configure?.Invoke(options);
        return options;
    }

    private static HostCircuitBreaker Breaker(CollectOptions options) =>
        new(options, NullLogger<HostCircuitBreaker>.Instance);

    /* ------------------------------------------------------------------
       熔断器
       ------------------------------------------------------------------ */

    [Fact]
    public void 连续失败达到阈值后进入冷却并立即抛异常()
    {
        var breaker = Breaker(Options(o =>
        {
            o.BreakerThreshold = 3;
            o.BreakerCooldownSeconds = 60;
        }));

        const string host = "push2his.eastmoney.com";

        // 阈值以下仍放行
        breaker.RecordFailure(host);
        breaker.RecordFailure(host);
        breaker.EnsureAvailable(host);

        // 第 3 次失败即熔断
        breaker.RecordFailure(host);
        var blocked = Assert.Throws<HostBlockedException>(() => breaker.EnsureAvailable(host));
        Assert.Equal(host, blocked.Host);
        Assert.True(blocked.BlockedUntil > DateTimeOffset.UtcNow);
    }

    [Fact]
    public void 一次成功即清零失败计数()
    {
        var breaker = Breaker(Options(o => o.BreakerThreshold = 3));

        const string host = "datacenter-web.eastmoney.com";
        breaker.RecordFailure(host);
        breaker.RecordFailure(host);
        breaker.RecordSuccess(host);

        // 清零后再失败 2 次仍不应熔断
        breaker.RecordFailure(host);
        breaker.RecordFailure(host);
        breaker.EnsureAvailable(host);
    }

    [Fact]
    public void 熔断按域名隔离不会误杀同主机的其它端点()
    {
        var breaker = Breaker(Options(o =>
        {
            o.BreakerThreshold = 1;
            o.BreakerCooldownSeconds = 60;
        }));

        // 实测：push2his 的 kline 被连接重置，而同一主机的 fflow 正常。
        // 若按「数据源」熔断，这里会把资金流一起打死。
        breaker.RecordFailure("push2his.eastmoney.com");
        Assert.Throws<HostBlockedException>(() => breaker.EnsureAvailable("push2his.eastmoney.com"));

        breaker.EnsureAvailable("datacenter-web.eastmoney.com");
        breaker.EnsureAvailable("qt.gtimg.cn");
    }

    [Fact]
    public void 冷却设为0表示关闭熔断()
    {
        var breaker = Breaker(Options(o =>
        {
            o.BreakerThreshold = 1;
            o.BreakerCooldownSeconds = 0;
        }));

        const string host = "web.ifzq.gtimg.cn";

        // 冷却 0 秒 = 不熔断（供排查时临时关掉），任何次数都不应抛
        for (var i = 0; i < 10; i++)
        {
            breaker.RecordFailure(host);
        }

        breaker.EnsureAvailable(host);
    }

    [Fact]
    public async Task 冷却到期后放行一次试探成功即恢复()
    {
        var breaker = Breaker(Options(o =>
        {
            o.BreakerThreshold = 1;
            o.BreakerCooldownSeconds = 1;
        }));

        const string host = "push2delay.eastmoney.com";
        breaker.RecordFailure(host);
        Assert.Throws<HostBlockedException>(() => breaker.EnsureAvailable(host));

        // 等过冷却窗口
        await Task.Delay(TimeSpan.FromMilliseconds(1100));

        // 半开：这一次放行（试探）
        breaker.EnsureAvailable(host);
        // 试探在途时后续请求仍被挡住，否则「半开」会退化成「全开」，
        // 刚恢复的上游会被瞬间打回冷却
        Assert.Throws<HostBlockedException>(() => breaker.EnsureAvailable(host));

        // 试探成功 → 熔断解除，恢复放行
        breaker.RecordSuccess(host);
        breaker.EnsureAvailable(host);
        breaker.EnsureAvailable(host);
    }

    [Fact]
    public async Task 半开试探失败后重新进入冷却()
    {
        var breaker = Breaker(Options(o =>
        {
            o.BreakerThreshold = 1;
            o.BreakerCooldownSeconds = 1;
        }));

        const string host = "web.ifzq.gtimg.cn";
        breaker.RecordFailure(host);
        await Task.Delay(TimeSpan.FromMilliseconds(1100));

        // 试探放行
        breaker.EnsureAvailable(host);
        // 试探失败 → 重新冷却
        breaker.RecordFailure(host);

        Assert.Throws<HostBlockedException>(() => breaker.EnsureAvailable(host));
    }

    /* ------------------------------------------------------------------
       按域名限速
       ------------------------------------------------------------------ */

    [Fact]
    public async Task 不同域名互不阻塞()
    {
        var limiter = new HostRateLimiter(Options(o => o.PerHostMinIntervalMs = 5000));

        // 第一个域名占用自己的时间片
        Assert.Equal(TimeSpan.Zero, await limiter.WaitAsync("push2.eastmoney.com"));

        // 另一个域名不该等第一个域名的时间片（这是「按域名」的核心语义）
        Assert.Equal(TimeSpan.Zero, await limiter.WaitAsync("datacenter-web.eastmoney.com"));
    }

    [Fact]
    public async Task 同一域名的第二次请求需要排队()
    {
        var limiter = new HostRateLimiter(Options(o => o.PerHostMinIntervalMs = 200));

        await limiter.WaitAsync("push2.eastmoney.com");
        var waited = await limiter.WaitAsync("push2.eastmoney.com");

        // 200ms 的间隔允许调度误差，断言「确实等了」而不是精确值
        Assert.True(waited > TimeSpan.Zero, $"期望需要排队，实际 waited={waited}");
        Assert.True(waited <= TimeSpan.FromMilliseconds(400), $"等待过久：{waited}");
    }

    [Fact]
    public async Task 限速关闭时不等待()
    {
        var limiter = new HostRateLimiter(Options(o => o.PerHostMinIntervalMs = 0));

        Assert.Equal(TimeSpan.Zero, await limiter.WaitAsync("push2.eastmoney.com"));
        Assert.Equal(TimeSpan.Zero, await limiter.WaitAsync("push2.eastmoney.com"));
    }

    [Fact]
    public async Task 域名大小写不影响分桶()
    {
        var limiter = new HostRateLimiter(Options(o => o.PerHostMinIntervalMs = 5000));

        Assert.Equal(TimeSpan.Zero, await limiter.WaitAsync("Push2.EastMoney.com"));
        // 同一个桶 → 需要排队（若按大小写分成两个桶就会立刻返回）
        Assert.True(await limiter.WaitAsync("push2.eastmoney.com") > TimeSpan.Zero);
    }

    /* ------------------------------------------------------------------
       域名解析
       ------------------------------------------------------------------ */

    [Theory]
    [InlineData("https://push2his.eastmoney.com/api/qt/stock/kline/get?secid=0.300750", "push2his.eastmoney.com")]
    [InlineData("https://qt.gtimg.cn/q=sz300750", "qt.gtimg.cn")]
    [InlineData("https://web.ifzq.gtimg.cn/appstock/app/fqkline/get?param=sz300750,day", "web.ifzq.gtimg.cn")]
    public void 从URL取出域名(string url, string expected) =>
        Assert.Equal(expected, CollectHttpClient.ResolveHost(url));

    [Fact]
    public void 非法URL退化为整串而不抛异常() =>
        Assert.Equal("not-a-url", CollectHttpClient.ResolveHost("not-a-url"));
}
