using System.Collections.Concurrent;
using SA.Application.Abstractions;
using SA.Domain.Common;

namespace SA.Infrastructure.Collect;

/// <summary>
/// 按需采集队列的进程内实现。
/// </summary>
/// <remarks>
/// 用「待处理集合 + 最近处理时间」而不是纯队列：同一只股票在一分钟内可能被反复请求
/// （用户刷新、多个页面同时拉），去重窗口避免把同一标的排成几十条任务。
/// </remarks>
public sealed class OnDemandQueue : IOnDemandQueue
{
    /// <summary>同一标的的重复请求在该窗口内被忽略。</summary>
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromMinutes(2);

    /// <summary>队列长度上限，防止异常客户端把内存打满。</summary>
    private const int MaxPending = 200;

    private readonly ConcurrentQueue<string> _pending = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _recent = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public int PendingCount => _pending.Count;

    /// <inheritdoc />
    public bool TryEnqueue(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalized = code.Trim();
        var now = SaTime.Now;

        // 处理过或正在排队的直接跳过
        if (_recent.TryGetValue(normalized, out var lastAt) && now - lastAt < DedupeWindow)
        {
            return false;
        }

        if (_pending.Count >= MaxPending)
        {
            return false;
        }

        _pending.Enqueue(normalized);
        _recent[normalized] = now;
        PruneRecent(now);
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Drain(int max)
    {
        var take = Math.Max(1, max);
        var result = new List<string>(take);

        while (result.Count < take && _pending.TryDequeue(out var code))
        {
            result.Add(code);
        }

        // 出队即刷新时间戳：处理失败时 2 分钟内不会再次入队，避免热循环
        var now = SaTime.Now;
        foreach (var code in result)
        {
            _recent[code] = now;
        }

        return result;
    }

    private void PruneRecent(DateTimeOffset now)
    {
        if (_recent.Count <= 500)
        {
            return;
        }

        foreach (var entry in _recent)
        {
            if (now - entry.Value >= DedupeWindow)
            {
                _recent.TryRemove(entry.Key, out _);
            }
        }
    }
}
