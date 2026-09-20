import { useQuery } from '@tanstack/react-query';
import { fetchStockFreshness, fetchStockOverview, fetchStockProfile, fetchTrend } from './api';

/**
 * 个股数据。
 *
 * 缓存时长按详细设计 §12 分档：个股详情 1 分钟（行情快照 60 秒刷新一次）。
 * 1003（采集中）由 `lib/query.ts` 统一处理为 3 秒 × 5 次重试，
 * 与「首次打开个股页需要按需采集」的路径对应。
 */

/** 总览（8 张摘要卡）。 */
export function useStockOverview(code: string) {
  return useQuery({
    queryKey: ['stock', code, 'overview'],
    queryFn: () => fetchStockOverview(code),
    enabled: code.length > 0
  });
}

/** 行情条。 */
export function useStockProfile(code: string) {
  return useQuery({
    queryKey: ['stock', code, 'quote'],
    queryFn: () => fetchStockProfile(code),
    enabled: code.length > 0
  });
}

/** 趋势与价格结构。 */
export function useTrend(code: string, limit = 120) {
  return useQuery({
    queryKey: ['stock', code, 'trend', limit],
    queryFn: () => fetchTrend(code, limit),
    enabled: code.length > 0
  });
}

/** 数据新鲜度（日线 / 指标补到哪一天）。 */
export function useStockFreshness(code: string) {
  return useQuery({
    queryKey: ['stock', code, 'freshness'],
    queryFn: () => fetchStockFreshness(code),
    enabled: code.length > 0
  });
}
