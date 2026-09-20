import { apiGet } from '@/lib/api';

/** 股权接口类型，对应后端 `SA.Contracts.Equity`。 */
export interface EquityHolder {
  rank: number;
  name: string;
  holdNum: number;
  holdRatio: number | null;
  freeHoldRatio: number | null;
  change: string | null;
  holderType: string | null;
  sharesType: string | null;
  marketCap: number | null;
}

export interface EquityPeriod {
  endDate: string;
  noticeDate: string | null;
  holders: EquityHolder[];
  totalRatio: number | null;
  concentration: string | null;
}

export interface HolderCount {
  endDate: string;
  reportName: string | null;
  holderNum: number;
  change: number | null;
  changeRatio: number | null;
  avgHoldNum: number | null;
  avgMarketCap: number | null;
  totalShares: number | null;
}

export interface Pledge {
  tradeDate: string;
  pledgeRatio: number | null;
  pledgeShares: number | null;
  pledgeDealNum: number | null;
  pledgeMarketCap: number | null;
  industry: string | null;
  year1Change: number | null;
}

export interface Equity {
  code: string;
  name: string;
  asOf: string | null;
  latest: EquityPeriod | null;
  latestFreeFloat: EquityPeriod | null;
  history: EquityPeriod[];
  holderCounts: HolderCount[];
  pledge: Pledge | null;
  insights: string[];
  notes: string[];
}

/** 取股权结构数据。 */
export function fetchEquity(code: string): Promise<Equity> {
  return apiGet<Equity>(`/api/stocks/${encodeURIComponent(code)}/equity`);
}
