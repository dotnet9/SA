import { QueryClient } from '@tanstack/react-query';
import { ApiError, ErrorCode } from './errors';

/** 数据未就绪时的重试间隔（毫秒），与实施计划 §5.1 的约定一致。 */
const DATA_NOT_READY_DELAY_MS = 3000;

/** 数据未就绪时的最大重试次数。 */
const DATA_NOT_READY_MAX_RETRY = 5;

/**
 * 是否重试。
 *
 * 只有「采集中」（1003）才自动重试：这类失败会随时间自愈。
 * 权限（2002/2003）、参数（1001）、未登录（2001）重试多少次结果都一样，
 * 重试只会拖长用户等待，因此直接交给错误态处理。
 */
export function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.code === ErrorCode.DataNotReady) {
    return failureCount < DATA_NOT_READY_MAX_RETRY;
  }

  return false;
}

/** 重试间隔。 */
export function retryDelay(failureCount: number, error: unknown): number {
  if (error instanceof ApiError && error.code === ErrorCode.DataNotReady) {
    return DATA_NOT_READY_DELAY_MS;
  }

  return Math.min(1000 * 2 ** failureCount, 8000);
}

/**
 * 全局查询客户端。
 *
 * 缓存时长按详细设计 §12 的分档设置：市场概览类 60 秒（与后端采集周期一致），
 * 窗口聚焦不自动重取（盘中数据由采集周期驱动，避免每次切窗口都打接口）。
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: shouldRetry,
      retryDelay,
      staleTime: 60_000,
      gcTime: 5 * 60_000,
      refetchOnWindowFocus: false
    }
  }
});
