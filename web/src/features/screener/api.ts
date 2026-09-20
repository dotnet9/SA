import { apiGet, apiPost } from '@/lib/api';

/** 选股器接口类型，对应后端 `SA.Contracts.Screener`。 */

export interface ScreenerField {
  field: string;
  name: string;
  unit: string;
  min: number | null;
  max: number | null;
}

export interface ScreenerPreset {
  key: string;
  name: string;
  description: string;
}

export interface ScreenerMeta {
  fields: ScreenerField[];
  presets: ScreenerPreset[];
  boards: string[];
  /** 当日剩余导出次数；0 表示无导出权限或已用完。 */
  exportQuota: number;
}

export interface ScreenerRow {
  code: string;
  name: string;
  board: string;
  industry: string | null;
  price: number;
  pct: number;
  turnover: number;
  volRatio: number;
  amount: number;
  peTtm: number | null;
  pb: number | null;
  cap: number;
  floatCap: number;
  isSt: boolean;
}

export interface ScreenerResult {
  total: number;
  page: number;
  pageSize: number;
  rows: ScreenerRow[];
  asOf: string | null;
  /** 实际生效的条件说明。 */
  applied: string[];
  scopeNote: string | null;
}

export interface ScreenerCondition {
  ranges: { field: string; min: number | null; max: number | null }[];
  enums: { field: string; values: string[] }[];
  flags: { field: string; value: boolean }[];
  sortBy: string;
  sortDesc: boolean;
  page: number;
  pageSize: number;
  preset: string | null;
}

/** 取选股器元数据。 */
export function fetchScreenerMeta(): Promise<ScreenerMeta> {
  return apiGet<ScreenerMeta>('/api/screener/meta');
}

/** 执行筛选。 */
export function runScreener(condition: ScreenerCondition): Promise<ScreenerResult> {
  return apiPost<ScreenerResult>('/api/screener', condition);
}

/**
 * 导出结果为 CSV。
 *
 * 用 fetch 而不是 apiPost：导出返回的是文件流而不是 JSON 包，
 * 需要拿 blob 再触发下载，走统一的 JSON 解包反而会失败。
 */
export async function exportScreener(condition: ScreenerCondition): Promise<{ ok: boolean; message?: string }> {
  const response = await fetch('/api/screener/export', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(condition)
  });

  if (!response.ok) {
    // 配额与权限的错误是 JSON 包，取出里面的 message 展示
    try {
      const body = await response.json();
      return { ok: false, message: body?.message ?? `导出失败（HTTP ${response.status}）` };
    } catch {
      return { ok: false, message: `导出失败（HTTP ${response.status}）` };
    }
  }

  const blob = await response.blob();
  const disposition = response.headers.get('Content-Disposition') ?? '';
  const match = /filename="?([^";]+)"?/.exec(disposition);
  const fileName = match?.[1] ?? 'screener.csv';

  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);

  return { ok: true };
}
