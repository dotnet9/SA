using System.Diagnostics;
using System.Globalization;
using System.Text;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 腾讯行情备源（<c>qt.gtimg.cn/q=sh600519,sz300750</c>，GBK 编码、<c>~</c> 分隔的定长索引）。
/// </summary>
/// <remarks>
/// <para>
/// <b>字段索引是本文件的核心资产</b>，全部在 2026-09-20 用真实响应逐位核对过（宁德时代 300750，
/// 2026-09-18 收盘：价 301.95 / 昨收 304.30 / 开 309.77 / 高 310.00 / 低 300.27 / 量 380713 手 /
/// 额 11537664647.91 元）；解析单测 <c>TencentQuoteSourceTests</c> 用同一份样本固化，
/// 上游一旦调整位次即会测试失败（实施计划 §3.5 第 6 项）。
/// </para>
/// <para>
/// 前缀规则实测：沪市 <c>sh</c>、深市 <c>sz</c>、北交所 <c>bj</c>；
/// 用 <c>sz</c> 请求北交所代码会得到空响应，因此不能用市场标志直接拼前缀。
/// </para>
/// </remarks>
public sealed class TencentQuoteSource(CollectHttpClient http) : IQuoteSnapshotSource
{
    /// <summary>单次请求代码数上限（URL 长度与上游容忍度折中）。</summary>
    private const int BatchSize = 60;

    private const string BaseUrl = "https://qt.gtimg.cn/q=";

    private const int IndexName = 1;
    private const int IndexCode = 2;
    private const int IndexPrice = 3;
    private const int IndexPrevClose = 4;
    private const int IndexOpen = 5;
    private const int IndexVolume = 6;
    private const int IndexTimestamp = 30;
    private const int IndexChange = 31;
    private const int IndexPct = 32;
    private const int IndexHigh = 33;
    private const int IndexLow = 34;
    private const int IndexAmountWan = 37;
    private const int IndexTurnover = 38;
    private const int IndexPeTtm = 39;
    private const int IndexFloatCapYi = 44;
    private const int IndexMarketCapYi = 45;
    private const int IndexPb = 46;
    private const int IndexVolRatio = 49;
    private const int IndexPe = 52;

    /// <summary>实际字段数（实测 88）；少于该长度说明上游结构变了，按失败处理。</summary>
    private const int MinFieldCount = 53;

    /// <inheritdoc />
    public string Name => "腾讯财经 · 行情快照（备源）";

    /// <inheritdoc />
    public string Domains => "行情,降级";

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuoteRow>> GetQuotesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken = default)
    {
        var wanted = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (wanted.Count == 0)
        {
            return [];
        }

        var gbk = Encoding.GetEncoding("GBK");
        var rows = new List<QuoteRow>(wanted.Count);

        foreach (var batch in wanted.Chunk(BatchSize))
        {
            var query = string.Join(',', batch.Select(TencentSymbol));
            // 无 Referer 时腾讯同样可访问，但带上更稳（与新浪源的做法保持一致）
            var text = await http.GetStringAsync(BaseUrl + query, Name, gbk, "https://gu.qq.com/", cancellationToken)
                .ConfigureAwait(false);

            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var row = Parse(line);
                if (row is not null)
                {
                    rows.Add(row.Value);
                }
            }
        }

        return rows;
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var rows = await GetQuotesAsync(["300750", "600519"], cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return rows.Count > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, rows.Count)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "未返回任何快照");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 拼腾讯的代码前缀：沪市 <c>sh</c>、北交所 <c>bj</c>、其余 <c>sz</c>。
    /// </summary>
    internal static string TencentSymbol(string code)
    {
        var trimmed = code.Trim();
        if (MarketCodes.BoardOf(trimmed) == "北交所")
        {
            return "bj" + trimmed;
        }

        return MarketCodes.MarketOf(trimmed) == 1 ? "sh" + trimmed : "sz" + trimmed;
    }

    /// <summary>
    /// 解析一行 <c>v_sz300750="..."</c>。
    /// </summary>
    internal static QuoteRow? Parse(string line)
    {
        var start = line.IndexOf('"');
        var end = line.LastIndexOf('"');
        if (start < 0 || end <= start)
        {
            return null;
        }

        var parts = line[(start + 1)..end].Split('~');
        if (parts.Length < MinFieldCount)
        {
            return null;
        }

        var code = Field(parts, IndexCode);
        var price = Number(parts, IndexPrice);
        if (string.IsNullOrWhiteSpace(code) || price is null or <= 0)
        {
            return null;
        }

        return new QuoteRow(
            Code: code,
            Name: Field(parts, IndexName).Trim(),
            Market: MarketCodes.MarketOf(code),
            Price: price.Value,
            Change: Number(parts, IndexChange) ?? 0m,
            Pct: Number(parts, IndexPct) ?? 0m,
            Volume: Number(parts, IndexVolume) ?? 0m,
            // 腾讯给的是「万元」，统一换算成元入库
            Amount: (Number(parts, IndexAmountWan) ?? 0m) * 10_000m,
            Turnover: Number(parts, IndexTurnover) ?? 0m,
            VolRatio: Number(parts, IndexVolRatio) ?? 0m,
            Open: Number(parts, IndexOpen) ?? 0m,
            High: Number(parts, IndexHigh) ?? 0m,
            Low: Number(parts, IndexLow) ?? 0m,
            PrevClose: Number(parts, IndexPrevClose) ?? 0m,
            // 腾讯给的是「亿元」，统一换算成元入库
            MarketCap: (Number(parts, IndexMarketCapYi) ?? 0m) * 100_000_000m,
            FloatCap: (Number(parts, IndexFloatCapYi) ?? 0m) * 100_000_000m,
            Pe: Number(parts, IndexPe) ?? 0m,
            PeTtm: Number(parts, IndexPeTtm) ?? 0m,
            Pb: Number(parts, IndexPb) ?? 0m,
            AsOf: ParseTimestamp(Field(parts, IndexTimestamp)));
    }

    /// <summary>解析 <c>yyyyMMddHHmmss</c> 时间戳。</summary>
    private static DateTimeOffset? ParseTimestamp(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !DateTime.TryParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return null;
        }

        return new DateTimeOffset(parsed, SaTime.Zone.GetUtcOffset(parsed));
    }

    private static string Field(string[] parts, int index) => index < parts.Length ? parts[index] : string.Empty;

    private static decimal? Number(string[] parts, int index)
    {
        var text = Field(parts, index);
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }
}
