using SA.Application.Abstractions;
using SA.Application.Common;
using SA.Application.Market;
using SA.Contracts.Search;
using SA.Domain.Entities.Market;

namespace SA.Application.Search;

/// <summary>
/// 股票搜索。支持四种命中方式：代码（精确 / 前缀）、名称（精确 / 包含）、
/// 拼音首字母（前缀）、行业关键词（包含）——与需求规格 §5.2 一致。
/// </summary>
/// <remarks>
/// 排序按命中方式分档（代码精确 → 名称精确 → 代码前缀 → 拼音前缀 → 名称包含 → 行业包含），
/// 同档内按总市值降序。这样「300750」「宁德时代」「ndsd」「电池」都能得到符合直觉的第一条结果。
/// </remarks>
public sealed class SearchService(
    SearchIndexCache index,
    MarketSnapshotCache snapshot,
    IInstrumentStore instruments,
    IQuoteSnapshotStore quotes)
{
    /// <summary>命令面板提示条数。</summary>
    public const int SuggestSize = 8;

    /// <summary>分页上限（详细设计 §1.3）。</summary>
    private const int MaxPageSize = 200;

    /// <summary>
    /// 执行搜索。
    /// </summary>
    public async Task<ServiceResult<SearchResultDto>> SearchAsync(
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, MaxPageSize);
        var keyword = query.Q?.Trim() ?? string.Empty;

        await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

        if (keyword.Length == 0)
        {
            // 空查询不是错误：返回空结果集，界面展示「请输入代码 / 名称 / 拼音 / 行业」的空态
            return ServiceResult<SearchResultDto>.Success(
                new SearchResultDto(keyword, 0, page, pageSize, [], null));
        }

        var matches = Match(keyword, query.Board, query.Industry);
        var total = matches.Count;
        var rows = matches
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => MarketService.TrimSearchRow(ToRow(m)))
            .ToList();

        return ServiceResult<SearchResultDto>.Success(
            new SearchResultDto(keyword, total, page, pageSize, rows, null));
    }

    /// <summary>
    /// 命令面板提示。<paramref name="limit"/> 为返回条数上限。
    /// </summary>
    public async Task<ServiceResult<IReadOnlyList<SearchRowDto>>> SuggestAsync(
        string? query,
        int limit = SuggestSize,
        CancellationToken cancellationToken = default)
    {
        var keyword = query?.Trim() ?? string.Empty;
        if (keyword.Length == 0)
        {
            return ServiceResult<IReadOnlyList<SearchRowDto>>.Success([]);
        }

        await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

        var rows = Match(keyword, null, null)
            .Take(Math.Clamp(limit, 1, 50))
            .Select(m => MarketService.TrimSearchRow(ToRow(m)))
            .ToList();

        return ServiceResult<IReadOnlyList<SearchRowDto>>.Success(rows);
    }

    /// <summary>
    /// 按命中方式分档匹配。
    /// </summary>
    private List<(Instrument Instrument, string MatchedBy, int Rank, decimal Cap)> Match(
        string keyword,
        string? board,
        string? industry)
    {
        var result = new List<(Instrument, string, int, decimal)>();

        foreach (var instrument in index.Instruments)
        {
            if (!string.IsNullOrEmpty(board) && instrument.Board != board)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(industry) && instrument.Industry != industry)
            {
                continue;
            }

            var rank = Rank(instrument, keyword, out var matchedBy);
            if (rank < 0)
            {
                continue;
            }

            result.Add((instrument, matchedBy, rank, CapOf(instrument.Code)));
        }

        return result
            .OrderBy(x => x.Item3)
            .ThenByDescending(x => x.Item4)
            .ThenBy(x => x.Item1.Code, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 计算单条命中的档位；未命中返回 -1。
    /// </summary>
    private static int Rank(Instrument instrument, string keyword, out string matchedBy)
    {
        matchedBy = string.Empty;

        if (instrument.Code == keyword)
        {
            matchedBy = "code";
            return 0;
        }

        if (string.Equals(instrument.Name, keyword, StringComparison.OrdinalIgnoreCase))
        {
            matchedBy = "name";
            return 1;
        }

        if (instrument.Code.StartsWith(keyword, StringComparison.Ordinal))
        {
            matchedBy = "code";
            return 2;
        }

        if (SearchMatcher.PinyinMatches(instrument.Pinyin, keyword))
        {
            matchedBy = "pinyin";
            return 3;
        }

        if (SearchMatcher.NameMatches(instrument.Name, keyword))
        {
            matchedBy = "name";
            return 4;
        }

        if (SearchMatcher.IndustryMatches(instrument.Industry, keyword))
        {
            matchedBy = "industry";
            return 5;
        }

        return -1;
    }

    /// <summary>
    /// 转成结果行。价格等行情取内存快照；未采集到该标的行情时这些字段为 null，
    /// 界面按「无行情」呈现而不是显示 0（0 会被误读为真实价格）。
    /// </summary>
    private SearchRowDto ToRow((Instrument Instrument, string MatchedBy, int Rank, decimal Cap) match)
    {
        var instrument = match.Instrument;
        var quote = FindQuote(instrument.Code);

        return new SearchRowDto(
            Code: instrument.Code,
            Name: instrument.Name,
            Py: instrument.Pinyin,
            Board: instrument.Board,
            Industry: instrument.Industry,
            Price: quote?.Price,
            Chg: quote?.Change,
            Pct: quote?.Pct,
            VolRatio: quote?.VolRatio,
            Turnover: quote?.Turnover,
            Pe: quote is null || quote.Pe <= 0 ? null : quote.Pe,
            Pb: quote is null || quote.Pb <= 0 ? null : quote.Pb,
            Cap: quote is null || quote.MarketCap <= 0 ? null : MarketService.ToYi(quote.MarketCap),
            IsSt: instrument.IsSt,
            MatchedBy: match.MatchedBy);
    }

    private QuoteSnapshot? FindQuote(string code)
    {
        var current = snapshot.Current;
        return current.ByCode.TryGetValue(code, out var quote) ? quote : null;
    }

    private decimal CapOf(string code) => FindQuote(code)?.MarketCap ?? 0m;

    /// <summary>
    /// 装载搜索索引：进程启动后首次搜索时从库内读取，之后由采集任务整体替换。
    /// </summary>
    private async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        if (!index.IsEmpty)
        {
            return;
        }

        var rows = await instruments.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (rows.Count > 0)
        {
            index.Replace(rows);
        }

        // 快照可能比索引先就绪，这里顺带触发一次装载，保证首屏搜索能带上行情
        if (snapshot.IsEmpty)
        {
            var snapshotRows = await quotes.GetAllAsync(cancellationToken).ConfigureAwait(false);
            if (snapshotRows.Count > 0)
            {
                snapshot.Replace(snapshotRows, rows.ToDictionary(i => i.Code, StringComparer.Ordinal));
            }
        }
    }
}
