using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.History;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 腾讯 K 线（<c>web.ifzq.gtimg.cn/appstock/app/fqkline/get</c>），日 / 周 / 月线，含指数。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么它是主源而不是备源</b>：实测（2026-09-21）东财 <c>push2his</c> 的 kline 端点在本机
/// 被连接重置，而<b>同一主机的</b> <c>stock/fflow/daykline</c> 端点正常返回 200。
/// 若仍让东财优先，每次 K 线请求都要先失败重试 3 次（1s+2s+4s 退避）才降级，
/// 日线回补（数千次请求）会被拖到不可用，日志也会被失败记录淹没。
/// </para>
/// <para>
/// <b>字段序（实测，与实施计划 §2.2 一致）</b>：每行只有 6 段
/// <c>[日期, 开, 收, 高, 低, 量]</c>——<b>没有成交额、没有换手率</b>。
/// 因此 <see cref="DailyBar.Amount"/> 与 <see cref="DailyBar.Turnover"/> 一律为 <c>null</c>，
/// 由界面标注「该源不提供」。参考实现写作 <c>parseFloat(item[6] || '0')</c> 会把成交额
/// 静默变成 0，本项目不采用。
/// </para>
/// <para>
/// <b>两个硬约束（实测）</b>：
/// </para>
/// <list type="number">
/// <item>单次最多约 640 根（<c>count=2000</c> 返回 642 根，<c>count=5000</c> 直接返回空），
/// 超出区间需按日期窗口分段拉取（见 <see cref="WindowDays"/>）。</item>
/// <item>日期区间参数是<b>生效</b>的，且 <c>count</c> 从区间<b>末端</b>截断——
/// <c>param=sz300750,day,2024-01-01,2024-03-31,10,qfq</c> 返回的是该季度最后 10 个交易日。</item>
/// </list>
/// <para>
/// 复权键名随口径变化：<c>qfq</c> → <c>qfqday</c>/<c>qfqweek</c>/<c>qfqmonth</c>，
/// <c>bfq</c> → <c>day</c>/<c>week</c>/<c>month</c>，<c>hfq</c> 同理。取错键会静默得到空数组。
/// </para>
/// </remarks>
public sealed class TencentKlineSource(CollectHttpClient http) : IKlineSource
{
    /// <summary>前复权（默认口径）。</summary>
    public const int AdjustFront = 1;

    /// <summary>不复权（用于除权除息检测）。</summary>
    public const int AdjustNone = 0;

    /// <summary>后复权。</summary>
    public const int AdjustBack = 2;

    /// <summary>日线。</summary>
    public const int PeriodDaily = 101;

    /// <summary>周线。</summary>
    public const int PeriodWeekly = 102;

    /// <summary>月线。</summary>
    public const int PeriodMonthly = 103;

    private const string BaseUrl = "https://web.ifzq.gtimg.cn/appstock/app/fqkline/get";

    /// <summary>单次请求的最大根数（实测上限约 640，留一点余量）。</summary>
    private const int MaxBarsPerRequest = 640;

    /// <summary>分段窗口的日历跨度：640 个交易日约 900 个自然日。</summary>
    private const int WindowDays = 900;

    /// <summary>分段次数上限，防止上游行为异常时无限翻页。</summary>
    private const int MaxChunks = 40;

    /// <inheritdoc />
    public string Name => "腾讯财经 · K 线";

    /// <inheritdoc />
    public string Domains => "行情,历史";

    /// <inheritdoc />
    public async Task<IReadOnlyList<DailyBar>> GetDailyAsync(
        string code,
        DateOnly from,
        DateOnly to,
        int adjust = AdjustFront,
        int period = PeriodDaily,
        CancellationToken cancellationToken = default)
    {
        var symbol = TencentSymbol(code)
            ?? throw new CollectHttpException($"{Name} 不支持该标的：{code}", null, null);

        return await FetchAsync(symbol, code, from, to, adjust, period, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DailyBar>> GetSectorDailyAsync(
        string sectorCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default) =>
        // 板块码（BKxxxx）腾讯不提供。抛异常而不是返回空，是为了让 SourceRegistry
        // 把它当作「该源不支持」并降级到东财——返回空会被当成「真实无数据」。
        throw new CollectHttpException($"{Name} 不提供板块指数日线：{sectorCode}", null, null);

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var to = SaTime.Today;
            var bars = await GetDailyAsync("300750", to.AddDays(-30), to, AdjustFront, PeriodDaily, cancellationToken)
                .ConfigureAwait(false);
            stopwatch.Stop();
            return bars.Count > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, bars.Count)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "近 30 天无日线");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 按日期窗口分段拉取，拼成完整区间（上游单次上限约 640 根）。
    /// </summary>
    private async Task<IReadOnlyList<DailyBar>> FetchAsync(
        string symbol,
        string code,
        DateOnly from,
        DateOnly to,
        int adjust,
        int period,
        CancellationToken cancellationToken)
    {
        var all = new List<DailyBar>();
        var cursorTo = to;

        for (var chunk = 0; chunk < MaxChunks && cursorTo >= from; chunk++)
        {
            var windowFrom = cursorTo.AddDays(-WindowDays);
            if (windowFrom < from)
            {
                windowFrom = from;
            }

            var bars = await FetchWindowAsync(symbol, code, windowFrom, cursorTo, adjust, period, cancellationToken)
                .ConfigureAwait(false);

            if (bars.Count == 0)
            {
                // 该窗口没有数据：可能是停牌、也可能是这只标的的历史还没这么长。
                // 继续往前一个窗口，而不是就此认定整段为空。
                cursorTo = windowFrom.AddDays(-1);
                continue;
            }

            all.AddRange(bars);

            var earliest = bars[0].Date;
            for (var i = 1; i < bars.Count; i++)
            {
                if (bars[i].Date < earliest)
                {
                    earliest = bars[i].Date;
                }
            }

            if (earliest <= from || windowFrom == from)
            {
                break;
            }

            cursorTo = earliest.AddDays(-1);
        }

        // 分段是「从新到旧」追加的，统一按日期升序返回（与东财源的顺序一致）
        all.Sort(static (a, b) => a.Date.CompareTo(b.Date));
        return all.Where(bar => bar.Date >= from && bar.Date <= to).ToList();
    }

