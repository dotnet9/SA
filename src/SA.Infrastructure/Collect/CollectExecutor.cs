using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Domain.Entities.Collect;
using SA.Domain.Common;

namespace SA.Infrastructure.Collect;

/// <summary>
/// 采集执行的统一外壳：计时、异常收敛、写 <see cref="CollectTaskLog"/> 与
/// <see cref="DataSourceStatus"/>。
/// </summary>
/// <remarks>
/// 每个采集任务都必须经由这里执行，以保证「任何一源失败都能在数据源监控页看到」
/// （实施计划 §5.4）。失败不抛出：单个任务失败不应让整个宿主或调度循环崩溃，
/// 由上层按返回值决定是否继续。
/// </remarks>
public sealed class CollectExecutor(
    ICollectStatusStore status,
    SourceCooldown cooldown,
    CollectOptions options,
    ILogger<CollectExecutor> logger)
{
    /// <summary>
    /// 执行一次采集。数据源处于冷却期时直接跳过并返回 null。
    /// </summary>
    /// <typeparam name="T">结果类型。</typeparam>
    /// <param name="taskName">任务名（写入任务日志）。</param>
    /// <param name="source">数据源（写入数据源状态）。</param>
    /// <param name="sourceType">主源 / 备源。</param>
    /// <param name="action">实际取数逻辑，返回结果与写入行数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功时返回结果；失败或被冷却跳过时返回 null。</returns>
    public async Task<T?> ExecuteAsync<T>(
        string taskName,
        IProbeable source,
        string sourceType,
        Func<CancellationToken, Task<(T Value, int Rows)>> action,
        CancellationToken cancellationToken = default)
    {
        if (await cooldown.IsCoolingDownAsync(source.Name, cancellationToken).ConfigureAwait(false))
        {
            return default;
        }

        var startedAt = SaTime.Now;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var (value, rows) = await action(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            await status.RecordSourceAsync(
                source.Name, source.Domains, sourceType, ok: true, stopwatch.ElapsedMilliseconds, null,
                options.DegradeAfterFailures, cancellationToken).ConfigureAwait(false);

            await status.AddTaskLogAsync(
                new CollectTaskLog
                {
                    TaskName = taskName,
                    Source = source.Name,
                    StartedAt = startedAt,
                    CostMs = (int)stopwatch.ElapsedMilliseconds,
                    Status = CollectStatuses.Ok,
                    RowsWritten = rows
                },
                cancellationToken).ConfigureAwait(false);

            logger.LogInformation("任务 {Task} 完成：{Rows} 行 / {Cost}ms（{Source}）", taskName, rows, stopwatch.ElapsedMilliseconds, source.Name);
            return value;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "任务 {Task} 失败（{Source}）", taskName, source.Name);

            await SafeRecordAsync(
                () => status.RecordSourceAsync(
                    source.Name, source.Domains, sourceType, ok: false, stopwatch.ElapsedMilliseconds, ex.Message,
                    options.DegradeAfterFailures, cancellationToken)).ConfigureAwait(false);

            await SafeRecordAsync(
                () => status.AddTaskLogAsync(
                    new CollectTaskLog
                    {
                        TaskName = taskName,
                        Source = source.Name,
                        StartedAt = startedAt,
                        CostMs = (int)stopwatch.ElapsedMilliseconds,
                        Status = CollectStatuses.Err,
                        RowsWritten = 0,
                        Error = Truncate(ex.Message, 500)
                    },
                    cancellationToken)).ConfigureAwait(false);

            return default;
        }
    }

    /// <summary>
    /// 写状态本身失败时不再向上抛出：否则「记录失败」会把采集任务变成宿主异常。
    /// </summary>
    private async Task SafeRecordAsync(Func<Task> record)
    {
        try
        {
            await record().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "写入采集状态失败（不阻断采集流程）");
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
