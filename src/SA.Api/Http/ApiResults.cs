using SA.Api.Middleware;
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

    private static string TraceId(HttpContext context) =>
        context.Items[TraceIdMiddleware.ItemsKey] as string ?? context.TraceIdentifier;
}
