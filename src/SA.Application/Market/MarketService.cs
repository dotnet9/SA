using SA.Application.Abstractions;
using SA.Application.Common;
using SA.Contracts.Common;
using SA.Contracts.Market;
using SA.Contracts.Search;
using SA.Domain.Common;
using SA.Domain.Entities.Collect;
using SA.Domain.Entities.Market;

namespace SA.Application.Market;

/// <summary>
/// 市场概览读模型。数据来源与口径：
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>指数卡：<see cref="IIndexStore"/>（东财指数快照），指数同时提供全市场数据的业务日期。</item>
/// <item>涨跌家数 / 榜单单 / 成交额：<see cref="MarketSnapshotCache"/> 里的全市场个股快照，内存聚合。</item>
/// <item>涨停跌停 / 资金分层 / 两融：<see cref="IMarketStatStore"/>（无法由个股快照推导，见 <see cref="MarketStat"/>）。</item>
/// <item>北向资金：<b>本轮为空态</b>，公开接口已不再提供逐日净买入，界面须展示口径说明。</item>
/// </list>
/// <para>
/// 数据未就绪时返回 <c>1003</c> 而不是空数据，让前端进入「采集中 + 重试」流程
/// （实施计划 §5.1：<c>1003</c> 是本轮的关键语义，不允许静默留空）。
/// </para>
/// </remarks>
public sealed class MarketService(
    MarketSnapshotCache cache,
    IQuoteSnapshotStore quotes,
    IInstrumentStore instruments,
    IIndexStore indexStore,
    ISectorStore sectorStore,
    IMarketStatStore stats,
    ICollectStatusStore collectStatus,
    ITradingCalendarStore calendar,
    CollectOptions options)
{
    /// <summary>榜单默认取前多少名（与原型的 6 行表格兼容，界面可要求更多）。</summary>
    public const int DefaultRankingSize = 8;

    /// <summary>北向资金的降级说明。<b>必须原样展示</b>，不允许把估算值渲染成披露值。</summary>
    private const string NorthboundNote =
        "北向资金逐日净买入自 2024 年起不再公开披露（东财原报表 RPT_MUTUAL_STOCK_NORTHSTA 已失效），"
        + "本轮不提供该数值；可用的陆股通持股数据将用于后续批次按持股变动估算，届时会标注「估算」。";

    /// <summary>
    /// 市场概览聚合：首屏所需数据一次返回，避免多次往返（概要设计 §3.1 的同款思路）。
    /// </summary>
    public async Task<ServiceResult<MarketOverviewDto>> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var status = await BuildStatusAsync(cancellationToken).ConfigureAwait(false);
        if (!status.Ok || status.Value is null)
        {
            return ServiceResult<MarketOverviewDto>.Fail(status.Error, status.Message ?? "市场数据不可用");
        }

        if (!status.Value.IsReady)
        {
            return ServiceResult<MarketOverviewDto>.Fail(ErrorCode.DataNotReady, "市场数据正在采集，请稍后重试");
        }

        var indices = await BuildIndicesAsync(cancellationToken).ConfigureAwait(false);
        var breadth = await BuildBreadthAsync(cancellationToken).ConfigureAwait(false);
        var fundFlow = await BuildFundFlowAsync(cancellationToken).ConfigureAwait(false);
        var sectors = await BuildSectorsAsync(cancellationToken).ConfigureAwait(false);
        var rankings = BuildRankings(DefaultRankingSize);

        return ServiceResult<MarketOverviewDto>.Success(
            new MarketOverviewDto(indices.Value!, breadth.Value!, fundFlow.Value!, sectors.Value!, rankings.Value!, status.Value));
    }

    /// <summary>指数卡片。</summary>
    public async Task<ServiceResult<IReadOnlyList<IndexCardDto>>> GetIndicesAsync(CancellationToken cancellationToken = default) =>
        await BuildIndicesAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>涨跌家数与两市资金。</summary>
    public async Task<ServiceResult<MarketBreadthDto>> GetBreadthAsync(CancellationToken cancellationToken = default) =>
        await BuildBreadthAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>行业热力与排行（东财行业口径）。</summary>
    public async Task<ServiceResult<IReadOnlyList<SectorDto>>> GetSectorsAsync(CancellationToken cancellationToken = default) =>
        await BuildSectorsAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>两市资金分层与两融杠杆。</summary>
    public async Task<ServiceResult<MarketFundFlowDto>> GetFundFlowAsync(CancellationToken cancellationToken = default) =>
        await BuildFundFlowAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>榜单：成交额 / 涨幅 / 跌幅。</summary>
    public async Task<ServiceResult<MarketRankingsDto>> GetRankingsAsync(
        int take = DefaultRankingSize,
        CancellationToken cancellationToken = default)
    {
        await EnsureSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return BuildRankings(Math.Clamp(take, 1, 100));
    }

    /// <summary>数据新鲜度与数据源健康状态。</summary>
    public Task<ServiceResult<MarketStatusDto>> GetStatusAsync(CancellationToken cancellationToken = default) =>
        BuildStatusAsync(cancellationToken);

    /// <summary>
    /// 指数卡。走势缩略图（<c>spark</c>）需要日线序列，指数日线在后续批次接入，
    /// 因此本轮返回空数组，由前端按「无缩略图」渲染而不是画一条假线。
    /// </summary>
    private async Task<ServiceResult<IReadOnlyList<IndexCardDto>>> BuildIndicesAsync(CancellationToken cancellationToken)
    {
        var rows = await indexStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var cards = rows
            .Where(r => r.Displayed)
            .OrderBy(r => r.SortOrder)
            .Select(r => new IndexCardDto(
                Code: r.Code,
                Name: r.Name,
                Price: Trim(r.Price),
                Chg: Trim(r.Change),
                Pct: Trim(r.Pct),
                Amount: ToYi(r.Amount),
                Spark: [],
                AsOf: SaTime.Format(r.AsOf)))
            .ToList();

        return ServiceResult<IReadOnlyList<IndexCardDto>>.Success(cards);
    }

    /// <summary>
    /// 市场宽度：涨跌家数按全市场快照统计（只计有行情的标的），
    /// 涨跌停、资金分层与两融取自市场统计表。
    /// </summary>
    private async Task<ServiceResult<MarketBreadthDto>> BuildBreadthAsync(CancellationToken cancellationToken)
    {
        await EnsureSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = cache.Current;

        var up = 0;
        var down = 0;
        var flat = 0;
        decimal turnover = 0;
        foreach (var row in snapshot.Rows)
        {
            if (row.Pct > 0)
            {
                up++;
            }
            else if (row.Pct < 0)
            {
                down++;
            }
            else
            {
                flat++;
            }

            turnover += row.Amount;
        }

        var stat = await stats.GetLatestAsync(cancellationToken).ConfigureAwait(false);

        // 成交额环比：需要上一交易日的两市成交额，本轮尚无历史序列，故为 null（界面显示「—」）
        var dto = new MarketBreadthDto(
            Up: up,
            Down: down,
            Flat: flat,
            LimitUp: stat?.LimitUp ?? 0,
            LimitDown: stat?.LimitDown ?? 0,
            Total: snapshot.Rows.Count,
            Turnover: ToYi(turnover),
            TurnoverPct: null,
            Northbound: null,
            Northbound5: [],
            NorthboundNote: NorthboundNote,
            MarginBalance: stat is null ? 0m : ToYi(stat.FinanceBalance),
            MarginChg: null,
            AsOf: snapshot.AsOf == DateOnly.MinValue ? string.Empty : SaTime.Format(snapshot.AsOf),
            MarginAsOf: stat?.MarginDate is null ? null : SaTime.Format(stat.MarginDate.Value));

        return ServiceResult<MarketBreadthDto>.Success(dto);
    }

    /// <summary>
    /// 两市资金分层与两融杠杆。分层净额来自大盘资金流（沪 + 深按层相加），
    /// 两融来自沪深合计；两者披露时间不同，各自带口径日。
    /// </summary>
    private async Task<ServiceResult<MarketFundFlowDto>> BuildFundFlowAsync(CancellationToken cancellationToken)
    {
        var stat = await stats.GetLatestAsync(cancellationToken).ConfigureAwait(false);

        var dto = new MarketFundFlowDto(
            Layers: new FundFlowLayersDto(
                SuperLarge: stat is null ? 0m : ToYi(stat.SuperLarge),
                Large: stat is null ? 0m : ToYi(stat.Large),
                Medium: stat is null ? 0m : ToYi(stat.Medium),
                Small: stat is null ? 0m : ToYi(stat.Small),
                MainNet: stat is null ? 0m : ToYi(stat.MainNet)),
            FinanceBalance: stat is null ? 0m : ToYi(stat.FinanceBalance),
            LoanBalance: stat is null ? 0m : ToYi(stat.LoanBalance),

            // 环比需要上一交易日的同类数值，本轮尚未保留历史序列，故为 null（界面显示「—」）
            MarginChg: null,
            AsOf: stat?.FundFlowDate is null ? string.Empty : SaTime.Format(stat.FundFlowDate.Value),
            MarginAsOf: stat?.MarginDate is null ? null : SaTime.Format(stat.MarginDate.Value));

        return ServiceResult<MarketFundFlowDto>.Success(dto);
    }

    /// <summary>行业热力与排行。数据来自板块端点，估值分位暂缺（无同口径历史样本）。</summary>
    private async Task<ServiceResult<IReadOnlyList<SectorDto>>> BuildSectorsAsync(CancellationToken cancellationToken)
    {
        var rows = await sectorStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var list = rows
            .Select(s => new SectorDto(
                Code: s.Code,
                Name: s.Name,
                Pct: Trim(s.Pct),
                Flow: ToYi(s.MainNet),
                Leader: s.LeaderName,
                LeaderCode: s.LeaderCode,
                Pe: s.Pe > 0 ? Trim(s.Pe) : null,
                PePct: null,
                UpCount: s.UpCount,
                DownCount: s.DownCount))
            .ToList();

        return ServiceResult<IReadOnlyList<SectorDto>>.Success(list);
    }

    /// <summary>
    /// 榜单。涨跌幅榜剔除 ST、退市风险与新股 / 次新股（原型卡片的说明文案即「剔除新股与 ST」）；
    /// 成交额榜不剔除，成交额是客观规模指标。
    /// </summary>
    private ServiceResult<MarketRankingsDto> BuildRankings(int take)
    {
        var snapshot = cache.Current;

        var annotated = snapshot.Rows
            .Select(row => (Row: row, Instrument: Lookup(snapshot, row.Code)))
            .Where(x => x.Row.Price > 0)
            .ToList();

        var amount = annotated
            .OrderByDescending(x => x.Row.Amount)
            .Take(take)
            .Select(ToRankingRow)
            .ToList();

        // 涨跌幅榜只看正常交易标的：ST 的 5% 限制与新股的不设限都会让榜单单向失真
        var tradable = annotated.Where(x => !IsExcludedFromChangeRanking(x.Instrument)).ToList();

        var gainers = tradable
            .OrderByDescending(x => x.Row.Pct)
            .Take(take)
            .Select(ToRankingRow)
            .ToList();

        var losers = tradable
            .OrderBy(x => x.Row.Pct)
            .Take(take)
            .Select(ToRankingRow)
            .ToList();

        return ServiceResult<MarketRankingsDto>.Success(
            new MarketRankingsDto(amount, gainers, losers, SaTime.Format(snapshot.AsOf)));
    }

    /// <summary>数据新鲜度与数据源状态。</summary>
    private async Task<ServiceResult<MarketStatusDto>> BuildStatusAsync(CancellationToken cancellationToken)
    {
        await EnsureSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = cache.Current;

        var sources = await collectStatus.GetSourcesAsync(cancellationToken).ConfigureAwait(false);
        var today = SaTime.Today;

        // 交易日判定：以交易日历为准；日历尚未生成时退化为「本机是工作日且已采到当日数据」
        var calendarRows = await calendar
            .GetRangeAsync(today.AddDays(-7), today, cancellationToken)
            .ConfigureAwait(false);

        bool isTradingDay;
        if (calendarRows.Count > 0)
        {
            isTradingDay = calendarRows.Any(d => d.Date == today && d.IsOpen);
        }
        else
        {
            isTradingDay = today.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
                && snapshot.AsOf == today;
        }

        var dataDate = snapshot.AsOf == DateOnly.MinValue ? (DateOnly?)null : snapshot.AsOf;
        var updatedAt = snapshot.UpdatedAt == DateTimeOffset.MinValue ? (DateTimeOffset?)null : snapshot.UpdatedAt;

        var dto = new MarketStatusDto(
            AsOf: dataDate is null ? null : SaTime.Format(dataDate.Value),
            UpdatedAt: updatedAt is null ? null : SaTime.Format(updatedAt.Value),
            TradingDay: isTradingDay,
            MarketPhase: MarketSession.Phase(isTradingDay, dataDate),
            IsReady: snapshot.Rows.Count > 0,
            Sources: sources.Select(ToStatusDto).ToList());

        return ServiceResult<MarketStatusDto>.Success(dto);
    }

    /// <summary>
    /// 确保内存快照可用：进程刚启动时先从库内装载一次，
    /// 之后由采集任务整体替换（避免每个请求都读 6,000 行）。
    /// </summary>
    private async Task EnsureSnapshotAsync(CancellationToken cancellationToken)
    {
        if (!cache.IsEmpty)
        {
            return;
        }

        var rows = await quotes.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return;
        }

        var instrumentRows = await instruments.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var map = instrumentRows.ToDictionary(i => i.Code, StringComparer.Ordinal);

        cache.Replace(rows, map);
    }

    private static Instrument? Lookup(MarketSnapshotCache.Snapshot snapshot, string code) =>
        snapshot.Instruments.TryGetValue(code, out var instrument) ? instrument : null;

    /// <summary>
    /// 是否从涨跌幅榜剔除。
    /// </summary>
    /// <remarks>
    /// 无基础信息的标的（理论上不会出现）不剔除，避免因缺信息而漏掉正常标的。
    /// </remarks>
    private static bool IsExcludedFromChangeRanking(Instrument? instrument) =>
        instrument is not null && (instrument.IsSt || MarketCodes.IsNewListing(instrument.Name));

    private static RankingRowDto ToRankingRow((QuoteSnapshot Row, Instrument? Instrument) item) =>
        new(
            Code: item.Row.Code,
            Name: item.Instrument?.Name ?? item.Row.Code,
            Price: Trim(item.Row.Price),
            Pct: Trim(item.Row.Pct),
            Amount: ToYi(item.Row.Amount),
            Industry: item.Instrument?.Industry,
            Board: item.Instrument?.Board ?? MarketCodes.BoardOf(item.Row.Code),
            IsSt: item.Instrument?.IsSt ?? false,
            IsNew: MarketCodes.IsNewListing(item.Instrument?.Name));

    /// <summary>
    /// 搜索结果行同样需要收敛精度（价格、涨跌幅、估值、市值都经过 REAL 往返）。
    /// </summary>
    internal static SearchRowDto TrimSearchRow(SearchRowDto row) =>
        row with
        {
            Price = row.Price is null ? null : Trim(row.Price.Value),
            Chg = row.Chg is null ? null : Trim(row.Chg.Value),
            Pct = row.Pct is null ? null : Trim(row.Pct.Value),
            VolRatio = row.VolRatio is null ? null : Trim(row.VolRatio.Value),
            Turnover = row.Turnover is null ? null : Trim(row.Turnover.Value),
            Pe = row.Pe is null ? null : Trim(row.Pe.Value),
            Pb = row.Pb is null ? null : Trim(row.Pb.Value),
            Cap = row.Cap is null ? null : Trim(row.Cap.Value)
        };

    private static DataSourceStatusDto ToStatusDto(DataSourceStatus source) =>
        new(
            Name: source.Name,
            Type: source.Type,
            Domains: source.Domains,
            Status: source.Status,
            LastOkAt: source.LastOkAt is null ? null : SaTime.Format(source.LastOkAt.Value),
            LatencyMs: source.LatencyMs,
            FailCount: source.FailCount,
            LastError: source.LastError);

    /// <summary>
    /// 元转亿元，保留 2 位小数（详细设计 §1.3：金额统一以亿元呈现）。
    /// </summary>
    internal static decimal ToYi(decimal yuan) => Trim(yuan / 100_000_000m);

    /// <summary>
    /// 收敛到接口约定的精度。
    /// </summary>
    /// <remarks>
    /// 报价类字段在 SQLite 里是 REAL（双精度），十进制小数往返后会带出
    /// <c>3911.8699999999998908606357872</c> 这类尾数。接口是面向展示的，
    /// 因此在组装 DTO 时统一收敛到 2 位小数，而不是把浮点尾数透给前端。
    /// 原始精度由上游本身决定（价 2 位、比率 2 位），收敛不会丢失有效信息。
    /// </remarks>
    internal static decimal Trim(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
