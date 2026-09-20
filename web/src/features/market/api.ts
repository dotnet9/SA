import { apiGet } from '@/lib/api';

/**
 * 市场概览接口类型。字段与后端 `SA.Contracts.Market` 一一对应（camelCase）。
 * 金额统一为亿元；比率为百分数（`0.94` 表示 +0.94%），与详细设计 §1.3 一致。
 */

/** 指数卡片。 */
export interface IndexCard {
  code: string;
  name: string;
  price: number;
  chg: number;
  pct: number;
  amount: number;
  /** 近 40 日收盘序列；本批后端尚未接入指数日线，为空数组。 */
  spark: number[];
  asOf: string;
}

/** 市场宽度。 */
export interface MarketBreadth {
  up: number;
  down: number;
  flat: number;
  limitUp: number;
  limitDown: number;
  total: number;
  turnover: number;
  turnoverPct: number | null;
  /** 北向净流入（亿元）；本轮恒为 null，见 `northboundNote`。 */
  northbound: number | null;
  northbound5: number[];
  /** 北向数据的口径说明与降级原因，界面必须原样展示。 */
  northboundNote: string;
  marginBalance: number;
  marginChg: number | null;
  asOf: string;
  marginAsOf: string | null;
}

/** 资金分层（亿元）。 */
export interface FundFlowLayers {
  superLarge: number;
  large: number;
  medium: number;
  small: number;
  mainNet: number;
}

/** 两市资金与杠杆。 */
export interface MarketFundFlow {
  layers: FundFlowLayers;
  financeBalance: number;
  loanBalance: number;
  marginChg: number | null;
  asOf: string;
  marginAsOf: string | null;
}

/** 行业条目。 */
export interface Sector {
  code: string;
  name: string;
  pct: number;
  flow: number;
  leader: string | null;
  leaderCode: string | null;
  pe: number | null;
  pePct: number | null;
  upCount: number;
  downCount: number;
}

/** 榜单条目。 */
export interface RankingRow {
  code: string;
  name: string;
  price: number;
  pct: number;
  amount: number;
  industry: string | null;
  board: string;
  isSt: boolean;
  /** 新股 / 次新股（名称前缀 N / C）；涨跌幅榜已剔除，此处用于界面解释。 */
  isNew: boolean;
}

/** 榜单集合。 */
export interface MarketRankings {
  amount: RankingRow[];
  gainers: RankingRow[];
  losers: RankingRow[];
  asOf: string;
}

/** 单个数据源状态。 */
export interface DataSourceStatus {
  name: string;
  type: string | null;
  domains: string | null;
  status: 'ok' | 'warn' | 'err' | 'idle';
  lastOkAt: string | null;
  latencyMs: number | null;
  failCount: number;
  lastError: string | null;
}

/** 数据新鲜度与采集状态。 */
export interface MarketStatus {
  asOf: string | null;
  updatedAt: string | null;
  tradingDay: boolean;
  marketPhase: string;
  isReady: boolean;
  sources: DataSourceStatus[];
}

/** 市场概览聚合。 */
export interface MarketOverview {
  indices: IndexCard[];
  breadth: MarketBreadth;
  fundFlow: MarketFundFlow;
  industries: Sector[];
  rankings: MarketRankings;
  status: MarketStatus;
}

/** 取市场概览（首屏一次请求）。 */
export function fetchMarketOverview(): Promise<MarketOverview> {
  return apiGet<MarketOverview>('/api/market/overview');
}

/** 取数据新鲜度与数据源健康状态。 */
export function fetchMarketStatus(): Promise<MarketStatus> {
  return apiGet<MarketStatus>('/api/market/status');
}
