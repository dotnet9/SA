import { ApiError } from './errors';

/**
 * 把任意异常转成可直接展示给用户的文案。
 *
 * 三处关键行为：
 * - 业务错误（{@link ApiError}）优先用后端返回的 message：它已经带了具体原因
 *   （例如「自选数量上限为 200 只，当前 198 只，本次新增 3 只」），比前端的通用文案有用得多；
 * - 附加错误码与 traceId，用户可以直接凭 traceId 对照日志排查；
 * - 非业务异常（网络中断、浏览器抛错）统一给一句可操作的提示，而不是把 `Error: ...` 直接展示。
 */
export function errorText(error: unknown): string {
  if (error instanceof ApiError) {
    const trace = error.traceId ? `（traceId ${error.traceId}）` : '';
    return `${error.message}${trace}`;
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return '操作失败，请稍后重试';
}
