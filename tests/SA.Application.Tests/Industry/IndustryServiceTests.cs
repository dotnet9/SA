using SA.Application.Industry;
using SA.Application.Market;
using SA.Application.Tests.Market;
using SA.Domain.Entities.Market;

namespace SA.Application.Tests.Industry;

/// <summary>
/// 行业与同业对比的口径。这个模块全部由本地横截面计算得出，
/// 因此每个统计口径（中位数、分位、亏损股排除）都必须逐条钉住。
/// </summary>
public class IndustryServiceTests
{
    private const string AsOf = "2026-09-18";

    [Fact]
    public void 中位数按标准定义处理奇数与偶数样本()
    {
        Assert.Equal(3m, SA.Domain.Analysis.Indicators.Median([1m, 3m, 5m]));

        // 偶数样本取中间两个的平均
        Assert.Equal(3m, SA.Domain.Analysis.Indicators.Median([1m, 2m, 4m, 5m]));
        Assert.Null(SA.Domain.Analysis.Indicators.Median([]));
    }

    [Fact]
    public async Task 同业只包含同行业的标的且按市值倒序()
    {
        var service = Build(
            instruments:
            [
                Instrument("300750", "宁德时代", "电池"),
                Instrument("002594", "比亚迪", "电池"),
                Instrument("601012", "隆基绿能", "光伏设备"),
                Instrument("600519", "贵州茅台", "白酒")
            ],
            quotes:
            [
                Quote("300750", pct: 1.2m, cap: 1_400_000_000_000m, pe: 25m),
                Quote("002594", pct: -0.8m, cap: 900_000_000_000m, pe: 30m),
                Quote("601012", pct: 2.0m, cap: 200_000_000_000m, pe: 18m),
                Quote("600519", pct: 0.5m, cap: 1_900_000_000_000m, pe: 22m)
            ],
            sectors: [Sector("BK1033", "电池", pct: 1.5m)]);

        var result = await service.GetAsync("300750");

        Assert.True(result.Ok);
        var dto = result.Value!;

        Assert.Equal("电池", dto.Industry);

        // 同业只含「电池」两家，且按市值倒序（宁德 > 比亚迪）
        Assert.Equal(2, dto.Peers.Count);
        Assert.Equal("300750", dto.Peers[0].Code);
        Assert.True(dto.Peers[0].IsSelf);
        Assert.Equal("002594", dto.Peers[1].Code);
        Assert.DoesNotContain(dto.Peers, peer => peer.Code is "601012" or "600519");

        // 行业内排名：涨跌幅第 1、共 2 家
        Assert.Equal(1, dto.Position.PctRank);
        Assert.Equal(2, dto.Position.PctTotal);
    }

    [Fact]
    public async Task 亏损股不参与估值统计()
    {
        var service = Build(
            instruments:
            [
                Instrument("300750", "宁德时代", "电池"),
                Instrument("002594", "比亚迪", "电池"),
                Instrument("000001", "亏损股", "电池")
            ],
            quotes:
            [
                Quote("300750", pct: 1m, cap: 100m, pe: 20m),
                Quote("002594", pct: 1m, cap: 200m, pe: 30m),
                // PE 为负（亏损）：必须排除，否则会把中位数拉到 20 以下
                Quote("000001", pct: 1m, cap: 300m, pe: -50m)
            ],
            sectors: [Sector("BK1033", "电池", pct: 1m)]);

        var dto = (await service.GetAsync("300750")).Value!;

        // 中位数为 20 与 30 的平均 = 25，而不是包含 -50 后的 20
        Assert.Equal(25m, dto.Overview!.MedianPe);

        // 亏损股本身没有 PE 分位
        var loss = dto.Peers.Single(peer => peer.Code == "000001");
        Assert.Null(loss.PeTtm);
    }

    [Fact]
    public async Task 行业涨跌幅排名来自板块快照()
    {
        var service = Build(
            instruments: [Instrument("300750", "宁德时代", "电池")],
            quotes: [Quote("300750", pct: 1m, cap: 100m, pe: 20m)],
            sectors:
            [
                Sector("BK1", "白酒", pct: 3.0m),
                Sector("BK2", "电池", pct: 1.5m),
                Sector("BK3", "银行", pct: -0.5m)
            ]);

        var dto = (await service.GetAsync("300750")).Value!;

        Assert.Equal("电池", dto.Overview!.Name);
        Assert.Equal(2, dto.Overview.Rank);
        Assert.Equal(3, dto.Overview.TotalIndustries);

        // 行业排行按涨跌幅倒序
        Assert.Equal(["白酒", "电池", "银行"], dto.TopIndustries.Select(i => i.Name));
    }

