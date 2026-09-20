import { apiGet } from '@/lib/api';

/**
 * 财务接口类型，对应后端 `SA.Contracts.Finance`。金额为亿元，比率为百分数。
 */

/** 一期财务数据。 */
export interface FinancePeriod {
  reportDate: string;
  reportType: string | null;
  quarter: string | null;
  revenue: number | null;
  revenueYoy: number | null;
  netProfit: number | null;
  netProfitYoy: number | null;
  eps: number | null;
  deductedEps: number | null;
  roe: number | null;
  bps: number | null;
  operatingCashFlowPerShare: number | null;
  grossMargin: number | null;
  revenueQoq: number | null;
  netProfitQoq: number | null;
  dividendPlan: string | null;
  dividendYield: number | null;
  noticeDate: string | null;
}

/** 业绩预告。 */
export interface EarningsForecast {
  reportDate: string;
  forecastType: string | null;
  summary: string | null;
  netProfitMin: number | null;
  netProfitMax: number | null;
  changeMin: number | null;
  changeMax: number | null;
  noticeDate: string | null;
}

/** 财务趋势点。 */
export interface FinanceTrendPoint {
  label: string;
  revenue: number | null;
  netProfit: number | null;
  roe: number | null;
  grossMargin: number | null;
  revenueYoy: number | null;
  netProfitYoy: number | null;
  operatingCashFlowPerShare: number | null;
  eps: number | null;
}

/** 财务视图。 */
export interface Finance {
  code: string;
  name: string;
  asOf: string | null;
  latest: FinancePeriod | null;
  periods: FinancePeriod[];
  trend: FinanceTrendPoint[];
  forecasts: EarningsForecast[];
  insights: string[];
  notes: string[];
}

/** 取财务数据。 */
export function fetchFinance(code: string): Promise<Finance> {
  return apiGet<Finance>(`/api/stocks/${encodeURIComponent(code)}/finance`);
}
