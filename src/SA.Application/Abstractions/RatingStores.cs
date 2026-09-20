using SA.Domain.Entities.Rating;

namespace SA.Application.Abstractions;

/// <summary>
/// 机构评级与盈利预测源（数据中心 <c>RPT_WEB_RESPREDICT</c>）。
/// </summary>
public interface IRatingSource : IProbeable
{
    /// <summary>
    /// 取某标的的评级共识；该标的暂无机构覆盖时返回 null。
    /// </summary>
    Task<RatingConsensus?> GetConsensusAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>
/// 机构评级存储。
/// </summary>
public interface IRatingStore
{
    /// <summary>取评级共识。</summary>
    Task<RatingConsensus?> GetAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>写入或覆盖评级共识。</summary>
    Task<int> UpsertAsync(RatingConsensus consensus, CancellationToken cancellationToken = default);
}
