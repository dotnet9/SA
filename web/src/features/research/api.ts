import { apiGet } from '@/lib/api';

/** 价值研究接口类型，对应后端 `SA.Contracts.Research`。 */

/** 数据状态四态。 */
export type ResearchStatus = 'ok' | 'noData' | 'notApplicable' | 'noUpcoming';

export interface ValueResearchHistoryPoint {
  period: string;
  value: number | null;
}

export interface ValueResearchItem {
  key: string;
  name: string;
  group: string;
  value: string | null;
  unit: string | null;
  asOf: string | null;
  status: ResearchStatus;
  source: string | null;
  history: ValueResearchHistoryPoint[] | null;
}

export interface ValueResearchGroup {
  key: string;
  name: string;
  question: string;
  items: ValueResearchItem[];
  defaultExpanded: boolean;
}

export interface ValueResearchMetric {
  name: string;
  value: string;
  status: ResearchStatus;
}

export interface ValueResearchConclusion {
  summary: string;
  metrics: ValueResearchMetric[];
  reportDate: string | null;
}

export interface ValueResearch {
  code: string;
  name: string;
  board: string;
  industry: string | null;
  conclusion: ValueResearchConclusion;
  groups: ValueResearchGroup[];
  caliberNotes: string[];
  fundamentalAsOf: string | null;
}

export interface FundamentalHistoryPoint {
  reportDate: string;
  reportType: string | null;
  revenue: number | null;
  netProfit: number | null;
  roeWeighted: number | null;
  grossMargin: number | null;
  netMargin: number | null;
}

export interface FundamentalHistory {
  code: string;
  points: FundamentalHistoryPoint[];
  annualPoints: FundamentalHistoryPoint[];
}

export interface BusinessCompositionItem {
  reportDate: string;
  /** 1=按行业/大类、2=按产品、3=按地区。三套并列，不能相加。 */
  mainOpType: number;
  itemName: string;
  income: number | null;
  /** 占比是**小数**（0.652002 = 65.20%），展示时 ×100。 */
  incomeRatio: number | null;
  /** 毛利率是**小数**；招股书口径为 null。 */
  grossProfitRatio: number | null;
  /** 「其中:」子项：已计入父项，不与父项并列展示。 */
  isSubItem: boolean;
}

export interface BusinessComposition {
  code: string;
  reportDates: string[];
  items: BusinessCompositionItem[];
  subItems: BusinessCompositionItem[];
  businessScope: string | null;
  businessReview: string | null;
}

export interface ShareChange {
  endDate: string;
  totalShares: number | null;
  limitedShares: number | null;
  unlimitedShares: number | null;
  changeReason: string | null;
}

export interface UpcomingUnlock {
  liftDate: string;
  liftType: string;
  liftShares: number | null;
  totalSharesRatio: number | null;
  unlimitedASharesRatio: number | null;
}

export interface ShareStructure {
  code: string;
  changes: ShareChange[];
  upcomingUnlocks: UpcomingUnlock[];
  /** `noUpcoming` 表示**确实没有**待解禁（已全流通），不是「没查到」。 */
  unlockStatus: ResearchStatus;
}

export interface Announcement {
  artCode: string;
  title: string;
  noticeDate: string;
  columnName: string | null;
  annType: string | null;
  url: string | null;
}

export interface AnnouncementList {
  code: string;
  total: number;
  types: Record<string, number>;
  items: Announcement[];
}

export interface ResearchReport {
  infoCode: string;
  title: string;
  orgShortName: string | null;
  researcher: string | null;
  publishDate: string;
  ratingName: string | null;
  industryName: string | null;
  predictThisYearEps: number | null;
  predictThisYearPe: number | null;
  predictNextYearEps: number | null;
  predictNextYearPe: number | null;
  predictNextTwoYearEps: number | null;
  predictNextTwoYearPe: number | null;
  url: string | null;
}

export interface ResearchList {
  code: string;
  items: ResearchReport[];
  /** 目标价缺失的说明：界面据此显示空态原因，而不是留空。 */
  aimPriceNote: string;
}

/** 取价值研究聚合结果。 */
export function fetchValueResearch(code: string): Promise<ValueResearch> {
  return apiGet<ValueResearch>(`/api/stocks/${code}/value-research`);
}

/** 取历史财务序列。 */
export function fetchFundamentalHistory(code: string, years = 10): Promise<FundamentalHistory> {
  return apiGet<FundamentalHistory>(`/api/stocks/${code}/fundamental-history?years=${years}`);
}

/** 取主营构成。 */
export function fetchBusinessComposition(code: string): Promise<BusinessComposition> {
  return apiGet<BusinessComposition>(`/api/stocks/${code}/business-composition`);
}

/** 取股本结构与限售解禁。 */
export function fetchShareStructure(code: string): Promise<ShareStructure> {
  return apiGet<ShareStructure>(`/api/stocks/${code}/share-structure`);
}

/** 取公告列表。 */
export function fetchAnnouncements(code: string, type?: string): Promise<AnnouncementList> {
  const query = type ? `?type=${encodeURIComponent(type)}` : '';
  return apiGet<AnnouncementList>(`/api/stocks/${code}/announcements${query}`);
}

/** 取研报列表（不含目标价）。 */
export function fetchResearchReports(code: string): Promise<ResearchList> {
  return apiGet<ResearchList>(`/api/stocks/${code}/research`);
}

/**
 * 数据状态的展示文案。
 *
 * 四态必须区分：「不适用」与「暂无数据」含义不同，
 * 「暂无待解禁」更是「确实没有」而不是「没查到」。
 */
export function statusText(status: ResearchStatus): string | null {
  switch (status) {
    case 'noData':
      return '暂无数据';
    case 'notApplicable':
      return '不适用';
    case 'noUpcoming':
      return '暂无待解禁';
    default:
      return null;
  }
}

/** 小数占比 → 百分数展示（0.652002 → 65.20%）。 */
export function ratioToPercent(ratio: number | null | undefined): string {
  return ratio === null || ratio === undefined ? '—' : `${(ratio * 100).toFixed(2)}%`;
}

/** 元 → 亿元。 */
export function toYi(yuan: number | null | undefined): string {
  return yuan === null || yuan === undefined ? '—' : `${(yuan / 100_000_000).toFixed(2)} 亿`;
}
