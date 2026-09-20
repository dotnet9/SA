namespace SA.Contracts.Common;

/// <summary>
/// 业务错误码。取值与 HTTP 映射以 docs/详细设计.md §1.2 为准。
/// </summary>
public enum ErrorCode
{
    /// <summary>成功。</summary>
    Success = 0,

    /// <summary>参数校验失败（HTTP 400）。</summary>
    InvalidParameter = 1001,

    /// <summary>资源不存在，如股票代码（HTTP 404）。</summary>
    NotFound = 1002,

    /// <summary>数据尚未就绪（采集中），前端应重试（HTTP 409）。</summary>
    DataNotReady = 1003,

    /// <summary>未登录或令牌失效（HTTP 401）。</summary>
    Unauthenticated = 2001,

    /// <summary>无该功能点权限（HTTP 403）。</summary>
    NoPermission = 2002,

    /// <summary>超出数据范围，仅自选（HTTP 403）。</summary>
    OutOfDataScope = 2003,

    /// <summary>登录失败次数过多，账号锁定（HTTP 423）。</summary>
    AccountLocked = 2004,

    /// <summary>数据源不可用（已降级）（HTTP 503）。</summary>
    SourceUnavailable = 3001,

    /// <summary>查询超出配额（HTTP 429）。</summary>
    QuotaExceeded = 3002,

    /// <summary>导出权限未开启（HTTP 403）。</summary>
    ExportNotAllowed = 4001,

    /// <summary>未预期异常（HTTP 500）。</summary>
    Unexpected = 9999
}

/// <summary>
/// 错误码到 HTTP 状态码的映射。
/// </summary>
public static class ErrorCodeExtensions
{
    /// <summary>
    /// 返回该错误码对应的 HTTP 状态码。
    /// </summary>
    public static int ToHttpStatus(this ErrorCode code) => code switch
    {
        ErrorCode.Success => 200,
        ErrorCode.InvalidParameter => 400,
        ErrorCode.NotFound => 404,
        ErrorCode.DataNotReady => 409,
        ErrorCode.Unauthenticated => 401,
        ErrorCode.NoPermission => 403,
        ErrorCode.OutOfDataScope => 403,
        ErrorCode.AccountLocked => 423,
        ErrorCode.SourceUnavailable => 503,
        ErrorCode.QuotaExceeded => 429,
        ErrorCode.ExportNotAllowed => 403,
        ErrorCode.Unexpected => 500,
        _ => 500
    };
}
