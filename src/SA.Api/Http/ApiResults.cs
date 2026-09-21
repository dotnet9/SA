using SA.Api.Middleware;
using SA.Application.Common;
using SA.Contracts.Common;

namespace SA.Api.Http;

/// <summary>
/// 统一响应包的写出入口：所有端点都必须经过这里，保证 code / message / data / traceId 四个字段齐全。
/// </summary>
public static class ApiResults
{
    /// <summary>
    /// 成功响应（HTTP 200）。
    /// </summary>
    public static IResult Ok<T>(HttpContext context, T data) =>
        Results.Json(ApiResponse.Ok(data, TraceId(context)));

    /// <summary>
    /// 失败响应，HTTP 状态码由错误码映射得到（见 docs/详细设计.md §1.2）。
    /// </summary>
    public static IResult Fail(HttpContext context, ErrorCode code, string message) =>
        Results.Json(
            ApiResponse.Fail<object>(code, message, TraceId(context)),
            statusCode: code.ToHttpStatus());

    /// <summary>
    /// 把用例结果直接写成响应：成功返回 <c>data</c>，失败按错误码映射 HTTP 状态码。
    /// </summary>
    public static IResult From<T>(HttpContext context, ServiceResult<T> result) =>
        result.Ok
            ? Ok(context, result.Value)
            : Fail(context, result.Error, result.Message ?? "请求失败");

    /// <summary>
    /// 直接写出失败响应。用于中间件与认证事件等拿不到 <c>IResult</c> 返回值的场合
    /// （它们必须自己写响应体）。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Fail"/> 共用同一套错误码到 HTTP 状态码的映射，
    /// 避免「中间件写的 401 与端点写的 401」出现两套格式。
    /// </remarks>
    public static async Task WriteFailAsync(
        HttpContext context,
        ErrorCode code,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (context.Response.HasStarted)
        {
            // 响应已开始写出（例如流式端点），此刻无法再改状态码与响应体
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = code.ToHttpStatus();
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = ApiResponse.Fail<object>(code, message, TraceId(context));
        await context.Response.WriteAsJsonAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private static string TraceId(HttpContext context) =>
        context.Items[TraceIdMiddleware.ItemsKey] as string ?? context.TraceIdentifier;
}
