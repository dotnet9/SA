namespace SA.Api.Middleware;

/// <summary>
/// 为每个请求确定 traceId：优先复用入站 <c>X-Trace-Id</c>，否则新生成；
/// 写入 <see cref="HttpContext.Items"/> 与响应头，便于前端报错时对照日志（实施计划 §5.1）。
/// </summary>
public sealed class TraceIdMiddleware(RequestDelegate next)
{
    /// <summary>请求与响应使用的头名。</summary>
    public const string HeaderName = "X-Trace-Id";

    /// <summary>HttpContext.Items 中的键。</summary>
    public const string ItemsKey = "sa.traceId";

    private const int MaxLength = 32;

    /// <summary>
    /// 处理请求。
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = ResolveIncoming(context) ?? Guid.NewGuid().ToString("N")[..8];
        context.Items[ItemsKey] = traceId;
        context.TraceIdentifier = traceId;
        context.Response.Headers[HeaderName] = traceId;

        await next(context);
    }

    /// <summary>
    /// 读取入站 traceId 并做白名单校验，避免被日志注入（换行）或超长头污染。
    /// </summary>
    private static string? ResolveIncoming(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            return null;
        }

        var raw = values.ToString();
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > MaxLength)
        {
            return null;
        }

        foreach (var ch in raw)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch != '-' && ch != '_')
            {
                return null;
            }
        }

        return raw;
    }
}