    /// <summary>拉取单个日期窗口。</summary>
    private async Task<List<DailyBar>> FetchWindowAsync(
        string symbol,
        string code,
        DateOnly from,
        DateOnly to,
        int adjust,
        int period,
        CancellationToken cancellationToken)
    {
        var (periodName, adjustName, key) = Parameters(period, adjust, code);
        var url = $"{BaseUrl}?param={symbol},{periodName},{from:yyyy-MM-dd},{to:yyyy-MM-dd},{MaxBarsPerRequest},{adjustName}";

        using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

        var bars = new List<DailyBar>();
        if (!JsonValueReader.TryGet(document.RootElement, "data", out var data)
            || data.ValueKind != JsonValueKind.Object
            || !JsonValueReader.TryGet(data, symbol, out var node)
            || node.ValueKind != JsonValueKind.Object
            || !JsonValueReader.TryGet(node, key, out var rows)
            || rows.ValueKind != JsonValueKind.Array)
        {
            // 上游在参数越界或标的不存在时返回空 data，而不是错误码。按空处理：
            // 调用方（分段逻辑）会继续往前找，最终由「整段无数据」判定。
            return bars;
        }

        foreach (var element in rows.EnumerateArray())
        {
            var bar = ParseRow(element);
            if (bar is not null)
            {
                bars.Add(bar.Value);
            }
        }

        return bars;
    }

    /// <summary>
    /// 拼出 <c>param</c> 的三个可变部分：周期名、复权名、响应键名。
    /// </summary>
    /// <remarks>
    /// 复权名与键名必须成对变化：用 <c>qfq</c> 请求却去读 <c>day</c> 键会静默拿到空数组。
    /// </remarks>
    internal static (string PeriodName, string AdjustName, string Key) Parameters(int period, int adjust, string code)
    {
        var (periodName, suffix) = period switch
        {
            PeriodWeekly => ("week", "week"),
            PeriodMonthly => ("month", "month"),
            _ => ("day", "day")
        };

        var adjustName = adjust switch
        {
            AdjustNone => "bfq",
            AdjustBack => "hfq",
            _ => "qfq"
        };

        // 不复权的键没有前缀（day/week/month），前复权与后复权带各自前缀
        var key = adjustName == "bfq" ? suffix : adjustName + suffix;
        return (periodName, adjustName, key);
    }

    /// <summary>
    /// 拼腾讯的代码前缀：沪市（含登记指数）<c>sh</c>、北交所 <c>bj</c>、其余 <c>sz</c>。
    /// </summary>
    /// <remarks>
    /// 用 <see cref="MarketCodes.ResolveSecId"/> 而不是按代码首位推断：沪深300 的代码是 <c>000300</c>，
    /// 按首位会得到 <c>sz000300</c>（错），实际是 <c>sh000300</c>。
    /// 板块码返回 <c>90.</c> 前缀，腾讯不支持，返回 null 交由调用方处理。
    /// </remarks>
    internal static string? TencentSymbol(string code)
    {
        var secId = MarketCodes.ResolveSecId(code);
        var dot = secId.IndexOf('.');
        if (dot <= 0)
        {
            return null;
        }

        var market = secId[..dot];
        var symbol = secId[(dot + 1)..];

        return market switch
        {
            "1" => "sh" + symbol,
            "0" => (MarketCodes.BoardOf(symbol) == "北交所" ? "bj" : "sz") + symbol,
            _ => null
        };
    }

    /// <summary>
    /// 解析一行：<c>[日期, 开, 收, 高, 低, 量]</c>（第 2 段是收盘、第 3 段是最高，不是 OHLC 序）。
    /// </summary>
    /// <remarks>
    /// <b>成交额与换手率一律 null</b>：该源每行只有 6 段，没有这两项。
    /// 少于 6 段视为格式异常，跳过该行而不是补 0。
    /// </remarks>
    internal static DailyBar? ParseRow(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() < 6)
        {
            return null;
        }

        var parts = element.EnumerateArray().ToArray();

        if (parts[0].ValueKind != JsonValueKind.String
            || !DateOnly.TryParse(parts[0].GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return null;
        }

        return new DailyBar(
            Date: date,
            Open: Number(parts[1]),
            // 实测序：1=开、2=收、3=高、4=低
            Close: Number(parts[2]),
            High: Number(parts[3]),
            Low: Number(parts[4]),
            Volume: Number(parts[5]),
            Amount: null,
            Turnover: null,
            VolRatio: 0m,
            AdjFactor: 1m);
    }

    private static decimal Number(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.TryGetDecimal(out var number) ? number : 0m,
        JsonValueKind.String => decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m,
        _ => 0m
    };
}
