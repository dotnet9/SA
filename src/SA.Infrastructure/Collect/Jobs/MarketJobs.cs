using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Application.Market;
using SA.Application.Search;
using SA.Domain.Common;
using SA.Domain.Entities.Market;
using SA.Infrastructure.Collect.Adapters;
using SA.Infrastructure.Collect.Registry;

namespace SA.Infrastructure.Collect.Jobs;

/// <summary>
/// 股票池任务：拉取全市场证券列表，写入基础信息（含拼音首字母）并重建搜索索引。
/// </summary>
/// <remarks>
/// 每天一次，外加启动时一次。它不写行情快照——那是 <see cref="QuoteSnapshotJob"/> 的职责，
/// 两个任务职责分开后，查询侧可以独立判断「池子是否完整」与「行情是否新鲜」。
/// </remarks>
public sealed class UniverseJob(
    SourceRegistry registry,
    CollectExecutor executor,
    IInstrumentStore instruments,
    IPinyinIndexer pinyin,
    SearchIndexCache searchIndex,
    ILogger<UniverseJob> logger)
{
    /// <summary>任务名（写入 <c>CollectTaskLog</c>）。</summary>
    public const string TaskName = "universe";

    /// <summary>
    /// 执行一次。
    /// </summary>
    /// <returns>写入行数；失败返回 0。</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var collected = new List<Instrument>(6000);
        var today = SaTime.Today;

        var result = await executor.ExecuteAsync(
            TaskName,
            registry.MarketLists[0],
            "主源",
            async ct =>
            {
                var hit = await registry.GetMarketListWithFallbackAsync(
                    rows =>
                    {
                        collected.AddRange(rows.Select(row => ToInstrument(row, today)));
                        return Task.CompletedTask;
                    },
                    ct).ConfigureAwait(false);

                if (hit is null)
                {
                    return (0, 0);
                }

                logger.LogInformation("股票池扫描命中数据源：{Source}", hit.Value.Source);
                return (collected.Count, collected.Count);
            },
            cancellationToken).ConfigureAwait(false);

        if (result == 0 || collected.Count == 0)
        {
            return 0;
        }

        // 名称里可能出现拼音库无法覆盖的生僻字，此时拼音为空串，不影响其余标的
        var written = await instruments.UpsertAsync(collected, cancellationToken).ConfigureAwait(false);

        var all = await instruments.GetAllAsync(cancellationToken).ConfigureAwait(false);
        searchIndex.Replace(all);

        logger.LogInformation("股票池已更新：{Written} 行写入，索引 {Indexed} 只", written, all.Count);
        return written;
    }

    /// <summary>
    /// 列表行转基础信息。停牌标的的价格为 <c>"-"</c>，这类标的仍然要进股票池
    /// （可以搜到、可以看到「无行情」状态），因此不做过滤。
    /// </summary>
    private Instrument ToInstrument(MarketListRow row, DateOnly updatedOn) =>
        new()
        {
            Code = row.Code,
            Name = row.Name,
            Pinyin = pinyin.InitialsOf(row.Name),
            Market = row.Market,
            Board = MarketCodes.BoardOf(row.Code),
            Industry = row.Industry,
            IsSt = MarketCodes.IsSt(row.Name),
            UpdatedOn = updatedOn
        };
}

/// <summary>
/// 全市场快照任务：把一轮扫描结果整体写入行情快照表并替换内存快照。
/// </summary>
/// <remarks>
/// 频率由 <see cref="CollectOptions.FullScanIntervalSeconds"/> 控制：端点单页上限 100 行，
/// 一轮约 60 次请求，因此不可能按自选股的 3 秒节奏跑（见 <see cref="IMarketListSource"/> 的备注）。
/// </remarks>
public sealed class QuoteSnapshotJob(
    SourceRegistry registry,
    CollectExecutor executor,
    IQuoteSnapshotStore quotes,
    IInstrumentStore instruments,
    MarketBusinessDate businessDate,
    MarketSnapshotCache cache,
    ILogger<QuoteSnapshotJob> logger)
{
    /// <summary>任务名。</summary>
    public const string TaskName = "quote-snapshot-full";

    /// <summary>
    /// 执行一次。
    /// </summary>
    /// <returns>写入行数；失败返回 0。</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<QuoteSnapshot>(6000);
        var seen = new List<Instrument>(256);
        var now = SaTime.Now;
        var asOf = await businessDate.ResolveOrTodayAsync(TaskName, cancellationToken).ConfigureAwait(false);

        var result = await executor.ExecuteAsync(
            TaskName,
            registry.MarketLists[0],
            "主源",
            async ct =>
            {
                var hit = await registry.GetMarketListWithFallbackAsync(
                    page =>
                    {
                        foreach (var row in page)
                        {
                            // 停牌 / 退市的数值字段为 "-"，解析后价格为 0：不进入快照，
                            // 也就不参与涨跌家数与排行榜（这类标的由基础信息表承担）
                            if (row.Price > 0)
                            {
                                rows.Add(ToSnapshot(row, now, asOf));
                                seen.Add(new Instrument
                                {
                                    Code = row.Code,
                                    Name = row.Name,
                                    Market = row.Market,
                                    Board = MarketCodes.BoardOf(row.Code),
                                    Industry = row.Industry,
                                    IsSt = MarketCodes.IsSt(row.Name),
                                    UpdatedOn = asOf
                                });
                            }
                        }

                        return Task.CompletedTask;
                    },
                    ct).ConfigureAwait(false);

                if (hit is null)
                {
                    return (0, 0);
                }

                logger.LogInformation("全市场快照命中数据源：{Source}", hit.Value.Source);
                return (rows.Count, rows.Count);
            },
            cancellationToken).ConfigureAwait(false);

        if (result == 0 || rows.Count == 0)
        {
            return 0;
        }

        var written = await quotes.ReplaceAllAsync(rows, cancellationToken).ConfigureAwait(false);

        // 顺带补上新上市的标的：列表接口一次就带回了名称与行业，不必等到次日股票池任务。
        // UpsertAsync 只覆盖已有行的可变字段，因此这里不会把拼音清空。
        await instruments.UpsertAsync(seen, cancellationToken).ConfigureAwait(false);

        var allInstruments = await instruments.GetAllAsync(cancellationToken).ConfigureAwait(false);
        cache.Replace(rows, allInstruments.ToDictionary(i => i.Code, StringComparer.Ordinal));

        logger.LogInformation("全市场快照已更新：{Rows} 只，口径日 {AsOf:yyyy-MM-dd}", written, asOf);
        return written;
    }

    private static QuoteSnapshot ToSnapshot(MarketListRow row, DateTimeOffset updatedAt, DateOnly asOf) =>
        new()
        {
            Code = row.Code,
            Price = row.Price,
            Change = row.Change,
            Pct = row.Pct,
            Volume = row.Volume,
            Amount = row.Amount,
            Turnover = row.Turnover,
            VolRatio = row.VolRatio,
            Open = row.Open,
            High = row.High,
            Low = row.Low,
            PrevClose = row.PrevClose,
            MarketCap = row.MarketCap,
            FloatCap = row.FloatCap,
            Pe = row.Pe,
            PeTtm = row.PeTtm,
            Pb = row.Pb,
            AsOf = asOf,
            UpdatedAt = updatedAt
        };
}

