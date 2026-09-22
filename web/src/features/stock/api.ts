import { apiGet } from '@/lib/api';

/**
 * 个股接口类型，对应后端 `SA.Contracts.Stock` 与 `SA.Contracts.Trend`。
 */

/** 行情条。 */
export interface StockProfile {
  code: string;
  name: string;
  py: string | null;
  board: string;
  industry: string | null;
  price: number | null;
  chg: number | null;
  pct: number | null;
  open: number | null;
  high: number | null;
  low: number | null;
  prevClose: number | null;
  volume: number | null;
  amount: number | null;
  turnover: number | null;
  volRatio: number | null;
  cap: number | null;
  floatCap: number | null;
  pe: number | null;
  peTtm: number | null;
  pb: number | null;
  isSt: boolean;
  asOf: string | null;
}

/** 模块标签。 */
export interface ModuleTag {
  text: string;
  tone: 'up' | 'down' | 'warn' | 'neutral';
}

/** 模块指标。 */
export interface ModuleKpi {
  label: string;
  value: string;
  tone: string;
}

/** 摘要卡。 */
export interface ModuleCard {
  key: string;
  name: string;
  /** ready：有真实数据；collecting：数据源未接入或正在采集。 */
  status: 'ready' | 'collecting' | 'failed';
  tags: ModuleTag[];
  kpis: ModuleKpi[];
  thumb: number[];
  summary: string | null;
  link: string;
}

/** 个股总览。 */
export interface StockOverview {
  profile: StockProfile;
  modules: ModuleCard[];
  summary: ModuleTag[];
  notes: string[];
}

/** K 线。 */
export interface Candle {
  d: string;
  o: number;
  h: number;
  l: number;
  c: number;
  v: number;
}

/** 趋势视图。 */
export interface Trend {
  code: string;
  name: string;
  period: string;
  adjust: string;
  asOf: string;
  candles: Candle[];
  ma: {
    ma5: (number | null)[];
    ma10: (number | null)[];
    ma20: (number | null)[];
    ma60: (number | null)[];
  };
  macd: {
    dif: (number | null)[];
    dea: (number | null)[];
    macd: (number | null)[];
  };
  kdj: {
    k: (number | null)[];
    d: (number | null)[];
    j: (number | null)[];
  };
  boll: {
    up: (number | null)[];
    mid: (number | null)[];
    low: (number | null)[];
  };
  relativeStrength: {
    benchmark: string;
    benchmarkCode: string;
    value: number | null;
    line: number[];
  };
  levels: {
    high250: number | null;
    low250: number | null;
    aboveMa20Pct: number | null;
    aboveMa250Pct: number | null;
    quantile3y: number | null;
    quantile250: number | null;
    samples: number;
  };
  insights: { label: string; text: string; tone: string }[];
  notes: string[];
}

/** 数据新鲜度。 */
export interface StockFreshness {
  code: string;
  asOf: string | null;
  dailyLastDate: string | null;
  indicatorLastDate: string | null;
  collecting: boolean;
}

/** 总览。 */
export function fetchStockOverview(code: string): Promise<StockOverview> {
  return apiGet<StockOverview>(`/api/stocks/${encodeURIComponent(code)}/overview`);
}

/** 行情条。 */
export function fetchStockProfile(code: string): Promise<StockProfile> {
  return apiGet<StockProfile>(`/api/stocks/${encodeURIComponent(code)}/quote`);
}

/** 趋势与价格结构。 */
export function fetchTrend(code: string, limit = 120): Promise<Trend> {
  return apiGet<Trend>(`/api/stocks/${encodeURIComponent(code)}/trend`, { query: { limit } });
}

/** 数据新鲜度。 */
export function fetchStockFreshness(code: string): Promise<StockFreshness> {
  return apiGet<StockFreshness>(`/api/stocks/${encodeURIComponent(code)}/freshness`);
}
