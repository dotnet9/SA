import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api';

/**
 * 自选接口类型，对应后端 `SA.Contracts.Watchlist`。
 */

/** 自选分组。 */
export interface WatchGroup {
  id: string;
  name: string;
  sortOrder: number;
  count: number;
}

/** 自选项（含行情）。 */
export interface WatchItem {
  code: string;
  name: string;
  groupId: string | null;
  sortOrder: number;
  note: string | null;
  addedAt: string;
  price: number | null;
  chg: number | null;
  pct: number | null;
  volume: number | null;
  amount: number | null;
  turnover: number | null;
  volRatio: number | null;
  industry: string | null;
  isSt: boolean;
  asOf: string | null;
}

/** 自选列表。 */
export interface Watchlist {
  groups: WatchGroup[];
  items: WatchItem[];
  asOf: string | null;
  /** 是否可编辑（无 watchlist.edit 时界面隐藏编辑入口）。 */
  canEdit: boolean;
  /** 自选数量上限（服务端侧的固定值）。 */
  quota: number;
}

/** 取自选列表。 */
export function fetchWatchlist(): Promise<Watchlist> {
  return apiGet<Watchlist>('/api/watchlist');
}

/** 批量加入自选。 */
export function addWatchItems(codes: string[], groupId?: string | null, note?: string): Promise<number> {
  return apiPost<number>('/api/watchlist/items', { codes, groupId: groupId ?? null, note: note ?? null });
}

/** 批量移除自选。 */
export function removeWatchItems(codes: string[]): Promise<number> {
  return apiDelete<number>('/api/watchlist/items', { codes });
}

/** 新建分组。 */
export function createWatchGroup(name: string): Promise<WatchGroup> {
  return apiPost<WatchGroup>('/api/watchlist/groups', { name });
}

/** 删除分组（组内股票移回未分组）。 */
export function removeWatchGroup(groupId: string): Promise<number> {
  return apiDelete<number>(`/api/watchlist/groups/${groupId}`);
}

/** 保存排序与分组。 */
export function reorderWatchItems(
  items: { code: string; groupId: string | null; note: string | null }[]
): Promise<number> {
  return apiPut<number>('/api/watchlist/order', { items });
}