/// <summary>
/// 指数快照任务。除点位外，它还提供全市场数据的业务日期（上游 <c>f124</c>）。
/// </summary>
public sealed class IndexJob(
    SourceRegistry registry,
    CollectExecutor executor,
    IIndexStore store,
    ILogger<IndexJob> logger)
{
    /// <summary>任务名。</summary>
    public const string TaskName = "index-quote";

    /// <summary>执行一次。</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = SaTime.Now;

        var result = await executor.ExecuteAsync(
            TaskName,
            registry.Indices,
            "主源",
            async ct =>
            {
                var rows = await registry.Indices.GetIndicesAsync(ct).ConfigureAwait(false);

                // 口径日取上游时间戳；全部缺失时退化为本机业务日（并留下日志）
                var quoteTimes = rows.Where(r => r.AsOf is not null).Select(r => r.AsOf!.Value).ToList();
                var asOf = quoteTimes.Count > 0
                    ? DateOnly.FromDateTime(SaTime.ToLocal(quoteTimes.Max()))
                    : DateOnly.FromDateTime(now.Date);

                var list = rows
                    .Select(row => new IndexQuote
                    {
                        Code = row.Code,
                        Name = row.Name,
                        Market = row.Market,
                        SortOrder = EastMoneyIndexSource.SortOrderOf(row.Code),
                        Displayed = EastMoneyIndexSource.IsDisplayed(row.Code),
                        Price = row.Price,
                        Change = row.Change,
                        Pct = row.Pct,
                        Volume = row.Volume,
                        Amount = row.Amount,
                        AsOf = asOf,
                        UpdatedAt = now
                    })
                    .ToList();

                await store.ReplaceAllAsync(list, ct).ConfigureAwait(false);
                return (list.Count, list.Count);
            },
            cancellationToken).ConfigureAwait(false);

        if (result > 0)
        {
            logger.LogInformation("指数快照已更新：{Rows} 条", result);
        }

        return result;
    }
}

/// <summary>
/// 行业板块任务（东财行业口径，约 500 个板块）。
/// </summary>
/// <remarks>
/// 板块列表同样不返回日期，因此口径日与全市场快照共用同一套解析（<see cref="MarketBusinessDate"/>），
/// 否则周末会把周五的板块涨跌标成周日的数据。
/// </remarks>
public sealed class SectorJob(
    SourceRegistry registry,
    CollectExecutor executor,
    ISectorStore store,
    MarketBusinessDate businessDate,
    ILogger<SectorJob> logger)
{
    /// <summary>任务名。</summary>
    public const string TaskName = "sector-list";

    /// <summary>执行一次。</summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = SaTime.Now;
        var asOf = await businessDate.ResolveOrTodayAsync(TaskName, cancellationToken).ConfigureAwait(false);

        var result = await executor.ExecuteAsync(
            TaskName,
            registry.Sectors,
            "主源",
            async ct =>
            {
                var rows = await registry.Sectors.GetSectorsAsync(ct).ConfigureAwait(false);
                var list = rows
                    .Select(row => new Sector
                    {
                        Code = row.Code,
                        Name = row.Name,
                        Pct = row.Pct,
                        MainNet = row.MainNet,
                        UpCount = row.UpCount,
                        DownCount = row.DownCount,
                        LeaderName = row.LeaderName,
                        LeaderCode = row.LeaderCode,
                        Pe = row.Pe ?? 0m,
                        AsOf = asOf,
                        UpdatedAt = now
                    })
                    .ToList();

                await store.ReplaceAllAsync(list, ct).ConfigureAwait(false);
                return (list.Count, list.Count);
            },
            cancellationToken).ConfigureAwait(false);

        if (result > 0)
        {
            logger.LogInformation("行业板块已更新：{Rows} 个", result);
        }

        return result;
    }
}
