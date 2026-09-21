using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 新浪全市场证券列表（<c>Market_Center.getHQNodeData</c>），作为东财 <c>clist</c> 的备源。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么是备源而不是主源</b>：实测（2026-09-21）东财 <c>push2</c> 的 clist 被连接重置，
/// 但 <c>push2delay</c> 的同一端点可用<b>且带 <c>f100</c> 行业字段</b>；新浪列表虽然可用，
/// 却<b>完全没有行业字段</b>，而行业是股票池的必需元数据（搜索按行业、行业页、同业对比都依赖它）。
/// 因此东财（delay 主机）优先、新浪兜底——信息损失最小。
/// </para>
/// <para>
/// <b>实测事实</b>：
/// </para>
/// <list type="bullet">
/// <item><c>num</c> 参数被上游限制为 100/页（<c>num=6000</c> 仍只返回 100 行），
/// 全市场 5,564 只需约 56 次请求。</item>
/// <item>响应是<b>严格 JSON</b>（键有引号）——实施计划 §4.3 假设的「非严格 JSON」不成立，
/// 因此不需要容错解析。</item>
/// <item><c>node</c> 支持板块：<c>hs_a</c> 5,564（含北交所）、<c>kcb</c> 618、<c>cyb</c> 1,407、
/// <c>sh_a</c> 2,319、<c>sz_a</c> 2,901。</item>
/// <item><c>symbol=</c> 过滤参数<b>被忽略</b>（传任何值都返回第一页），因此只能翻页。</item>
/// </list>
/// <para>
/// <b>单位换算（与东财口径对齐）</b>：<c>volume</c> 上游为<b>股</b>（东财为手，故 ÷100）、
/// <c>amount</c> 为元、<c>mktcap</c>/<c>nmc</c> 为<b>万元</b>（故 ×10000）。
/// </para>
/// </remarks>
public sealed class SinaMarketListSource(
    CollectHttpClient http,
    ILogger<SinaMarketListSource> logger) : IMarketListSource
{
    private const string BaseUrl =
        "https://vip.stock.finance.sina.com.cn/quotes_service/api/json_v2.php/Market_Center.getHQNodeData";

    private const string CountUrl =
        "https://vip.stock.finance.sina.com.cn/quotes_service/api/json_v2.php/Market_Center.getHQNodeStockCount";

    /// <summary>实测上限：<c>num</c> 超过 100 也仍只返回 100 行。</summary>
    private const int PageSize = 100;

    /// <summary>安全上限：5,564 只 ÷ 100 ≈ 56 页，留出余量。</summary>
    private const int MaxPages = 90;

    /// <summary>全部 A 股节点（含北交所）。</summary>
    private const string UniverseNode = "hs_a";

    /// <inheritdoc />
    public string Name => "新浪财经 · 全市场列表（备源）";

    /// <inheritdoc />
    public string Domains => "行情,股票池,降级";

    /// <inheritdoc />
    public async Task<MarketListResult> GetMarketListAsync(
        Func<IReadOnlyList<MarketListRow>, Task>? onPage = null,
        CancellationToken cancellationToken = default)
    {
        var declared = await GetDeclaredCountAsync(cancellationToken).ConfigureAwait(false);
        var rows = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var asOf = SaTime.Today;

        for (var page = 1; page <= MaxPages; page++)
        {
            var url = $"{BaseUrl}?page={page}&num={PageSize}&sort=symbol&asc=1&node={UniverseNode}";
            var text = await http
                .GetStringAsync(url, Name, "https://finance.sina.com.cn", cancellationToken)
                .ConfigureAwait(false);

            var pageRows = ParsePage(text);
            if (pageRows.Count == 0)
            {
                break;
            }

            // 分页期间行情变化可能让行在页间漂移，按代码去重避免重复入库
            var fresh = new List<MarketListRow>(pageRows.Count);
            foreach (var row in pageRows)
            {
                if (seen.Add(row.Code))
                {
                    fresh.Add(row);
                }
            }

            rows += fresh.Count;
            if (fresh.Count > 0 && onPage is not null)
            {
                await onPage(fresh).ConfigureAwait(false);
            }

            if (pageRows.Count < PageSize)
            {
                break;
            }
        }

        logger.LogInformation("{Source} 扫描完成：{Rows} 行 / 上游声明 {Total} 行", Name, rows, declared);
        return new MarketListResult(rows, declared, asOf);
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var url = $"{BaseUrl}?page=1&num=5&sort=symbol&asc=1&node={UniverseNode}";
            var text = await http
                .GetStringAsync(url, Name, "https://finance.sina.com.cn", cancellationToken)
                .ConfigureAwait(false);
            var rows = ParsePage(text).Count;
            stopwatch.Stop();

            return rows > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, rows)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "返回 0 行");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }

    /// <summary>取上游声明的总数，仅用于日志与完整性校验。</summary>
    private async Task<int> GetDeclaredCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            var text = await http
                .GetStringAsync($"{CountUrl}?node={UniverseNode}", Name, "https://finance.sina.com.cn", cancellationToken)
                .ConfigureAwait(false);
            var trimmed = text.Trim().Trim('"');
            return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : 0;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // 总数只是辅助信息，拿不到不影响扫描
            logger.LogDebug(ex, "{Source} 未取到总数", Name);
            return 0;
        }
    }

    /// <summary>
    /// 解析一页（严格 JSON 数组）。
    /// </summary>
    /// <remarks>
    /// 停牌 / 退市标的的数值字段为 <c>null</c> 或 <c>"-"</c>，与东财一致按 0 处理，
    /// 但这类标的仍要进股票池（可以搜到、可以看到「无行情」状态）。
    /// </remarks>
    internal static List<MarketListRow> ParsePage(string text)
    {
        var rows = new List<MarketListRow>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return rows;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return rows;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return rows;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                var row = Parse(element);
                if (row is not null)
                {
                    rows.Add(row.Value);
                }
            }
        }

        return rows;
    }

    /// <summary>解析一行。</summary>
    internal static MarketListRow? Parse(JsonElement element)
    {
        var code = JsonValueReader.Text(element, "code");
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var name = JsonValueReader.TextOrEmpty(element, "name").Replace('\u3000', ' ').Trim();
        name = string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return new MarketListRow(
            Code: code,
            Name: name,
            Market: MarketCodes.MarketOf(code),
            // 该源不提供行业：留 null 而不是猜一个，界面按「无行业」显示
            Industry: null,
            Price: JsonValueReader.DecimalOrZero(element, "trade"),
            Change: JsonValueReader.DecimalOrZero(element, "pricechange"),
            Pct: JsonValueReader.DecimalOrZero(element, "changepercent"),
            // 上游为「股」，东财口径为「手」
            Volume: JsonValueReader.DecimalOrZero(element, "volume") / 100m,
            Amount: JsonValueReader.DecimalOrZero(element, "amount"),
            Turnover: JsonValueReader.DecimalOrZero(element, "turnoverratio"),
            // 该源不提供量比
            VolRatio: 0m,
            Pe: JsonValueReader.DecimalOrZero(element, "per"),
            // 该源只给一个 PE，没有 TTM 口径；留 0 由查询侧按缺失处理
            PeTtm: 0m,
            Pb: JsonValueReader.DecimalOrZero(element, "pb"),
            // 上游为「万元」
            MarketCap: JsonValueReader.DecimalOrZero(element, "mktcap") * 10_000m,
            FloatCap: JsonValueReader.DecimalOrZero(element, "nmc") * 10_000m,
            Open: JsonValueReader.DecimalOrZero(element, "open"),
            High: JsonValueReader.DecimalOrZero(element, "high"),
            Low: JsonValueReader.DecimalOrZero(element, "low"),
            PrevClose: JsonValueReader.DecimalOrZero(element, "settlement"));
    }
}
