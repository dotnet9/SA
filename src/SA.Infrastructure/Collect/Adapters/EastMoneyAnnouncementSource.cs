using System.Diagnostics;
using System.Text.Json;
using SA.Application.Abstractions;
using SA.Domain.Common;
using SA.Domain.Entities.Research;
using SA.Infrastructure.Collect.Http;

namespace SA.Infrastructure.Collect.Adapters;

/// <summary>
/// 东方财富公告列表（<c>np-anotice-stock.eastmoney.com/api/security/ann</c>）。
/// </summary>
/// <remarks>
/// <para>
/// 实测东芯股份 <c>total_hits</c> 为 892。字段：<c>art_code</c>（文章 Id）、<c>title</c>、
/// <c>notice_date</c>、<c>display_time</c>、<c>columns[].column_name</c>（公告类型，
/// 实测含「签订协议」「调研活动」「法律意见书」）、<c>codes[].ann_type</c>
/// （实测含 <c>A,KCB,SHA</c> 与 <c>INV</c>）、<c>source_type</c>。
/// </para>
/// <para>
/// <b>只取列表，不做正文解析</b>（实施计划 §1.3）：不做关键词抽取、不做情绪判断、
/// 不做事件自动归类。因此本适配器只映射标题 / 日期 / 类型 / 链接。
/// </para>
/// <para>
/// <b>原文链接由 <c>art_code</c> 拼出</b>：实测
/// <c>https://data.eastmoney.com/notices/detail/{code}/{art_code}.html</c> 返回 200。
/// </para>
/// <para>
/// 该端点在仓库里早已被 <c>ProbeCatalog</c> 验证过（当时以 <c>stock_list=300750</c> 探测），
/// 只是从未接进运行时采集；本适配器就是把它接线。
/// </para>
/// </remarks>
public sealed class EastMoneyAnnouncementSource(CollectHttpClient http) : IAnnouncementSource
{
    private const string BaseUrl = "https://np-anotice-stock.eastmoney.com/api/security/ann";

    /// <summary>单页上限（上游 page_size 上限未实测，60 足够覆盖「近期公告」的展示需求）。</summary>
    private const int PageSize = 60;

    /// <inheritdoc />
    public string Name => "东方财富 · 公告列表";

    /// <inheritdoc />
    public string Domains => "个股研究,事件";

    /// <inheritdoc />
    public async Task<IReadOnlyList<Announcement>> GetAsync(
        string code,
        int limit = 60,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 200);
        var pages = (int)Math.Ceiling((double)take / PageSize);
        var rows = new List<Announcement>(take);

        for (var page = 1; page <= pages; page++)
        {
            var url = $"{BaseUrl}?sr=-1&page_size={PageSize}&page_index={page}&ann_type=A" +
                      $"&client_source=web&stock_list={Uri.EscapeDataString(code)}&f_node=0&s_node=0";

            using var document = await http.GetJsonAsync(url, Name, cancellationToken: cancellationToken).ConfigureAwait(false);

            var pageRows = Parse(code, document.RootElement).ToList();
            if (pageRows.Count == 0)
            {
                break;
            }

            rows.AddRange(pageRows);
            if (rows.Count >= take || pageRows.Count < PageSize)
            {
                break;
            }
        }

        return rows.Count <= take ? rows : rows.GetRange(0, take);
    }

    /// <inheritdoc />
    public async Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var rows = await GetAsync("688110", 3, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return rows.Count > 0
                ? SourceProbeResult.Success(stopwatch.ElapsedMilliseconds, 200, rows.Count)
                : SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, 200, "公告列表为空");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return SourceProbeResult.Failure(stopwatch.ElapsedMilliseconds, (ex as CollectHttpException)?.StatusCode, ex.Message);
        }
    }

    /// <summary>解析公告列表（<c>data.list[]</c>）。</summary>
    internal static IEnumerable<Announcement> Parse(string code, JsonElement root)
    {
        if (!JsonValueReader.TryGet(root, "data", out var data)
            || data.ValueKind != JsonValueKind.Object
            || !JsonValueReader.TryGet(data, "list", out var list)
            || list.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var row in list.EnumerateArray())
        {
            var artCode = JsonValueReader.Text(row, "art_code");
            var title = JsonValueReader.Text(row, "title");
            if (string.IsNullOrWhiteSpace(artCode) || string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            // 公告日期缺失时退回展示时间（上游偶有 notice_date 为空）。
            // 注意 display_time 的毫秒用**冒号**分隔（"2026-09-17 19:01:36:224"），
            // 标准解析会失败，必须先归一化。
            var noticeDate = JsonValueReader.Date(row, "notice_date") ?? ParseDisplayTime(row);
            if (noticeDate is null)
            {
                continue;
            }

            var columns = ColumnNames(row);

            yield return new Announcement
            {
                ArtCode = artCode.Trim(),
                Code = code,
                Title = title.Trim(),
                NoticeDate = noticeDate.Value,
                ColumnName = columns.Count > 0 ? columns[0] : null,
                ColumnNames = columns.Count > 0 ? string.Join(',', columns) : null,
                AnnType = FirstAnnType(row),
                SourceType = JsonValueReader.Text(row, "source_type"),
                Url = $"https://data.eastmoney.com/notices/detail/{code}/{artCode.Trim()}.html",
                UpdatedAt = SaTime.Now
            };
        }
    }

    /// <summary>
    /// 解析 <c>display_time</c>。
    /// </summary>
    /// <remarks>
    /// 实测形态是 <c>"2026-09-17 19:01:36:224"</c>：<b>毫秒用冒号分隔</b>，
    /// 标准 <c>DateTime.TryParse</c> 会失败（返回 null）。
    /// 这里只取日期部分，因此把最后一个冒号换成点即可。
    /// </remarks>
    private static DateOnly? ParseDisplayTime(JsonElement row)
    {
        var text = JsonValueReader.Text(row, "display_time");
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalized = text.Trim();
        var lastColon = normalized.LastIndexOf(':');
        var firstColon = normalized.IndexOf(':');
        if (lastColon > 0 && lastColon != firstColon)
        {
            normalized = string.Concat(normalized.AsSpan(0, lastColon), ".", normalized.AsSpan(lastColon + 1));
        }

        return DateTime.TryParse(
            normalized,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var parsed)
                ? DateOnly.FromDateTime(parsed)
                : null;
    }

    /// <summary>取 <c>columns[].column_name</c>（一个公告可能有多个类型）。</summary>
    private static List<string> ColumnNames(JsonElement row)
    {
        var names = new List<string>();
        if (!JsonValueReader.TryGet(row, "columns", out var columns) || columns.ValueKind != JsonValueKind.Array)
        {
            return names;
        }

        foreach (var column in columns.EnumerateArray())
        {
            if (JsonValueReader.Text(column, "column_name") is { } name)
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>取 <c>codes[].ann_type</c>（实测形如 <c>A,KCB,SHA</c>）。</summary>
    private static string? FirstAnnType(JsonElement row)
    {
        if (!JsonValueReader.TryGet(row, "codes", out var codes) || codes.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var entry in codes.EnumerateArray())
        {
            if (JsonValueReader.Text(entry, "ann_type") is { } annType)
            {
                return annType;
            }
        }

        return null;
    }
}
