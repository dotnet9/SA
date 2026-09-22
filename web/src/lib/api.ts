import { ApiError, defaultMessage, ErrorCode } from './errors';
import { cacheGet, cachePut } from './idb-cache';

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
  /**
   * 客户端缓存时长（毫秒）。
   *
   * 大于 0 时：命中的缓存**先**通过 {@link ApiRequestOptions.onCache} 回填，
   * 同时照常发起请求并用新结果更新缓存——即「先出缓存再更新」。
   * 请求失败时若已有缓存，不抛错（调用方已拿到可用数据）。
   *
   * 只对 GET 生效：写操作缓存没有意义。
   */
  cacheMs?: number;
  /** 缓存命中时的回调（在请求返回之前调用）。 */
  onCache?: (value: unknown, savedAt: number) => void;
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

function send(url: string, options: ApiRequestOptions): Promise<Response> {
  const { method = 'GET', body, headers, signal } = options;

  return fetch(url, {
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
 * - 本应用<b>没有登录</b>：不带任何凭证，也不再处理 401 换令牌。
 * - 传了 `cacheMs` 的 GET 走「先出缓存再更新」，并在请求失败时用缓存兜底。
 */
export async function apiRequest<T>(path: string, options: ApiRequestOptions = {}): Promise<T> {
  const url = buildUrl(path, options.query);
  const cacheable = (options.cacheMs ?? 0) > 0 && (options.method ?? 'GET') === 'GET';

  let cached: { value: T; savedAt: number } | null = null;
  if (cacheable) {
    const hit = await cacheGet<T>(url);
    if (hit) {
      cached = { value: hit.value, savedAt: hit.savedAt };
      options.onCache?.(hit.value, hit.savedAt);
    }
  }

  try {
    const response = await send(url, options);
    const envelope = await readEnvelope<T>(response);

    if (!response.ok || envelope.code !== ErrorCode.Success) {
      throw new ApiError(
        envelope.code,
        envelope.message || defaultMessage(envelope.code),
        envelope.traceId ?? '',
        response.status
      );
    }

    if (cacheable) {
      void cachePut(url, envelope.data, options.cacheMs!);
    }

    return envelope.data as T;
  } catch (error) {
    // 有缓存时不让请求失败变成白屏：调用方已经通过 onCache 拿到数据，
    // 这里返回缓存值而不是抛错，界面照常渲染（只是数据是上次的）。
    if (cached) {
      return cached.value;
    }

    throw error;
  }
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
