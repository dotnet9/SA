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

/** 全市场列表的一行（对应后端 `MarketStockRowDto`）。 */
export interface MarketStockRow {
  code: string;
  name: string;
  py: string | null;
  board: string;
  industry: string | null;
  /** 无行情（停牌 / 未采集）时为 null，界面显示「—」而不是 0。 */
  price: number | null;
  chg: number | null;
  pct: number | null;
  volRatio: number | null;
  turnover: number | null;
  pe: number | null;
  pb: number | null;
  cap: number | null;
  isSt: boolean;
}

/** 全市场列表响应。 */
export interface MarketStocks {
  total: number;
  page: number;
  pageSize: number;
  rows: MarketStockRow[];
  /** 可选板块，由后端下发（不在前端硬编码）。 */
  boards: string[];
  asOf: string | null;
  scopeNote: string | null;
}

/** 全市场列表参数。 */
export interface MarketStocksParams {
  board?: string;
  q?: string;
  sortBy?: string;
  desc?: boolean;
  page?: number;
  pageSize?: number;
}

/**
 * 取全市场列表（大盘概况页的主角）。
 *
 * 与 `/api/search` 分开：搜索的空查询返回空结果（搜索页依赖那个空态），
 * 而大盘页需要「不给关键词也列出全市场」。
 */
export function fetchMarketStocks(params: MarketStocksParams = {}): Promise<MarketStocks> {
  return apiGet<MarketStocks>('/api/market/stocks', {
    query: {
      board: params.board,
      q: params.q,
      sortBy: params.sortBy ?? 'cap',
      desc: params.desc ?? true,
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 60
    },
    // 行情每 3 秒刷新一次，缓存 30 秒：刷新页面先出上次的数据，再被新结果覆盖
    cacheMs: 30_000
  });
}

/** 多只标的的行情快照（自选股列表用）。 */
export interface MarketQuote {
  code: string;
  name: string;
  price: number | null;
  pct: number | null;
  chg: number | null;
  turnover: number | null;
  cap: number | null;
  industry: string | null;
  isSt: boolean;
}

/**
 * 按代码批量取行情。
 *
 * 自选股存在浏览器本地，页面需要按一组代码取涨跌幅。逐个调个股接口会产生 N 次请求，
 * 因此走这个批量入口；一次最多 500 只，超出的会被后端忽略。
 * 查不到的代码**不会出现在结果里**（而不是给一行空值），调用方显示「暂无行情」。
 */
export function fetchMarketQuotes(codes: readonly string[]): Promise<MarketQuote[]> {
  if (codes.length === 0) {
    return Promise.resolve([]);
  }

  return apiGet<MarketQuote[]>('/api/market/quotes', {
    query: { codes: codes.slice(0, 500).join(',') },
    cacheMs: 30_000
  });
}
