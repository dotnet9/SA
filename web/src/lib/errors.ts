/**
 * 业务错误码。与后端 SA.Contracts.Common.ErrorCode 一一对应，
 * 取值依据 docs/详细设计.md §1.2。
 */
export const ErrorCode = {
  Success: 0,
  /** 参数校验失败 */
  InvalidParameter: 1001,
  /** 资源不存在 */
  NotFound: 1002,
  /** 数据尚未就绪（采集中），应提示重试 */
  DataNotReady: 1003,
  /** 未登录或令牌失效 */
  Unauthenticated: 2001,
  /** 无该功能点权限 */
  NoPermission: 2002,
  /** 超出数据范围（仅自选） */
  OutOfDataScope: 2003,
  /** 登录失败次数过多，账号锁定 */
  AccountLocked: 2004,
  /** 数据源不可用 */
  SourceUnavailable: 3001,
  /** 超出配额 */
  QuotaExceeded: 3002,
  /** 导出权限未开启 */
  ExportNotAllowed: 4001,
  /** 未预期异常 */
  Unexpected: 9999
} as const;

export type ErrorCodeValue = (typeof ErrorCode)[keyof typeof ErrorCode];

const messages: Record<number, string> = {
  [ErrorCode.InvalidParameter]: '请求参数有误',
  [ErrorCode.NotFound]: '未找到该股票或数据',
  [ErrorCode.DataNotReady]: '数据正在采集中，请稍候',
  [ErrorCode.Unauthenticated]: '登录已失效，请重新登录',
  [ErrorCode.NoPermission]: '当前账号没有该功能权限',
  [ErrorCode.OutOfDataScope]: '当前账号仅可查询自选股范围内的数据',
  [ErrorCode.AccountLocked]: '账号已被锁定，请联系管理员',
  [ErrorCode.SourceUnavailable]: '数据源暂不可用，已保留上次有效数据',
  [ErrorCode.QuotaExceeded]: '已达今日查询上限',
  [ErrorCode.ExportNotAllowed]: '当前账号未开启导出权限',
  [ErrorCode.Unexpected]: '服务异常，请稍后重试'
};

/** 取错误码对应的默认中文提示；后端 message 优先。 */
export function defaultMessage(code: number): string {
  return messages[code] ?? `未知错误（${code}）`;
}

/**
 * 接口调用失败。携带业务错误码与 traceId，便于在界面上直接给出可对照日志的报错信息。
 */
export class ApiError extends Error {
  readonly code: number;
  readonly traceId: string;
  readonly httpStatus: number;

  constructor(code: number, message: string, traceId: string, httpStatus: number) {
    super(message || defaultMessage(code));
    this.name = 'ApiError';
    this.code = code;
    this.traceId = traceId;
    this.httpStatus = httpStatus;
  }

  /** 数据尚未就绪：调用方应提示「采集中」并重试，而不是当作失败（实施计划 §5.1）。 */
  get isDataNotReady(): boolean {
    return this.code === ErrorCode.DataNotReady;
  }

  /** 未登录或令牌失效：调用方应触发重新登录流程。 */
  get isUnauthenticated(): boolean {
    return this.code === ErrorCode.Unauthenticated;
  }
}
