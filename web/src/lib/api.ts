import { ApiError, defaultMessage, ErrorCode } from './errors';

/**
 * 后端统一响应包，见 docs/详细设计.md §1.1。
 */
export interface ApiEnvelope<T> {
  code: number;
  message: string;
  data: T | null;
  traceId: string;
}

/**
 * 请求选项。
 */
export interface ApiRequestOptions {
  /** HTTP 方法，默认 GET。 */
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  /** 请求体，会被序列化为 JSON。 */
  body?: unknown;
  /** 附加查询参数，值为 undefined/null/'' 的项会被跳过。 */
  query?: Record<string, string | number | boolean | undefined | null>;
  /** 覆盖默认请求头。 */
  headers?: Record<string, string>;
  /** 外部取消信号。 */
  signal?: AbortSignal;
}

/** 构建带查询串的 URL。 */
function buildUrl(path: string, query?: ApiRequestOptions['query']): string {
  if (!query) return path;
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue;
    search.append(key, String(value));
  }
  const qs = search.toString();
  return qs ? `${path}${path.includes('?') ? '&' : '?'}${qs}` : path;
}

/**
 * 调用后端接口并解包统一响应包。
 *
 * 成功时直接返回 `data`；失败时抛出 {@link ApiError}，其中保留业务错误码与 traceId。
 * 业务错误码由 HTTP 状态码承载（见 ErrorCode.ToHttpStatus），因此非 2xx 也必须解析响应体。
 */
export async function apiRequest<T>(path: string, options: ApiRequestOptions = {}): Promise<T> {
  const { method = 'GET', body, query, headers, signal } = options;

  const response = await fetch(buildUrl(path, query), {
    method,
    credentials: 'same-origin',
    headers: {
      Accept: 'application/json',
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
      ...headers
    },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal
  });

  let envelope: ApiEnvelope<T> | null = null;
  try {
    envelope = (await response.json()) as ApiEnvelope<T>;
  } catch {
    // 非 JSON 响应（网关错误页、连接中断等）走统一兜底
    throw new ApiError(
      ErrorCode.Unexpected,
      `服务响应无法解析（HTTP ${response.status}）`,
      response.headers.get('X-Trace-Id') ?? '',
      response.status
    );
  }

  if (!response.ok || envelope.code !== ErrorCode.Success) {
    throw new ApiError(
      envelope.code,
      envelope.message || defaultMessage(envelope.code),
      envelope.traceId ?? '',
      response.status
    );
  }

  return envelope.data as T;
}

/** GET 便捷方法。 */
export function apiGet<T>(path: string, options: Omit<ApiRequestOptions, 'method' | 'body'> = {}) {
  return apiRequest<T>(path, { ...options, method: 'GET' });
}
