using SA.Application.Abstractions;
using SA.Domain.Entities.Rating;

namespace SA.Api.Tests.Fakes;

/// <summary>
/// 机构评级源的测试替身：为 300750 返回确定性的评级共识，其余代码视为无机构覆盖。
/// </summary>
internal sealed class FakeRatingSource : IRatingSource
{
    public string Name => "测试源 · 机构评级";

    public string Domains => "评级";

    public Task<RatingConsensus?> GetConsensusAsync(string code, CancellationToken cancellationToken = default)
    {
        if (code != "300750")
        {
            // 无机构覆盖是合法状态，替身也要覆盖这条路径
            return Task.FromResult<RatingConsensus?>(null);
        }

        return Task.FromResult<RatingConsensus?>(new RatingConsensus
        {
            Code = code,
            RatingOrgNum = 35,
            BuyNum = 29,
            AddNum = 6,
            AimPriceMin = 500m,
            AimPriceMax = 656m,
            LongTermNum = 35,
            Year1 = 2025,
            YearMark1 = "A",
            Eps1 = 15.6m,
            Year2 = 2026,
            YearMark2 = "E",
            Eps2 = 20.83m,
            Year3 = 2027,
            YearMark3 = "E",
            Eps3 = 26.01m,
            IndustryBoard = "电池",
            UpdatedAt = SA.Domain.Common.SaTime.Now
        });
    }

    public Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SourceProbeResult.Success(1, 200, 1));
}
