import { apiGet } from '@/lib/api';

/** 评级接口类型，对应后端 `SA.Contracts.Rating`。 */

export interface RatingBucket {
  level: string;
  count: number;
  tone: string;
}

export interface RatingYear {
  year: number;
  eps: number;
  /** 是否为已实现值（上游标记 A）；false 表示预测值。 */
  isActual: boolean;
  growthVsPrevious: number | null;
}

export interface Rating {
  code: string;
  name: string;
  asOf: string | null;
  orgNum: number;
  buckets: RatingBucket[];
  consensusLevel: string;
  bullishRatio: number | null;
  aimPriceMin: number | null;
  aimPriceMax: number | null;
  currentPrice: number | null;
  upsideMin: number | null;
  upsideMax: number | null;
  years: RatingYear[];
  insights: string[];
  notes: string[];
}

/** 取机构评级与预测。 */
export function fetchRating(code: string): Promise<Rating> {
  return apiGet<Rating>(`/api/stocks/${encodeURIComponent(code)}/rating`);
}

/** 风险接口类型，对应后端 `SA.Contracts.Risk`。 */

export interface RiskItem {
  key: string;
  category: string;
  level: 'high' | 'medium' | 'low';
  title: string;
  detail: string;
  metric: string | null;
  threshold: string | null;
}

export interface RiskMetric {
  label: string;
  value: string;
  threshold: string | null;
  triggered: boolean;
}

export interface Risk {
  code: string;
  name: string;
  asOf: string | null;
  score: number;
  grade: string;
  items: RiskItem[];
  metrics: RiskMetric[];
  insights: string[];
  notes: string[];
}

/** 取风险监控。 */
export function fetchRisk(code: string): Promise<Risk> {
  return apiGet<Risk>(`/api/stocks/${encodeURIComponent(code)}/risk`);
}
