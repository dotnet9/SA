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

export interface ScreenerFieldGroup {
  key: string;
  name: string;
  fields: string[];
  /** 是否默认展开（其余折叠：一屏 30 个输入框会让用户无从下手）。 */
  defaultExpanded: boolean;
  note: string | null;
}

export interface ScreenerMeta {
  fields: ScreenerField[];
  presets: ScreenerPreset[];
  boards: string[];
  /** 今日剩余导出次数；-1 表示未配置上限（不限）。 */
  exportQuota: number;
  /** 单次导出的行数上限。 */
  exportRowLimit: number;
  /** 字段分组，界面据此折叠展示。 */
  fieldGroups: ScreenerFieldGroup[];
  /** 口径提示（由后端下发，不在前端硬编码）。 */
  caliberNotes: string[];
  /** 可用于连续性条件的字段。 */
  continuousFields: string[];
  /** 连续性条件可选年数。 */
  continuousYears: number[];
  /** 基本面口径报告期；未采集为 null。 */
  fundamentalAsOf: string | null;
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
  /** 是否有财报数据（最新一期）。 */
  hasFundamental: boolean;
  /** 已采集的连续年报期数。 */
  fundamentalYears: number;
  fundamentalAsOf: string | null;
  /** 是否金融业：其毛利率/流动比率/ROIC 等字段为空属行业口径不同，不是数据缺失。 */
  isFinancial: boolean;
  roe: number | null;
  roeDeducted: number | null;
  grossMargin: number | null;
  netMargin: number | null;
  roic: number | null;
  debtRatio: number | null;
  currentRatio: number | null;
  quickRatio: number | null;
  interestDebtRatio: number | null;
  interestCoverageRatio: number | null;
  operatingCashFlowToRevenue: number | null;
  operatingCashFlowToNetProfit: number | null;
  freeCashFlow: number | null;
  inventoryTurnoverDays: number | null;
  receivableTurnoverDays: number | null;
  revenueYoy: number | null;
  netProfitYoy: number | null;
  deductedNetProfitYoy: number | null;
  eps: number | null;
  bps: number | null;
  dividendYield: number | null;
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

/** 连续性条件：「某字段连续 N 年落在区间内」。 */
export interface ScreenerContinuous {
  field: string;
  min: number | null;
  max: number | null;
  years: number;
}

export interface ScreenerCondition {
  ranges: { field: string; min: number | null; max: number | null }[];
  enums: { field: string; values: string[] }[];
  flags: { field: string; value: boolean }[];
  continuous: ScreenerContinuous[];
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
