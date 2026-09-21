import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api';

/**
 * 规则引擎推算接口类型，对应后端 `SA.Contracts.Analysis`。
 */

/** 景气度打分的一个构成项。 */
export interface ProsperityFactor {
  name: string;
  value: string;
  score: number;
  weight: number;
  weighted: number;
  note: string;
}

export interface Prosperity {
  code: string;
  name: string;
  score: number;
  grade: string;
  factors: ProsperityFactor[];
  bandwidth: number | null;
  relativeStrength: number | null;
  memberCount: number;
  samples: number;
}

export interface ProsperityRank {
  items: Prosperity[];
  asOf: string | null;
  notes: string[];
}

/** 因果链的一环。 */
export interface CausalLink {
  order: number;
  stage: string;
  title: string;
  evidence: string;
  tone: string;
  confidence: number;
  confidenceNote: string;
}

export interface TransmissionBandwidth {
  industryCode: string | null;
  industryName: string | null;
  industryCorrelation: number | null;
  benchmarkCorrelation: number | null;
  beta: number | null;
  samples: number;
  bandwidth: number | null;
  note: string;
}

export interface CausalChain {
  code: string;
  name: string;
  industry: string | null;
  asOf: string | null;
  links: CausalLink[];
  bandwidth: TransmissionBandwidth;
  insights: string[];
  notes: string[];
}

/** 行业景气度排行。 */
export function fetchProsperity(take = 30): Promise<ProsperityRank> {
  return apiGet<ProsperityRank>('/api/industry/prosperity', { query: { take } });
}

/** 个股因果链与传导带宽。 */
export function fetchCausalChain(code: string): Promise<CausalChain> {
  return apiGet<CausalChain>(`/api/stocks/${encodeURIComponent(code)}/causal`);
}

/* ------------------------------------------------------------------
   选股器：策略、分布、导出记录
   ------------------------------------------------------------------ */

/** 一次筛选的执行记录（同时承担筛选日志与策略）。 */
export interface ScreenerRun {
  id: number;
  name: string | null;
  isStrategy: boolean;
  summary: string | null;
  total: number;
  presetKey: string | null;
  createdAt: string;
}

export interface ScreenerRunList {
  items: ScreenerRun[];
  strategies: number;
  quota: number;
}

export interface HistogramBin {
  from: number;
  to: number;
  count: number;
}

export interface ScreenerDistribution {
  field: string;
  fieldName: string;
  unit: string;
  count: number;
  min: number | null;
  p25: number | null;
  median: number | null;
  p75: number | null;
  max: number | null;
  bins: HistogramBin[];
}

export interface ExportLogRow {
  id: number;
  dataset: string;
  format: string;
  rows: number;
  createdAt: string;
}

/** 取筛选日志（历史）。 */
export function fetchScreenerHistory(): Promise<ScreenerRunList> {
  return apiGet<ScreenerRunList>('/api/screener/history');
}

/** 取我的策略。 */
export function fetchScreenerStrategies(): Promise<ScreenerRunList> {
  return apiGet<ScreenerRunList>('/api/screener/strategies');
}

/** 保存策略（把某次筛选存为策略，或直接用新条件创建）。 */
export function saveScreenerStrategy(name: string, runId?: number): Promise<ScreenerRun> {
  return apiPost<ScreenerRun>('/api/screener/strategies', { runId: runId ?? null, name, request: null });
}

/** 重命名策略（name 为空表示取消策略标记）。 */
export function renameScreenerStrategy(id: number, name: string | null): Promise<number> {
  return apiPut<number>(`/api/screener/strategies/${id}`, { name });
}

/** 删除记录（策略或日志）。 */
export function deleteScreenerRun(id: number): Promise<number> {
  return apiDelete<number>(`/api/screener/strategies/${id}`);
}

/** 回放策略（按记录中的条件重新筛选）。 */
export function replayScreenerRun(id: number): Promise<unknown> {
  return apiPost<unknown>(`/api/screener/strategies/${id}/replay`, {});
}

/** 分布统计。 */
export function fetchScreenerDistribution(
  field: string,
  request: unknown
): Promise<ScreenerDistribution> {
  return apiPost<ScreenerDistribution>('/api/screener/distribution', { field, request });
}

/** 导出记录。 */
export function fetchExportLogs(): Promise<ExportLogRow[]> {
  return apiGet<ExportLogRow[]>('/api/screener/exports');
}
