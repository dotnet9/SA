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
  /** 置为 true 时不附带访问令牌（登录、刷新、健康检查）。 */
  anonymous?: boolean;
}

/* ------------------------------------------------------------------
   访问令牌与刷新协调
   访问令牌只放内存：刷新页面即失效，靠 HttpOnly Cookie 换新，
   避免 XSS 读到长期凭证（详细设计 §8「JWT + 刷新令牌」）。
   ------------------------------------------------------------------ */

let accessToken: string | null = null;
let refreshHandler: (() => Promise<string | null>) | null = null;

/** 设置当前访问令牌。 */
export function setAccessToken(token: string | null): void {
  accessToken = token;
}

/** 取当前访问令牌（SignalR 握手等场景需要）。 */
export function getAccessToken(): string | null {
  return accessToken;
}

/** 注册「令牌失效时如何换新」的回调，由 AuthProvider 注入。 */
export function setRefreshHandler(handler: (() => Promise<string | null>) | null): void {
  refreshHandler = handler;
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

function send(path: string, options: ApiRequestOptions): Promise<Response> {
  const { method = 'GET', body, query, headers, signal, anonymous } = options;

  return fetch(buildUrl(path, query), {
    method,
    credentials: 'same-origin',
    headers: {
      Accept: 'application/json',
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
      ...(!anonymous && accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      ...headers
    },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal
  });
}

async function readEnvelope<T>(response: Response): Promise<ApiEnvelope<T>> {
  try {
    return (await response.json()) as ApiEnvelope<T>;
  } catch {
    throw new ApiError(
      ErrorCode.Unexpected,
      `服务响应无法解析（HTTP ${response.status}）`,
      response.headers.get('X-Trace-Id') ?? '',
      response.status
    );
  }
}

/**
 * 调用后端接口并解包统一响应包。
 *
 * - 成功返回 `data`；失败抛 {@link ApiError}，保留业务错误码与 traceId。
 * - 遇到 2001（令牌失效）自动尝试刷新一次并重放请求；再失败则抛错，由上层跳登录页。
 */
export async function apiRequest<T>(path: string, options: ApiRequestOptions = {}): Promise<T> {
  let response = await send(path, options);
  let envelope = await readEnvelope<T>(response);

  const unauthorized = response.status === 401 || envelope.code === ErrorCode.Unauthenticated;
  if (unauthorized && !options.anonymous && refreshHandler) {
    const token = await refreshHandler();
    if (token) {
      response = await send(path, options);
      envelope = await readEnvelope<T>(response);
    }
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

/** POST 便捷方法。 */
export function apiPost<T>(path: string, body?: unknown, options: Omit<ApiRequestOptions, 'method' | 'body'> = {}) {
  return apiRequest<T>(path, { ...options, method: 'POST', body });
}

/** PUT 便捷方法。 */
export function apiPut<T>(path: string, body?: unknown, options: Omit<ApiRequestOptions, 'method' | 'body'> = {}) {
  return apiRequest<T>(path, { ...options, method: 'PUT', body });
}

/** DELETE 便捷方法（批量删除把参数放在请求体里，避免查询串过长）。 */
export function apiDelete<T>(path: string, body?: unknown, options: Omit<ApiRequestOptions, 'method' | 'body'> = {}) {
  return apiRequest<T>(path, { ...options, method: 'DELETE', body });
}
