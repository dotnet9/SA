using SA.Domain.Entities.Alerts;
using SA.Domain.Entities.Screener;
using SA.Domain.Entities.System;

namespace SA.Application.Abstractions;

/// <summary>
/// Web Push 订阅的读写。
/// </summary>
public interface IPushSubscriptionStore
{
    /// <summary>取某用户的全部订阅（含已停用的，便于界面展示与恢复）。</summary>
    Task<IReadOnlyList<PushSubscription>> GetByUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>取全部启用中的订阅（推送时按用户分组发送）。</summary>
    Task<IReadOnlyList<PushSubscription>> GetEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增或更新订阅（按端点去重）。
    /// </summary>
    /// <remarks>
    /// 同一端点重复订阅（刷新页面、重新授权）应当更新而不再插一条，
    /// 并且要重置失败计数与启用状态——用户重新授权本身就是「这个端点还活着」的证据。
    /// </remarks>
    Task<long> UpsertAsync(PushSubscription subscription, CancellationToken cancellationToken = default);

    /// <summary>按端点删除订阅（用户主动取消，或推送返回 410/404 时清理）。</summary>
    Task<int> RemoveByEndpointAsync(string endpoint, CancellationToken cancellationToken = default);

    /// <summary>记录一次推送结果：成功重置计数，失败累加并在到阈值时停用。</summary>
    Task RecordResultAsync(
        long subscriptionId,
        bool ok,
        string? error,
        int disableAfterFailures,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Web Push 发送。抽象出来是为了让「要不要发、发给谁」留在应用层可测，
/// 而把加密与 HTTP 细节（RFC 8291/8292）留给基础设施层。
/// </summary>
public interface IPushSender
{
    /// <summary>VAPID 公钥（base64url）；前端订阅时需要它。</summary>
    string PublicKey { get; }

    /// <summary>
    /// 发送一条通知。
    /// </summary>
    /// <param name="subscription">目标订阅。</param>
    /// <param name="payloadJson">载荷 JSON（客户端自行解析为 Notification）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功返回 null；失败返回错误摘要，同时给出端点是否已失效。</returns>
    Task<PushSendResult> SendAsync(
        PushSubscription subscription,
        string payloadJson,
        CancellationToken cancellationToken = default);
}

/// <summary>推送发送结果。</summary>
/// <param name="Ok">是否成功。</param>
/// <param name="Error">失败摘要。</param>
/// <param name="Permanent">是否为永久失败（410/404，应当删除该订阅）。</param>
public readonly record struct PushSendResult(bool Ok, string? Error, bool Permanent)
{
    /// <summary>成功。</summary>
    public static PushSendResult Success() => new(true, null, false);

    /// <summary>可重试的失败。</summary>
    public static PushSendResult Transient(string error) => new(false, error, false);

    /// <summary>永久失败：订阅已失效，应当清理。</summary>
    public static PushSendResult Gone(string error) => new(false, error, true);
}

/// <summary>
/// 选股器执行记录（筛选日志 + 我的策略）的读写。
/// </summary>
public interface IScreenerRunStore
{
    /// <summary>取最近执行记录（可按「仅策略」过滤）。</summary>
    Task<IReadOnlyList<ScreenerRun>> GetRecentAsync(
        string userId,
        bool strategiesOnly,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>取单条记录。</summary>
    Task<ScreenerRun?> FindAsync(string userId, long id, CancellationToken cancellationToken = default);

    /// <summary>按名字取策略（用于「名称不能重复」的校验）。</summary>
    Task<ScreenerRun?> FindByNameAsync(string userId, string name, CancellationToken cancellationToken = default);

    /// <summary>新增一条记录，返回自增 Id。</summary>
    Task<long> AddAsync(ScreenerRun run, CancellationToken cancellationToken = default);

    /// <summary>重命名/取消策略（<paramref name="name"/> 为 null 表示取消策略标记）。</summary>
    Task<bool> RenameAsync(string userId, long id, string? name, CancellationToken cancellationToken = default);

    /// <summary>删除记录，返回实际删除条数。</summary>
    Task<int> DeleteAsync(string userId, long id, CancellationToken cancellationToken = default);

    /// <summary>裁剪历史：只保留最近若干条「非策略」记录，返回删除条数。</summary>
    Task<int> TrimAsync(string userId, int keepNonStrategy, CancellationToken cancellationToken = default);

    /// <summary>策略数量（配额校验用）。</summary>
    Task<int> CountStrategiesAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 导出记录的读写。对应表 <c>ExportLog</c>（实施计划 §5.3 / §9：导出必须写操作日志）。
/// </summary>
public interface IExportLogStore
{
    /// <summary>写一条导出记录。</summary>
    Task AddAsync(ExportLog log, CancellationToken cancellationToken = default);

    /// <summary>取某用户最近的导出记录。</summary>
    Task<IReadOnlyList<ExportLog>> GetRecentAsync(
        string userId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>某用户今日已导出次数（按业务日统计）。</summary>
    Task<int> CountTodayAsync(string userId, CancellationToken cancellationToken = default);
}