    [Fact]
    public async Task 市值分位越大表示在行业内越大()
    {
        var service = Build(
            instruments:
            [
                Instrument("A", "小市值", "电池"),
                Instrument("B", "中市值", "电池"),
                Instrument("C", "大市值", "电池")
            ],
            quotes:
            [
                Quote("A", pct: 1m, cap: 100m, pe: 10m),
                Quote("B", pct: 1m, cap: 200m, pe: 20m),
                Quote("C", pct: 1m, cap: 300m, pe: 30m)
            ],
            sectors: [Sector("BK1", "电池", pct: 1m)]);

        var smallest = (await service.GetAsync("A")).Value!;
        var largest = (await service.GetAsync("C")).Value!;

        // 市值最大者的分位应当高于最小者
        Assert.True(largest.Position.CapPercentile > smallest.Position.CapPercentile);
        Assert.Equal(1, largest.Position.CapRank);
        Assert.Equal(3, smallest.Position.CapRank);

        // 市值第一时不写「前 0%」这类没有意义的说法
        Assert.Contains(largest.Insights, text => text.Contains("行业", StringComparison.Ordinal) && text.Contains("最大", StringComparison.Ordinal));
        Assert.DoesNotContain(largest.Insights, text => text.Contains("前 0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 相对行业中位给出强弱方向()
    {
        var service = Build(
            instruments:
            [
                Instrument("300750", "宁德时代", "电池"),
                Instrument("002594", "比亚迪", "电池"),
                Instrument("000001", "平盘股", "电池")
            ],
            quotes:
            [
                Quote("300750", pct: 5m, cap: 100m, pe: 20m),
                Quote("002594", pct: 1m, cap: 200m, pe: 20m),
                Quote("000001", pct: 0m, cap: 300m, pe: 20m)
            ],
            sectors: [Sector("BK1", "电池", pct: 2m)]);

        var dto = (await service.GetAsync("300750")).Value!;

        // 行业中位数为 1%，该股 5% → 强于中位 4 个百分点
        Assert.Equal(1m, dto.Overview!.MedianPct);
        Assert.Equal(4m, dto.Position.PctVsMedian);
        Assert.Contains(dto.Insights, text => text.Contains("强于行业中位数", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 行业缺失时仍返回同业为空而不报错()
    {
        var service = Build(
            instruments: [Instrument("300750", "宁德时代", industry: null)],
            quotes: [Quote("300750", pct: 1m, cap: 100m, pe: 20m)],
            sectors: [Sector("BK1", "电池", pct: 1m)]);

        var dto = (await service.GetAsync("300750")).Value!;

        Assert.Null(dto.Industry);
        Assert.Null(dto.Overview);
        Assert.Empty(dto.Peers);

        // 说明里要能解释「为什么没有同业数据」
        Assert.Contains(dto.Notes, note => note.Contains("东财行业", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 未知代码返回1002()
    {
        var service = Build(instruments: [], quotes: [], sectors: []);

        var result = await service.GetAsync("999999");

        Assert.False(result.Ok);
        Assert.Equal(SA.Contracts.Common.ErrorCode.NotFound, result.Error);
    }

    /* ------------------------------------------------------------------
       构造工具
       ------------------------------------------------------------------ */

    private static IndustryService Build(
        IReadOnlyList<Instrument> instruments,
        IReadOnlyList<QuoteSnapshot> quotes,
        IReadOnlyList<Sector> sectors)
    {
        var cache = new MarketSnapshotCache();
        cache.Replace(quotes, instruments.ToDictionary(item => item.Code, StringComparer.Ordinal));

        return new IndustryService(
            cache,
            new FakeSectorStore(sectors),
            new FakeInstrumentStore(instruments),
            new FakeQuoteStore(quotes));
    }

    private static Instrument Instrument(string code, string name, string? industry) =>
        new()
        {
            Code = code,
            Name = name,
            Market = SA.Domain.Common.MarketCodes.MarketOf(code),
            Board = SA.Domain.Common.MarketCodes.BoardOf(code),
            Industry = industry,
            IsSt = false,
            UpdatedOn = DateOnly.Parse(AsOf)
        };

    private static QuoteSnapshot Quote(string code, decimal pct, decimal cap, decimal pe) =>
        new()
        {
            Code = code,
            Price = 10m,
            Change = 0.1m,
            Pct = pct,
            Volume = 1000m,
            Amount = 1_000_000m,
            Turnover = 1m,
            VolRatio = 1m,
            Open = 10m,
            High = 10m,
            Low = 10m,
            PrevClose = 10m,
            MarketCap = cap,
            FloatCap = cap,
            Pe = pe,
            PeTtm = pe,
            Pb = 2m,
            AsOf = DateOnly.Parse(AsOf),
            UpdatedAt = DateTimeOffset.Now
        };

    private static Sector Sector(string code, string name, decimal pct) =>
        new()
        {
            Code = code,
            Name = name,
            Pct = pct,
            MainNet = 1_000_000_000m,
            UpCount = 10,
            DownCount = 5,
            LeaderName = "领涨股",
            LeaderCode = "000001",
            Pe = 20m,
            AsOf = DateOnly.Parse(AsOf),
            UpdatedAt = DateTimeOffset.Now
        };
}
