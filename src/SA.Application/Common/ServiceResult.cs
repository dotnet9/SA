using SA.Contracts.Common;

namespace SA.Application.Common;

/// <summary>
/// 用例结果。把业务错误码从服务层一路带到端点层，由端点统一转成响应包
/// （错误码语义见 docs/详细设计.md §1.2）。
/// </summary>
/// <typeparam name="T">成功时的数据类型。</typeparam>
public sealed record ServiceResult<T>(T? Value, ErrorCode Error, string? Message)
{
    /// <summary>是否成功。</summary>
    public bool Ok => Error == ErrorCode.Success;

    /// <summary>构造成功结果。</summary>
    public static ServiceResult<T> Success(T value) => new(value, ErrorCode.Success, null);

    /// <summary>构造失败结果。</summary>
    public static ServiceResult<T> Fail(ErrorCode code, string message) => new(default, code, message);
}
