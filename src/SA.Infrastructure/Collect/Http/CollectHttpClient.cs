using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;

namespace SA.Infrastructure.Collect.Http;

/// <summary>
/// 上游请求的统一出口：按域名限速 + 并发上限 + 域名熔断 + 指数退避重试 + UA 轮换。
/// </summary>
/// <remarks>
/// <para>
/// 所有适配器都必须经由此类发请求，避免各处自行 <c>new HttpClient</c> 造成
/// 连接池耗尽、限速失效或超时不可控（实施计划 §5.4）。
/// 退避策略见详细设计 §6.2：429/5xx 走 1s → 2s → 4s，最多 <see cref="CollectOptions.MaxRetry"/> 次。
/// </para>
/// <para>
/// <b>UA 按请求设置</b>而不是挂在 <c>DefaultRequestHeaders</c> 上：后者是进程级共享的，
/// 多线程同时改会互相覆盖，也无法做到「每个请求随机取一个」。
/// </para>
/// </remarks>
public sealed class CollectHttpClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly CollectOptions _options;
    private readonly ILogger<CollectHttpClient> _logger;
    private readonly HostRateLimiter _limiter;
    private readonly HostCircuitBreaker _breaker;

    /// <summary>UA 池；为空时退化为单元素池。</summary>
    private readonly string[] _userAgents;

    /// <summary>并发闸门（全局：限制本机同时出网的请求数）。</summary>
    private readonly SemaphoreSlim _concurrency;

    /// <summary>
    /// 构造客户端。
    /// </summary>
    public CollectHttpClient(
        CollectOptions options,
        ILogger<CollectHttpClient> logger,
        HostRateLimiter limiter,
        HostCircuitBreaker breaker)
    {
        _options = options;
        _logger = logger;
        _limiter = limiter;
        _breaker = breaker;
        _concurrency = new SemaphoreSlim(Math.Max(1, options.MaxConcurrency));

        _userAgents = options.UserAgents is { Length: > 0 }
            ? options.UserAgents
            : [options.FallbackUserAgent];

        // 腾讯与新浪源为 GBK（实施计划 §5.4）
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        _http = new HttpClient(new SocketsHttpHandler
        {
            // 上游（东财 push2）会主动断开空闲连接，复用到已关闭的连接会直接抛
            // HttpIOException（response ended prematurely）。把空闲与总存活时间都压短，
            // 让池子里的连接在变陈旧之前就被换掉；重试时拿到的就是新连接。
            PooledConnectionLifetime = TimeSpan.FromMinutes(1),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(15),
            MaxConnectionsPerServer = Math.Max(2, options.MaxConcurrency),
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(3, options.TimeoutSeconds))
        };

        _http.DefaultRequestHeaders.Accept.ParseAdd("*/*");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9");
    }

    /// <summary>
    /// 发起一次 GET 并解析 JSON。
    /// </summary>
    /// <param name="url">完整地址。</param>
    /// <param name="source">数据源名，用于日志与错误信息。</param>
    /// <param name="referer">Referer（新浪源必需）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>响应文本；上游返回空体时返回空字符串。</returns>
    /// <exception cref="CollectHttpException">重试耗尽后仍失败。</exception>
    public Task<string> GetStringAsync(
        string url,
        string source,
        string? referer = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            url,
            source,
            referer,
            async (response, ct) =>
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                if (bytes.Length == 0)
                {
                    return string.Empty;
                }

                // 绝大多数端点返回 UTF-8；GBK 由调用方显式传入 encoding
                return Decode(bytes, response.Content.Headers.ContentType?.CharSet);
            },
            cancellationToken);

    /// <summary>
    /// 发起一次 GET 并按指定代码页解码（腾讯 <c>qt.gtimg.cn</c> 为 GBK）。
    /// </summary>
    public Task<string> GetStringAsync(
        string url,
        string source,
        Encoding encoding,
        string? referer = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            url,
            source,
            referer,
            async (response, ct) =>
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                return bytes.Length == 0 ? string.Empty : encoding.GetString(bytes);
            },
            cancellationToken);

    /// <summary>
    /// 发起一次 GET 并返回已解析的 <see cref="JsonDocument"/>（由调用方负责释放）。
    /// </summary>
    public async Task<JsonDocument> GetJsonAsync(
        string url,
        string source,
        string? referer = null,
        CancellationToken cancellationToken = default)
    {
        var text = await GetStringAsync(url, source, referer, cancellationToken).ConfigureAwait(false);
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException ex)
        {
            // 上游返回 HTML 错误页是常见故障形态，直接把片段带进异常便于定位
            var snippet = text.Length <= 200 ? text : text[..200];
            throw new CollectHttpException($"{source} 返回非 JSON 响应：{ex.Message}｜片段：{snippet}", null, ex);
        }
    }

    /// <summary>
    /// 带限速、熔断与退避的实际发送逻辑。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 熔断判定放在重试循环<b>之外</b>：冷却期内一次请求都不发，直接抛给上层降级到备源。
    /// 失败计数按<b>请求</b>记（重试耗尽才算一次），否则阈值 3 与重试 3 次会让
    /// 「一个请求就把域名打死」。
    /// </para>
    /// <para>
    /// <b>并发闸门只罩住请求本身</b>，不罩退避等待：否则一个失败的请求会在退避的 1s+2s+4s 里
    /// 一直占着名额，4 个并发位被少数失败请求占满后，其余所有域名都被堵住
    /// （闸门是全局的，这是实测中会放大故障的形态）。
    /// </para>
    /// </remarks>
    private async Task<T> SendAsync<T>(
        string url,
        string source,
        string? referer,
        Func<HttpResponseMessage, CancellationToken, Task<T>> read,
        CancellationToken cancellationToken)
    {
        var host = ResolveHost(url);
        _breaker.EnsureAvailable(host);

        var attempts = Math.Max(1, _options.MaxRetry);
        Exception? last = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var waited = await _limiter.WaitAsync(host, cancellationToken).ConfigureAwait(false);

            // 全局并发闸门：限制本机同时出网的请求数。它在按域名限速之外，
            // 因为「同时开多少连接」是本机资源，与具体上游无关。
            await _concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                // UA 按请求轮换：DefaultRequestHeaders 是进程级共享的，多线程写会互相覆盖
                request.Headers.UserAgent.ParseAdd(_userAgents[Random.Shared.Next(_userAgents.Length)]);

                if (!string.IsNullOrEmpty(referer))
                {
                    request.Headers.Referrer = new Uri(referer);
                }

                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

                // 429 与 5xx 可重试；4xx 其余情况重试无意义
                if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                {
                    stopwatch.Stop();
                    last = new CollectHttpException(
                        $"{source} 返回 HTTP {(int)response.StatusCode}",
                        (int)response.StatusCode,
                        null);
                    _logger.LogWarning(
                        "{Source} HTTP {Status}，第 {Attempt}/{Total} 次尝试耗时 {Cost}ms（限速等待 {Wait}ms）",
                        source, (int)response.StatusCode, attempt, attempts, stopwatch.ElapsedMilliseconds, waited.TotalMilliseconds);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    stopwatch.Stop();
                    _breaker.RecordFailure(host);
                    throw new CollectHttpException(
                        $"{source} 返回 HTTP {(int)response.StatusCode}",
                        (int)response.StatusCode,
                        null);
                }

                var value = await read(response, cancellationToken).ConfigureAwait(false);
                _breaker.RecordSuccess(host);
                return value;
            }
            catch (HostBlockedException)
            {
                throw;
            }
            catch (CollectHttpException)
            {
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or OperationCanceledException)
            {
                // IOException 也要重试：上游截断响应时抛的是 HttpIOException
                // （"The response ended prematurely"），它不是 HttpRequestException
                stopwatch.Stop();
                last = ex;
                _logger.LogWarning(
                    ex, "{Source} 请求失败（第 {Attempt}/{Total} 次，耗时 {Cost}ms）", source, attempt, attempts, stopwatch.ElapsedMilliseconds);
            }
            finally
            {
                // continue / return / throw 都会走到这里，闸门不会泄漏
                _concurrency.Release();
            }

            // 退避在闸门之外：等待期间让出并发名额
            await BackoffAsync(attempt, cancellationToken).ConfigureAwait(false);
        }

        _breaker.RecordFailure(host);
        throw new CollectHttpException($"{source} 重试 {attempts} 次后仍失败：{last?.Message}", null, last);
    }

    /// <summary>
    /// 取 URL 的域名（熔断与限速的键）。URL 非法时退化为整串，避免抛异常打断采集。
    /// </summary>
    internal static string ResolveHost(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;

    /// <summary>
    /// 指数退避 + 抖动，避免多个任务在同一时刻齐步重试。
    /// </summary>
    private Task BackoffAsync(int attempt, CancellationToken cancellationToken)
    {
        var baseMs = Math.Max(1, _options.BackoffBaseMs);
        var delayMs = baseMs * (1 << (attempt - 1));
        var jitter = Random.Shared.Next(0, Math.Max(1, baseMs / 2));
        return Task.Delay(delayMs + jitter, cancellationToken);
    }

    /// <summary>
    /// 按响应声明的字符集解码，缺省 UTF-8。
    /// </summary>
    private static string Decode(byte[] bytes, string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8.GetString(bytes);
        }

        try
        {
            return Encoding.GetEncoding(charset.Trim('"', ' ')).GetString(bytes);
        }
        catch (ArgumentException)
        {
            // 未知字符集名：按 UTF-8 处理
            return Encoding.UTF8.GetString(bytes);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _http.Dispose();
        _concurrency.Dispose();
    }
}

