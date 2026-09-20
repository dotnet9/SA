using Microsoft.EntityFrameworkCore;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Rating;

namespace SA.Infrastructure.Persistence.Stores;

/// <summary>
/// 机构评级存储。每只股票一行，重复采集即覆盖（研报会持续更新预测）。
/// </summary>
public sealed class RatingStore(SaDbContext db) : IRatingStore
{
    private readonly SaDbContext _db = db;

    /// <inheritdoc />
    public Task<RatingConsensus?> GetAsync(string code, CancellationToken cancellationToken = default) =>
        _db.RatingConsensuses.AsNoTracking().FirstOrDefaultAsync(row => row.Code == code, cancellationToken);

    /// <inheritdoc />
    public async Task<int> UpsertAsync(RatingConsensus consensus, CancellationToken cancellationToken = default)
    {
        var row = await _db.RatingConsensuses
            .FirstOrDefaultAsync(existing => existing.Code == consensus.Code, cancellationToken).ConfigureAwait(false);

        if (row is null)
        {
            await _db.RatingConsensuses.AddAsync(consensus, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            row.RatingOrgNum = consensus.RatingOrgNum;
            row.BuyNum = consensus.BuyNum;
            row.AddNum = consensus.AddNum;
            row.NeutralNum = consensus.NeutralNum;
            row.ReduceNum = consensus.ReduceNum;
            row.SaleNum = consensus.SaleNum;
            row.AimPriceMax = consensus.AimPriceMax;
            row.AimPriceMin = consensus.AimPriceMin;
            row.LongTermNum = consensus.LongTermNum;
            row.Year1 = consensus.Year1;
            row.Eps1 = consensus.Eps1;
            row.YearMark1 = consensus.YearMark1;
            row.Year2 = consensus.Year2;
            row.Eps2 = consensus.Eps2;
            row.YearMark2 = consensus.YearMark2;
            row.Year3 = consensus.Year3;
            row.Eps3 = consensus.Eps3;
            row.YearMark3 = consensus.YearMark3;
            row.Year4 = consensus.Year4;
            row.Eps4 = consensus.Eps4;
            row.YearMark4 = consensus.YearMark4;
            row.IndustryBoard = consensus.IndustryBoard;
            row.UpdatedAt = SaTime.Now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return 1;
    }
}
