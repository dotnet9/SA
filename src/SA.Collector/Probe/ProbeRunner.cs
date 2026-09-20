using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SA.Collector.Probe;

/// <summary>
/// 单次探针的结果。字段名列表是报告的核心产物：后续各域适配器的解析单测据此固化，
/// 上游一旦改字段名即可被测试发现（实施计划 §13「上游字段漂移」）。
/// </summary>
internal sealed record ProbeResult(
    ProbeDefinition Definition,
    bool Ok,
    int? StatusCode,
    long ElapsedMs,
    long Bytes,
    string? Error,
    IReadOnlyList<string> FieldNames,
    IReadOnlyList<string> DetailLines);

/// <summary>
/// 探针执行器：以真实 HTTP 请求逐源验证连通性、字段名与样例值，用于实施首日实测与后续排障。
/// </summary>
internal sealed class ProbeRunner
{
    private const int MaxFieldNameCount = 60;
    private const int MaxSampleLength = 28;

    private static readonly string[] KlineLabels =
    [
        "日期", "开", "收", "高", "低", "成交量(手)", "成交额(元)", "振幅", "涨跌幅", "涨跌额", "换手"
    ];

    private readonly HttpClient _http;

    /// <summary>
    /// 构造执行器。<paramref name="timeoutSeconds"/> 为单请求超时。
    /// </summary>
    public ProbeRunner(int timeoutSeconds = 30)
    {
        // 腾讯与新浪源为 GBK（实施计划 §5.4）
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0 Safari/537.36");
    }