/// <summary>
/// 采集侧的上游异常。<see cref="StatusCode"/> 为 null 表示并未拿到响应（网络层失败）。
/// </summary>
public sealed class CollectHttpException(string message, int? statusCode, Exception? inner)
    : Exception(message, inner)
{
    /// <summary>HTTP 状态码。</summary>
    public int? StatusCode { get; } = statusCode;
}

/// <summary>
/// 上游 JSON 数值读取工具。上游对缺失值会给 <c>"-"</c>、空串或 null，
/// 统一在此收敛，避免每个适配器各写一份 try/catch。
/// </summary>
public static class JsonValueReader
{
    /// <summary>读小数；缺失或非数值返回 null。</summary>
    public static decimal? Decimal(JsonElement parent, string name) =>
        TryGet(parent, name, out var value) ? Decimal(value) : null;

    /// <summary>读小数；缺失或非数值返回 0。</summary>
    public static decimal DecimalOrZero(JsonElement parent, string name) =>
        Decimal(parent, name) ?? 0m;

    /// <summary>读整数；缺失或非数值返回 null。</summary>
    public static int? Int(JsonElement parent, string name) =>
        TryGet(parent, name, out var value) ? Int(value) : null;

    /// <summary>读整数；缺失或非数值返回 0。</summary>
    public static int IntOrZero(JsonElement parent, string name) =>
        Int(parent, name) ?? 0;

