import { apiGet } from '@/lib/api';

/** 搜索结果行，对应后端 `SA.Contracts.Search.SearchRowDto`。 */
export interface SearchRow {
  code: string;
  name: string;
  py: string | null;
  board: string;
  industry: string | null;
  /** 无行情时为 null（停牌 / 未采集），界面显示「—」而不是 0。 */
  price: number | null;
  chg: number | null;
  pct: number | null;
  volRatio: number | null;
  turnover: number | null;
  pe: number | null;
  pb: number | null;
  cap: number | null;
  isSt: boolean;
  /** 命中方式：code / name / pinyin / industry。 */
  matchedBy: 'code' | 'name' | 'pinyin' | 'industry';
}

/** 搜索响应。 */
export interface SearchResult {
  query: string;
  total: number;
  page: number;
  pageSize: number;
  rows: SearchRow[];
  scopeNote: string | null;
}

/** 搜索参数。 */
export interface SearchParams {
  q: string;
  board?: string;
  industry?: string;
  page?: number;
  pageSize?: number;
}

/** 搜索股票（代码 / 名称 / 拼音首字母 / 行业关键词）。 */
export function searchStocks(params: SearchParams): Promise<SearchResult> {
  return apiGet<SearchResult>('/api/search', {
    query: {
      q: params.q,
      board: params.board,
      industry: params.industry,
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 50
    }
  });
}
