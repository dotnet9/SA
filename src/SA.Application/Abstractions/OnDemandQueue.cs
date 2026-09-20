namespace SA.Application.Abstractions;

/// <summary>
/// 按需采集队列。用户打开某只股票而本地缺数据时，接口返回 <c>1003</c> 并把该标的入队，
/// 由采集调度优先处理（实施计划 §5.4）。
/// </summary>
/// <remarks>
/// 进程内队列，不落库：它的语义是「现在缺、请尽快补」，重启后由游标与首次回补重新覆盖，
/// 落库反而会留下永远处理不完的历史请求。
/// </remarks>
public interface IOnDemandQueue
{
    /// <summary>
    /// 入队一个标的。已经在队列里或刚处理过（去重窗口内）时返回 false。
    /// </summary>
    /// <param name="code">证券代码。</param>
    bool TryEnqueue(string code);

    /// <summary>
    /// 取出至多 <paramref name="max"/> 个待处理标的，并标记为「处理中」。
    /// </summary>
    /// <param name="max">本次最多取多少个。</param>
    IReadOnlyList<string> Drain(int max);

    /// <summary>当前排队数量。</summary>
    int PendingCount { get; }
}
