using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Domain.Common;

namespace SA.Infrastructure.Collect.Http;

/// <summary>
/// 按<b>域名</b>维度的熔断器。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么必须按域名而不是按数据源</b>：实测（2026-09-21 本机复核，与实施计划 §4.1 一致）
/// <c>push2his.eastmoney.com</c> 的 K 线端点被连接重置，而<b>同一主机的</b>
/// <c>stock/fflow/daykline</c> 端点正常返回 200。若按数据源熔断，「东方财富 · K 线」被熔断
/// 会连带把同域名的资金流一起误杀。
/// </para>
/// <para>
/// 状态机：同一 host 连续失败达 <see cref="CollectOptions.BreakerThreshold"/> 次 → 冷却
/// <see cref="CollectOptions.BreakerCooldownSeconds"/> 秒；冷却期内对该 host 的请求<b>立即</b>
/// 抛 <see cref="HostBlockedException"/>（不消耗重试预算、不占用限速时间片），由上层降级到备用源；
/// 冷却到期只放行<b>一次</b>试探（半开），成功即清零，失败则重新冷却。
/// </para>
/// <para>
/// 计数口径是「连续失败的<b>请求</b>」而不是「失败的尝试」：一次请求内部的 3 次重试算一次失败，
/// 否则阈值 3 与重试 3 次会变成「一个请求就把 host 打死」。
/// </para>
/// </remarks>
public sealed class HostCircuitBreaker(CollectOptions options, ILogger<HostCircuitBreaker> logger)
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>阈值（读一次，避免每次判定都走属性）。</summary>
    private int Threshold => Math.Max(1, options.BreakerThreshold);

    /// <summary>
    /// 判断该 host 当前是否可用；被熔断时抛 <see cref="HostBlockedException"/>。
    /// </summary>
    /// <param name="host">域名。</param>
    /// <exception cref="HostBlockedException">处于冷却期，或半开试探已被其它请求占用。</exception>
    public void EnsureAvailable(string host)
    {
        if (options.BreakerCooldownSeconds <= 0)
        {
            return;
        }

        var entry = _entries.GetOrAdd(host, static _ => new Entry());
        DateTimeOffset? blockedUntil = null;
        var isProbe = false;

        lock (entry.Gate)
        {
            if (entry.Failures >= Threshold)
            {
                if (entry.BlockedUntil > SaTime.Now)
                {
                    blockedUntil = entry.BlockedUntil;
                }
                else if (entry.ProbeInFlight)
                {
                    // 半开期只放行一个：否则「半开」退化成「全开」，
                    // 刚恢复的上游会被瞬间打回冷却。
                    blockedUntil = SaTime.Now.AddSeconds(1);
                }
                else
                {
                    entry.ProbeInFlight = true;
                    isProbe = true;
                }
            }
        }

        if (blockedUntil is not null)
        {
            throw new HostBlockedException(host, blockedUntil.Value);
        }

        if (isProbe)
        {
            logger.LogInformation("{Host} 冷却到期，放行一次试探请求（半开）", host);
        }
    }

    /// <summary>记一次请求成功：清零失败计数并解除熔断。</summary>
    public void RecordSuccess(string host)
    {
        if (!_entries.TryGetValue(host, out var entry))
        {
            return;
        }

        lock (entry.Gate)
        {
            if (entry.Failures >= Threshold)
            {
                logger.LogInformation("{Host} 试探成功，熔断解除（此前连续失败 {Failures} 次）", host, entry.Failures);
            }

            entry.Failures = 0;
            entry.BlockedUntil = default;
            entry.ProbeInFlight = false;
        }
    }

    /// <summary>记一次请求失败：达到阈值即进入冷却。</summary>
    public void RecordFailure(string host)
    {
        var entry = _entries.GetOrAdd(host, static _ => new Entry());

        lock (entry.Gate)
        {
            entry.ProbeInFlight = false;
            entry.Failures++;

            if (entry.Failures >= Threshold)
            {
                entry.BlockedUntil = SaTime.Now.AddSeconds(options.BreakerCooldownSeconds);
                logger.LogWarning(
                    "{Host} 连续失败 {Failures} 次，熔断 {Seconds} 秒",
                    host, entry.Failures, options.BreakerCooldownSeconds);
            }
        }
    }

    private sealed class Entry
    {
        /// <summary>保护下面三个字段的锁（<see cref="ConcurrentDictionary{TKey,TValue}"/> 只保证字典本身）。</summary>
        public Lock Gate { get; } = new();

        /// <summary>连续失败次数。</summary>
        public int Failures { get; set; }

        /// <summary>冷却截止时刻。</summary>
        public DateTimeOffset BlockedUntil { get; set; }

        /// <summary>是否已有一次半开试探在途。</summary>
        public bool ProbeInFlight { get; set; }
    }
}

/// <summary>
/// 域名处于熔断冷却期。抛出它的目的是让上层<b>立刻</b>降级到备用源，而不是继续重试。
/// </summary>
/// <remarks>
/// 故意不继承 <see cref="CollectHttpException"/>（该类是 sealed，且语义不同：这里并没有发出请求，
/// 也就没有 HTTP 状态码）。调用方按 <see cref="Exception"/> 兜底即可，不会漏掉。
/// </remarks>
public sealed class HostBlockedException(string host, DateTimeOffset blockedUntil)
    : Exception($"域名 {host} 处于熔断冷却期，{SaTime.Format(blockedUntil)} 前不再请求")
{
    /// <summary>被熔断的域名。</summary>
    public string Host { get; } = host;

    /// <summary>冷却截止时刻。</summary>
    public DateTimeOffset BlockedUntil { get; } = blockedUntil;
}
