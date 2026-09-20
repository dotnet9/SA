import { useQuery } from '@tanstack/react-query';
import { fetchMarketOverview, fetchMarketStatus } from './api';

/**
 * 市场概览数据。
 *
 * 缓存与重试策略在 `lib/query.ts` 统一配置（1003 采集中会自动重试 5 次 × 3 秒）。
 * 市场数据由服务端采集周期驱动，因此不做窗口聚焦重取。
 */
export function useMarketOverview() {
  return useQuery({
    queryKey: ['market', 'overview'],
    queryFn: fetchMarketOverview
  });
}

/** 数据新鲜度与数据源状态。 */
export function useMarketStatus() {
  return useQuery({
    queryKey: ['market', 'status'],
    queryFn: fetchMarketStatus
  });
}