    /// <summary>读字符串；缺失返回 null，空串与 <c>"-"</c> 视为缺失。</summary>
    public static string? Text(JsonElement parent, string name)
    {
        if (!TryGet(parent, name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) || text == "-" ? null : text;
    }

    /// <summary>读字符串；缺失返回空串。</summary>
    public static string TextOrEmpty(JsonElement parent, string name) => Text(parent, name) ?? string.Empty;

    /// <summary>读日期时间字符串（<c>yyyy-MM-dd HH:mm:ss</c> 或 <c>yyyy-MM-dd</c>）。</summary>
    public static DateTimeOffset? DateTime(JsonElement parent, string name)
    {
        var text = Text(parent, name);
        if (text is null)
        {
            return null;
        }

        return System.DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? new DateTimeOffset(parsed, TimeSpan.FromHours(8))
            : null;
    }

    /// <summary>读日期部分。</summary>
    public static DateOnly? Date(JsonElement parent, string name)
    {
        var value = DateTime(parent, name);
        return value is null ? null : DateOnly.FromDateTime(value.Value.DateTime);
    }

    /// <summary>
    /// 读「紧凑日期」（<c>yyyMMdd</c>）。
    /// </summary>
    /// <remarks>
    /// 上游对同一个语义字段的表示并不统一：涨跌停池的 <c>qdate</c> 是<b>数字</b>
    /// （<c>"qdate":20260918</c>），公告类接口的日期是字符串。
    /// 只按字符串读会把数字当成缺失（实测因此让涨停家数静默变成 0），所以两种都要认。
    /// </remarks>
    public static DateOnly? CompactDate(JsonElement parent, string name)
    {
        if (!TryGet(parent, name, out var value))
        {
            return null;
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.String => value.GetString(),
            _ => null
        };

        return !string.IsNullOrWhiteSpace(text)
            && DateOnly.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;
    }

    /// <summary>读小数元素本身。</summary>
    public static decimal? Decimal(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.TryGetDecimal(out var number) ? number : null,
        // 上游把缺失值写成字符串 "-" 或 ""
        JsonValueKind.String => decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null,
        _ => null
    };

    /// <summary>读整数元素本身。</summary>
    public static int? Int(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.TryGetInt32(out var number) ? number : null,
        JsonValueKind.String => int.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null,
        _ => null
    };

    /// <summary>按名取属性。</summary>
    public static bool TryGet(JsonElement parent, string name, out JsonElement value)
    {
        if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out value))
        {
            return true;
        }

        value = default;
        return false;
    }
}
