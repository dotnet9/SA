namespace SA.Contracts.Common;

/// <summary>
/// 统一响应包：{ code, message, data, traceId }，见 docs/详细设计.md §1.1。
/// </summary>
/// <typeparam name="T">业务数据类型。</typeparam>
public sealed record ApiResponse<T>(int Code, string Message, T? Data, string TraceId)
{
    /// <summary>
    /// 构造成功响应。
    /// </summary>
    public static ApiResponse<T> Ok(T data, string traceId) => new((int)ErrorCode.Success, "ok", data, traceId);

    /// <summary>
    /// 构造失败响应。
    /// </summary>
    public static ApiResponse<T> Fail(ErrorCode code, string message, string traceId) =>
        new((int)code, message, default, traceId);
}

/// <summary>
/// 非泛型构造入口，便于在端点中推断泛型参数。
/// </summary>
public static class ApiResponse
{
    /// <summary>
    /// 构造成功响应。
    /// </summary>
    public static ApiResponse<T> Ok<T>(T data, string traceId) => ApiResponse<T>.Ok(data, traceId);

    /// <summary>
    /// 构造失败响应。
    /// </summary>
    public static ApiResponse<T> Fail<T>(ErrorCode code, string message, string traceId) =>
        ApiResponse<T>.Fail(code, message, traceId);
}
