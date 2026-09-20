using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Infrastructure.Collect.Registry;

namespace SA.Infrastructure.Collect.Jobs;

/// <summary>
/// 机构评级采集任务。
/// </summary>
/// <remarks>
/// 评级数据由券商研报驱动，更新频率不确定（可能一天多次，也可能数月不变），
/// 因此不设独立轮询节奏，只随按需采集补齐：用户打开评级页或总览页时若缺数据即入队。
/// 覆盖机构为 0 是合法状态（小市值标的常常无人覆盖），此时不写库、不报错。
/// </remarks>
public sealed class RatingJob(
    SourceRegistry registry,
    CollectExecutor executor,
    IRatingStore store,
    ILogger<RatingJob> logger)
{
    /// <summary>任务名。</summary>
    public const string TaskName = "rating";

    /// <summary>
    /// 采集一个标的的机构评级。
    /// </summary>
    /// <returns>写入行数；无覆盖或失败返回 0。</returns>
    public async Task<int> RunAsync(string code, CancellationToken cancellationToken = default)
    {
        var result = await executor.ExecuteAsync(
            $"{TaskName}:{code}",
            registry.Rating,
            "主源",
            async ct =>
            {
                var consensus = await registry.Rating.GetConsensusAsync(code, ct).ConfigureAwait(false);
                if (consensus is null)
                {
                    // 无机构覆盖：不是错误，记 0 行
                    return (0, 0);
                }

                var written = await store.UpsertAsync(consensus, ct).ConfigureAwait(false);
                return (written, written);
            },
            cancellationToken).ConfigureAwait(false);

        if (result > 0)
        {
            logger.LogInformation("{Code} 机构评级已更新", code);
        }

        return result;
    }
}
