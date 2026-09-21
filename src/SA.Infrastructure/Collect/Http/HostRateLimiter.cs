using System.Collections.Concurrent;
using SA.Application.Abstractions;

namespace SA.Infrastructure.Collect.Http;

/// <summary>
/// 按<b>域名</b>维度的令牌桶限速：每个域名独立计时，互不占用对方的时间片。
/// </summary>
/// <remarks>
/// <para>
/// 取代了原先的全局令牌桶（<c>RequestsPerSecond</c>）。全局桶的问题是「查财务」会把
/// 「查行情」的配额吃掉：一轮全市场扫描（约 60 次请求）之后，同一时刻的指数刷新只能排队。
/// 按域名分桶后，<c>push2</c>、<c>push2his</c>、<c>datacenter-web</c> 各自按自己的节奏走。
/// </para>
/// <para>
/// 本类只管「什么时候可以发」，不负责发；并发闸门仍在 <see cref="CollectHttpClient"/> 上，
/// 是全局的——上限针对本机出网连接数，不针对单个上游。
/// </para>
/// </remarks>
public sealed class HostRateLimiter(CollectOptions options)
{
    private readonly ConcurrentDictionary<string, long> _slots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    /// <summary>
    /// 等到该域名下一个可用时间片。
    /// </summary>
    /// <param name="host">域名（大小写不敏感）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次实际等待的时长；未限速时为 <see cref="TimeSpan.Zero"/>。</returns>
    public async Task<TimeSpan> WaitAsync(string host, CancellationToken cancellationToken = default)
    {
        var intervalTicks = TimeSpan.FromMilliseconds(Math.Max(0, options.PerHostMinIntervalMs)).Ticks;

        long waitTicks = 0;
        if (intervalTicks > 0)
        {
            lock (_gate)
            {
                var now = DateTimeOffset.UtcNow.UtcTicks;
                // 该域名上一次预约的下一时间片；晚于 now 说明要排队
                var slot = _slots.TryGetValue(host, out var next) ? Math.Max(now, next) : now;
                _slots[host] = slot + intervalTicks;
                waitTicks = slot - now;
            }
        }

        if (waitTicks <= 0)
        {
            return TimeSpan.Zero;
        }

        var wait = TimeSpan.FromTicks(waitTicks);
        await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
        return wait;
    }
}
