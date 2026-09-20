import { apiGet } from '@/lib/api';

/** 资金面接口类型，对应后端 `SA.Contracts.Capital`。金额为亿元、比率为百分数。 */

export interface FundFlowPoint {
  date: string;
  mainNet: number;
  superLargeNet: number;
  largeNet: number;
  mediumNet: number;
  smallNet: number;
  mainRatio: number | null;
  close: number | null;
  changePercent: number | null;
}

export interface FundFlowSummary {
  days: number;
  mainNet: number;
  inflowDays: number;
  outflowDays: number;
  latestMainNet: number | null;
  latestMainRatio: number | null;
}

export interface Billboard {
  tradeDate: string;
  reason: string | null;
  explain: string | null;
  close: number | null;
  changePercent: number | null;
  turnoverRate: number | null;
  netAmount: number | null;
  buyAmount: number | null;
  sellAmount: number | null;
  next1Change: number | null;
  next5Change: number | null;
  next10Change: number | null;
}

export interface BlockTrade {
  tradeDate: string;
  dealPrice: number | null;
  premiumRatio: number | null;
  dealVolume: number | null;
  dealAmount: number | null;
  buyerName: string | null;
  sellerName: string | null;
  close: number | null;
}

export interface MarginDetail {
  date: string;
  financeBalance: number | null;
  financeBuy: number | null;
  financeNetBuy: number | null;
  loanBalance: number | null;
  totalBalance: number | null;
  financeBalanceRatio: number | null;
  close: number | null;
}

export interface Northbound {
  holdDate: string;
  dateType: string | null;
  holdShares: number | null;
  addShares: number | null;
  addSharesAmp: number | null;
  holdMarketCap: number | null;
  freeSharesRatio: number | null;
  totalSharesRatio: number | null;
  orgQuantity: number | null;
}

export interface Capital {
  code: string;
  name: string;
  asOf: string | null;
  fundFlow: FundFlowPoint[];
  summary: FundFlowSummary | null;
  summary20: FundFlowSummary | null;
  billboards: Billboard[];
  blockTrades: BlockTrade[];
  margins: MarginDetail[];
  northbound: Northbound[];
  insights: string[];
  notes: string[];
}

/** 取资金面数据。 */
export function fetchCapital(code: string): Promise<Capital> {
  return apiGet<Capital>(`/api/stocks/${encodeURIComponent(code)}/capital`);
}
