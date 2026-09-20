using SA.Contracts.Common;

namespace SA.Api.Middleware;

/// <summary>
/// 全局异常兜底：未预期异常一律转成 <c>9999</c> 响应包，并把 traceId 一并返回，
/// 便于用户报错时直接对照日志（实施计划 §5.1）。
/// </summary>
public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ExceptionMiddleware> _logger = logger;

    /// <summary>
    /// 处理请求。
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // 客户端主动断开，不是错误
        }
        catch (Exception ex)
        {
            var traceId = context.Items[TraceIdMiddleware.ItemsKey] as string ?? context.TraceIdentifier;
            _logger.LogError(ex, "未处理异常 traceId={TraceId} {Method} {Path}", traceId, context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                // 已经写出部分响应，无法再改写状态码
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = ErrorCode.Unexpected.ToHttpStatus();
            await context.Response.WriteAsJsonAsync(
                ApiResponse.Fail<object>(ErrorCode.Unexpected, "服务异常，请稍后重试", traceId));
        }
    }
}
