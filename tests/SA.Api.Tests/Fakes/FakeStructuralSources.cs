using SA.Application.Abstractions;
using SA.Domain.Entities.Capital;
using SA.Domain.Entities.Equity;

namespace SA.Api.Tests.Fakes;

/// <summary>
/// 股权结构源的测试替身：返回确定性样本，使总览页在不打真实上游的情况下保持可测。
/// </summary>
internal sealed class FakeEquitySource : IEquitySource
{
    public string Name => "测试源 · 股权结构";

    public string Domains => "股权";

    public Task<IReadOnlyList<TopHolder>> GetTopHoldersAsync(
        string code,
        int periods = 2,
        CancellationToken cancellationToken = default)
    {
        var endDate = new DateOnly(2026, 6, 30);
        var rows = Enumerable.Range(1, 10)
            .Select(rank => new TopHolder
            {
                Code = code,
                EndDate = endDate,
                Rank = rank,
                IsFreeFloat = false,
                HolderName = $"股东{rank}",
                HoldNum = 100_000_000m - rank * 1_000_000m,
                HoldRatio = 6m - rank * 0.4m,
                HoldChange = "不变",
                MarketCap = 30_000_000_000m,
                NoticeDate = endDate.AddDays(20),
                UpdatedAt = SA.Domain.Common.SaTime.Now
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<TopHolder>>(rows);
    }

    public Task<IReadOnlyList<HolderCount>> GetHolderCountsAsync(
        string code,
        int periods = 12,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<HolderCount>>(
        [
            new HolderCount
            {
                Code = code,
                EndDate = new DateOnly(2026, 3, 31),
                HolderNum = 120_000,
                HolderNumRatio = -4.2m,
                AvgHoldNum = 16_000m,
                AvgMarketCap = 5_000_000m,
                TotalShares = 4_400_000_000m,
                ReportName = "2026 1季末",
                UpdatedAt = SA.Domain.Common.SaTime.Now
            },
            new HolderCount
            {
                Code = code,
                EndDate = new DateOnly(2026, 6, 30),
                HolderNum = 110_000,
                PreviousHolderNum = 120_000,
                HolderNumChange = -10_000,
                HolderNumRatio = -8.3m,
                AvgHoldNum = 17_500m,
                AvgMarketCap = 6_400_000m,
                TotalShares = 4_400_000_000m,
                ReportName = "2026 2季末",
                UpdatedAt = SA.Domain.Common.SaTime.Now
            }
        ]);

    public Task<PledgeStat?> GetPledgeAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult<PledgeStat?>(new PledgeStat
        {
            Code = code,
            TradeDate = new DateOnly(2026, 9, 18),
            PledgeRatio = 0.57m,
            PledgeSharesWan = 2_515m,
            PledgeDealNum = 7,
            PledgeMarketCapWan = 759_404.25m,
            Industry = "电池",
            Year1ChangePercent = -18.38m,
            UpdatedAt = SA.Domain.Common.SaTime.Now
        });

    public Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SourceProbeResult.Success(1, 200, 10));
}

/// <summary>
/// 资金面源的测试替身：返回确定性样本。
/// </summary>
internal sealed class FakeCapitalSource : ICapitalSource
{
    public string Name => "测试源 · 资金面";

    public string Domains => "资金,筹码";

    public Task<IReadOnlyList<FundFlowDaily>> GetFundFlowAsync(
        string code,
        int days = 60,
        CancellationToken cancellationToken = default)
    {
        var start = new DateOnly(2026, 8, 20);
        var rows = Enumerable.Range(0, Math.Min(days, 20))
            .Select(index => new FundFlowDaily
            {
                Code = code,
                Date = start.AddDays(index),
                MainNet = index % 3 == 0 ? 150_000_000m : -80_000_000m,
                SuperLargeNet = 50_000_000m,
                LargeNet = 100_000_000m,
                MediumNet = -30_000_000m,
                SmallNet = -70_000_000m,
                MainRatio = 1.5m,
                Close = 300m + index,
                ChangePercent = 0.5m,
                UpdatedAt = SA.Domain.Common.SaTime.Now
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<FundFlowDaily>>(rows);
    }

    public Task<IReadOnlyList<BillboardRecord>> GetBillboardsAsync(
        string code,
        int limit = 20,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BillboardRecord>>(
        [
            new BillboardRecord
            {
                Code = code,
                TradeDate = new DateOnly(2026, 7, 7),
                Reason = "日涨幅偏离值达到 7% 的前五只证券",
                Explain = "4家机构买入",
                Close = 195.58m,
                ChangePercent = 10m,
                TurnoverRate = 2.07m,
                NetAmount = -148_902_744.65m,
                BuyAmount = 991_212_330.68m,
                SellAmount = 1_140_115_075.33m,
                Next1Change = 1.02m,
                Next5Change = 10.42m,
                UpdatedAt = SA.Domain.Common.SaTime.Now
            }
        ]);

    public Task<IReadOnlyList<BlockTrade>> GetBlockTradesAsync(
        string code,
        int limit = 20,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BlockTrade>>(
        [
            new BlockTrade
            {
                Code = code,
                TradeDate = new DateOnly(2026, 9, 15),
                DealPrice = 316.36m,
                PremiumRatio = 0m,
                DealVolume = 10_000m,
                DealAmount = 3_163_600m,
                BuyerName = "机构专用",
                SellerName = "机构专用",
                Close = 316.36m,
                UpdatedAt = SA.Domain.Common.SaTime.Now
            }
        ]);

    public Task<IReadOnlyList<MarginDetail>> GetMarginDetailsAsync(
        string code,
        int limit = 30,
        CancellationToken cancellationToken = default)
    {
        var start = new DateOnly(2026, 8, 20);
        var rows = Enumerable.Range(0, Math.Min(limit, 30))
            .Select(index => new MarginDetail
            {
                Code = code,
                Date = start.AddDays(index),
                FinanceBalance = 24_000_000_000m + index * 100_000_000m,
                FinanceBuy = 785_000_000m,
                FinanceNetBuy = 11_000_000m,
                LoanBalance = 252_000_000m,
                LoanVolume = 828_558m,
                TotalBalance = 24_250_000_000m,
                FinanceBalanceRatio = 1.89m,
                Close = 304.3m,
                ChangePercent = -0.39m,
                UpdatedAt = SA.Domain.Common.SaTime.Now
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<MarginDetail>>(rows);
    }

    public Task<IReadOnlyList<NorthboundHolding>> GetNorthboundAsync(
        string code,
        int limit = 8,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NorthboundHolding>>(
        [
            new NorthboundHolding
            {
                Code = code,
                HoldDate = new DateOnly(2026, 6, 30),
                DateType = "2026二季末",
                HoldShares = 894_158_187m,
                PreviousHoldShares = 761_315_474m,
                AddShares = 132_842_713m,
                AddSharesAmp = 14.91m,
                HoldMarketCap = 351_413_109_072.87m,
                OrgQuantity = 36,
                PreviousOrgQuantity = 40,
                FreeSharesRatio = 20.99m,
                TotalSharesRatio = 19.33m,
                Industry = "电源设备",
                UpdatedAt = SA.Domain.Common.SaTime.Now
            }
        ]);

    public Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SourceProbeResult.Success(1, 200, 20));
}