    /// <summary>
    /// 顺序执行探针。串行是刻意的：避免把上游限频行为掩盖掉。
    /// </summary>
    public async Task<IReadOnlyList<ProbeResult>> RunAsync(
        IReadOnlyList<ProbeDefinition> probes,
        Action<ProbeResult>? onCompleted = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ProbeResult>(probes.Count);
        foreach (var probe in probes)
        {
            var result = await RunOneAsync(probe, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            onCompleted?.Invoke(result);
        }

        return results;
    }

    private async Task<ProbeResult> RunOneAsync(ProbeDefinition probe, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, probe.Url);
            if (probe.Referer is not null)
            {
                request.Headers.Referrer = new Uri(probe.Referer);
            }

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var encoding = probe.Gbk ? Encoding.GetEncoding("GBK") : Encoding.UTF8;
            var text = encoding.GetString(bytes);

            var (fieldNames, detailLines) = Extract(probe.Shape, text);

            return new ProbeResult(
                probe,
                response.IsSuccessStatusCode,
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                bytes.Length,
                response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}",
                fieldNames,
                detailLines);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new ProbeResult(
                probe, false, null, stopwatch.ElapsedMilliseconds, 0, ex.Message, [], []);
        }
    }

    private static (IReadOnlyList<string> FieldNames, IReadOnlyList<string> DetailLines) Extract(
        ProbeShape shape,
        string text) => shape switch
    {
        ProbeShape.Kline => ExtractKline(text),
        ProbeShape.F10Sections => ExtractF10Sections(text),
        ProbeShape.TildeDelimited => ExtractTildeDelimited(text),
        ProbeShape.PlainText => ([], [Snippet(text, 220)]),
        _ => ExtractJson(text)
    };

    private static (IReadOnlyList<string>, IReadOnlyList<string>) ExtractJson(string text)
    {
        var details = new List<string>();
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            // push2 快照：data.diff[]
            if (TryGetProperty(root, "data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                if (TryGetProperty(data, "diff", out var diff) && diff.ValueKind == JsonValueKind.Array)
                {
                    return DescribeRows("data.diff", diff, ["f1", "f2", "f3", "f12", "f13", "f14", "f20", "f23", "f115"]);
                }

                if (TryGetProperty(data, "klines", out _))
                {
                    return ExtractKline(text);
                }

                // 公告：data.list[]
                if (TryGetProperty(data, "list", out var list) && list.ValueKind == JsonValueKind.Array)
                {
                    var described = DescribeRows("data.list", list, []);
                    details.Add($"total_hits={ReadRaw(data, "total_hits")}");
                    details.AddRange(described.Item2);
                    return (described.Item1, details);
                }

                // 快讯：data.fastNewsList[]
                if (TryGetProperty(data, "fastNewsList", out var fast) && fast.ValueKind == JsonValueKind.Array)
                {
                    var described = DescribeRows("data.fastNewsList", fast, []);
                    details.Add($"total={ReadRaw(data, "total")}");
                    details.AddRange(described.Item2);
                    return (described.Item1, details);
                }

                // 走到这里通常是上游返回错误体，逐字段打印标量值以便直接读出 message
                details.Add("data 为对象：");
                foreach (var property in data.EnumerateObject())
                {
                    details.Add($"  {property.Name} = {Snippet(RawValue(property.Value), 120)}");
                }

                return ([], details);
            }

            // datacenter / 公告：result.data[]
            if (TryGetProperty(root, "result", out var result) && TryGetProperty(result, "data", out var rows)
                && rows.ValueKind == JsonValueKind.Array)
            {
                var described = DescribeRows("result.data", rows, []);
                details.Add($"result.pages={ReadRaw(result, "pages")}  result.count={ReadRaw(result, "count")}");
                details.AddRange(described.Item2);
                return (described.Item1, details);
            }

            // 公告：data.list[]（data 为数组时不会走到上面的对象分支）
            if (TryGetProperty(root, "data", out var d2) && TryGetProperty(d2, "list", out var listRows)
                && listRows.ValueKind == JsonValueKind.Array)
            {
                return DescribeRows("data.list", listRows, []);
            }

            // 研报：{hits, data:[{...}]}
            if (TryGetProperty(root, "data", out var d3) && d3.ValueKind == JsonValueKind.Array)
            {
                var described = DescribeRows("data", d3, []);
                details.Add($"hits={ReadRaw(root, "hits")}");
                details.AddRange(described.Item2);
                return (described.Item1, details);
            }

            details.Add("顶层键：" + JoinKeys(root));
            if (text.Contains("\"code\":9501", StringComparison.Ordinal)
                || text.Contains("\"code\":9701", StringComparison.Ordinal))
            {
                details.Add("→ 报表不存在或已变更（需替换 reportName）");
            }

            return ([], details);
        }
        catch (JsonException ex)
        {
            details.Add($"非 JSON（{ex.Message}）");
            details.Add(Snippet(text, 220));
            return ([], details);
        }
    }

    private static (IReadOnlyList<string>, IReadOnlyList<string>) DescribeRows(
        string path,
        JsonElement array,
        IReadOnlyList<string> labelHints)
    {
        var details = new List<string>();
        var count = array.GetArrayLength();
        details.Add($"{path} 行数={count}");

        if (count == 0)
        {
            details.Add("→ 空数组：该过滤条件无数据");
            return ([], details);
        }

        var first = array[0];
        if (first.ValueKind != JsonValueKind.Object)
        {
            details.Add("首行：" + Snippet(first.ToString(), 220));
            return ([], details);
        }

        var names = first.EnumerateObject().Select(p => p.Name).ToList();
        details.Add($"字段数={names.Count}");

        if (names.Count > 0 && Array.Exists(names.ToArray(), n => labelHints.Contains(n)))
        {
            details.Add("已知字段口径：" + string.Join(", ", labelHints.Select(h =>
                names.Contains(h) ? $"{h}={ReadRaw(first, h)}" : $"{h}=(缺失)")));
        }

        var sample = string.Join(" | ", first.EnumerateObject()
            .Take(14)
            .Select(p => $"{p.Name}={Snippet(RawValue(p.Value), MaxSampleLength)}"));
        details.Add("首行：" + sample);

        return (names.Count > MaxFieldNameCount ? names.Take(MaxFieldNameCount).ToList() : names, details);
    }

    private static (IReadOnlyList<string>, IReadOnlyList<string>) ExtractKline(string text)
    {
        var details = new List<string>();
        try
        {
            using var document = JsonDocument.Parse(text);
            if (!TryGetProperty(document.RootElement, "data", out var data)
                || !TryGetProperty(data, "klines", out var klines)
                || klines.ValueKind != JsonValueKind.Array)
            {
                details.Add("未找到 data.klines");
                return ([], details);
            }

            var count = klines.GetArrayLength();
            details.Add($"data.klines 行数={count}（lmt 生效范围）");
            if (count == 0)
            {
                return ([], details);
            }

            var fieldNames = new List<string>();
            var first = klines[0].GetString() ?? string.Empty;
            var parts = first.Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                fieldNames.Add($"f5{i + 1}→{parts[i]}");
            }

            details.Add("字段序号对照（以首行为准）：");
            for (var i = 0; i < parts.Length; i++)
            {
                var label = i < KlineLabels.Length ? KlineLabels[i] : "未标注";
                details.Add($"  [{i}] {label} = {parts[i]}");
            }

            details.Add("末行：" + Snippet(klines[count - 1].GetString() ?? string.Empty, 160));
            return (fieldNames, details);
        }
        catch (JsonException ex)
        {
            details.Add($"非 JSON（{ex.Message}）：" + Snippet(text, 160));
            return ([], details);
        }
    }

    private static (IReadOnlyList<string>, IReadOnlyList<string>) ExtractF10Sections(string text)
    {
        var details = new List<string>();
        var sections = new List<string>();
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                details.Add("顶层非对象：" + Snippet(text, 160));
                return ([], details);
            }

            foreach (var section in root.EnumerateObject())
            {
                sections.Add(section.Name);
                if (section.Value.ValueKind == JsonValueKind.Array)
                {
                    var count = section.Value.GetArrayLength();
                    details.Add($"{section.Name}: 数组，行数={count}");
                    if (count > 0 && section.Value[0].ValueKind == JsonValueKind.Object)
                    {
                        var names = section.Value[0].EnumerateObject().Select(p => p.Name).ToList();
                        details.Add("  字段：" + string.Join(", ", names));
                        details.Add("  首行：" + string.Join(" | ", section.Value[0].EnumerateObject()
                            .Take(10)
                            .Select(p => $"{p.Name}={Snippet(RawValue(p.Value), MaxSampleLength)}")));
                    }
                }
                else if (section.Value.ValueKind == JsonValueKind.Object)
                {
                    details.Add($"{section.Name}: 对象，键={JoinKeys(section.Value)}");
                }
                else
                {
                    details.Add($"{section.Name}: {Snippet(RawValue(section.Value), 80)}");
                }
            }
        }
        catch (JsonException ex)
        {
            details.Add($"非 JSON（{ex.Message}）：" + Snippet(text, 160));
        }

        return (sections, details);
    }

    private static (IReadOnlyList<string>, IReadOnlyList<string>) ExtractTildeDelimited(string text)
    {
        var details = new List<string>();
        var fieldNames = new List<string>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var quoteStart = line.IndexOf('"');
            var quoteEnd = line.LastIndexOf('"');
            if (quoteStart < 0 || quoteEnd <= quoteStart)
            {
                continue;
            }

            var head = line[..quoteStart].TrimEnd('=', ' ');
            var parts = line[(quoteStart + 1)..quoteEnd].Split('~');
            details.Add($"{head} 字段数={parts.Length}");
            for (var i = 0; i < parts.Length && i < 14; i++)
            {
                details.Add($"  [{i}] {parts[i]}");
            }

            for (var i = 0; i < parts.Length; i++)
            {
                fieldNames.Add($"[{i}]={Snippet(parts[i], 20)}");
            }

            details.Add("---");
            if (details.Count > 40)
            {
                break;
            }
        }

        if (details.Count == 0)
        {
            details.Add("未识别到腾讯行情格式：" + Snippet(text, 160));
        }

        return (fieldNames, details);
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static string JoinKeys(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
            ? string.Join(", ", element.EnumerateObject().Select(p => p.Name).Take(20))
            : element.ValueKind.ToString();

    private static string ReadRaw(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value) ? RawValue(value) : "(无)";

    private static string RawValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Null => "null",
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => value.ToString()
    };

    private static string Snippet(string value, int max)
    {
        var flat = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return flat.Length <= max ? flat : flat[..max] + "…";
    }
}
