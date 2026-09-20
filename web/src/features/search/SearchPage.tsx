import { useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { EmptyState, ErrorState } from '@/components/ui/States';
import { errorText } from '@/lib/errorText';
import { addWatchItems, fetchWatchlist } from '@/features/watchlist/api';
import { useAuth } from '@/providers/AuthProvider';
import { useToast } from '@/providers/ToastProvider';
import { searchStocks, type SearchRow } from './api';

/**
 * 股票搜索。
 *
 * 结构与 `design/web/search-results.html` 对应：快捷筛选 → 结果表 → 搜索技巧。
 * 本批后端已支持「代码 / 名称 / 拼音首字母 / 行业」四种命中与板块过滤，并可直接加入自选；
 * 市值过滤、导出（第 12 批）、近 30 日走势（第 3 批已完成趋势页，搜索页缩略图待接入）尚未接入，
 * 因此不渲染这些控件，也不给假数据。
 */

/** 板块筛选项（与需求规格 §5.3 的 Board 枚举一致）。 */
const Boards = ['全部', '沪市主板', '深市主板', '创业板', '科创板', '北交所'] as const;

/** 每页条数。 */
const PageSize = 50;

/** 命中方式的中文说明。 */
const MatchedByText: Record<SearchRow['matchedBy'], string> = {
  code: '代码命中',
  name: '名称命中',
  pinyin: '拼音命中',
  industry: '行业命中'
};

/** 数字格式化；null 显示「—」。 */
function fmt(value: number | null, digits = 2, suffix = ''): string {
  if (value === null || value === undefined) {
    return '—';
  }

  return `${value.toLocaleString('zh-CN', { minimumFractionDigits: digits, maximumFractionDigits: digits })}${suffix}`;
}

/** 带符号百分比。 */
function signed(value: number | null, digits = 2): string {
  if (value === null || value === undefined) {
    return '—';
  }

  return `${value >= 0 ? '+' : ''}${fmt(value, digits)}%`;
}

/** 涨跌类名。 */
function tone(value: number | null): string {
  if (value === null) return '';
  return value > 0 ? 'is-up' : value < 0 ? 'is-down' : 'is-flat';
}

export function SearchPage() {
  const [params, setParams] = useSearchParams();

  // 关键词与筛选放在 URL 上，保证刷新与分享后结果一致
  const query = params.get('q') ?? '';
  const boardParam = params.get('board') ?? '';
  const industry = params.get('industry') ?? '';
  const page = Math.max(1, Number(params.get('page') ?? '1') || 1);

  const [keyword, setKeyword] = useState(query);
  const [industryInput, setIndustryInput] = useState(industry);

  useEffect(() => {
    setKeyword(query);
  }, [query]);

  useEffect(() => {
    setIndustryInput(industry);
  }, [industry]);

  const filters = useMemo(
    () => ({ q: query, board: boardParam || undefined, industry: industry || undefined, page, pageSize: PageSize }),
    [query, boardParam, industry, page]
  );

  const { data, error, isPending, isFetching } = useQuery({
    queryKey: ['search', filters],
    queryFn: () => searchStocks(filters),
    enabled: query.trim().length > 0
  });

  /* --- 加入自选（需 watchlist.edit；已在自选的不再重复提交） --- */
  const auth = useAuth();
  const queryClient = useQueryClient();
  const toast = useToast();
  const canEditWatchlist = auth.can('watchlist.edit');

  const { data: watchlist } = useQuery({
    queryKey: ['watchlist'],
    queryFn: fetchWatchlist,
    enabled: canEditWatchlist,
    staleTime: 30_000
  });

  const added = useMemo(
    () => new Set((watchlist?.items ?? []).map((item) => item.code)),
    [watchlist]
  );

  const [adding, setAdding] = useState<string | null>(null);

  const addToWatchlist = async (code: string) => {
    setAdding(code);
    try {
      await addWatchItems([code]);
      toast.toast('已加入自选', 'ok');
      await queryClient.invalidateQueries({ queryKey: ['watchlist'] });
    } catch (addError) {
      toast.toast(errorText(addError), 'error');
    } finally {
      setAdding(null);
    }
  };

  const update = (patch: Record<string, string | undefined>) => {
    const next = new URLSearchParams(params);
    for (const [key, value] of Object.entries(patch)) {
      if (value === undefined || value === '') {
        next.delete(key);
      } else {
        next.set(key, value);
      }
    }

    // 任何筛选变化都回到第一页，避免落在空页
    if (!('page' in patch)) {
      next.delete('page');
    }

    setParams(next);
  };

  const rows = data?.rows ?? [];
  const total = data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PageSize));

  return (
    <>
      <div className="sa-pagehead">
        <div>
          <div className="breadcrumb">
            <Link to="/market">市场概览</Link>
            <span className="sep">/</span>
            <span>搜索结果</span>
          </div>
          <h1>{query ? `「${query}」的搜索结果` : '股票搜索'}</h1>
          <div className="sub">
            {query
              ? `共命中 ${total} 只 · 按相关度排序 · 支持代码 / 名称 / 拼音首字母 / 行业`
              : '输入代码、名称、拼音首字母或行业关键词开始搜索'}
            {isFetching && query ? ' · 更新中…' : ''}
          </div>
        </div>
      </div>

      {/* 快捷筛选 */}
      <div className="card">
        <div className="card-body is-tight row gap-3 wrap">
          <span className="label" style={{ width: 'auto' }}>
            板块
          </span>
          <span className="segmented">
            {Boards.map((item) => (
              <span
                key={item}
                className={boardParam === (item === '全部' ? '' : item) ? 'is-active' : undefined}
                onClick={() => update({ board: item === '全部' ? undefined : item })}
              >
                {item}
              </span>
            ))}
          </span>

          <span className="label" style={{ width: 'auto', marginLeft: 8 }}>
            行业
          </span>
          {/* 与关键词一致：输入过程中不发请求，回车或失焦时才落到 URL 上，
              避免每敲一个字都触发一次全市场扫描 */}
          <input
            className="input input-sm"
            style={{ width: 140 }}
            placeholder="如：电池"
            value={industryInput}
            onChange={(event) => setIndustryInput(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                update({ industry: industryInput });
              }
            }}
            onBlur={() => update({ industry: industryInput })}
          />

          <span className="label" style={{ width: 'auto', marginLeft: 8 }}>
            关键词
          </span>
          <input
            className="input input-sm"
            style={{ width: 180 }}
            placeholder="代码 / 名称 / 拼音"
            value={keyword}
            onChange={(event) => setKeyword(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                update({ q: keyword });
              }
            }}
          />

          <button
            type="button"
            className="btn btn-sm btn-ghost"
            onClick={() => {
              setKeyword('');
              setIndustryInput('');
              setParams(new URLSearchParams());
            }}
          >
            重置
          </button>

          <span className="tag tag-outline" style={{ marginLeft: 'auto' }}>
            命中 {total} 只
          </span>
        </div>
      </div>

      {/* 结果表 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">搜索结果</span>
          <span className="card-sub">点击任意行进入个股分析矩阵</span>
        </div>
        <div className="card-body is-flush">
          {!query ? (
            <EmptyState
              title="请输入搜索条件"
              hint="支持 6 位代码精确匹配、中文名称模糊匹配、拼音首字母（如宁德时代 → ndsd）与行业关键词。"
            />
          ) : isPending ? (
            <div className="sa-boot" style={{ height: 140 }}>
              正在搜索…
            </div>
          ) : error ? (
            <ErrorState error={error} />
          ) : rows.length === 0 ? (
            <EmptyState
              title="没有匹配的股票"
              hint="可以换一个代码、名称片段或行业关键词；行业关键词会返回该行业的全部个股。"
            />
          ) : (
            <>
              <div className="tbl-wrap">
                <table className="tbl is-comfort">
                  <thead>
                    <tr>
                      <th>名称 / 代码</th>
                      <th>板块</th>
                      <th>所属行业</th>
                      <th className="num">现价</th>
                      <th className="num">涨跌幅</th>
                      <th className="num hide-mobile">换手率</th>
                      <th className="num hide-mobile">量比</th>
                      <th className="num hide-mobile">PE(TTM)</th>
                      <th className="num hide-mobile">PB</th>
                      <th className="num">总市值</th>
                      <th style={{ width: 92 }}>命中方式</th>
                      <th className="col-actions" style={{ width: 96 }}>
                        操作
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((row) => (
                      <tr key={row.code} data-chg={row.pct === null ? 'flat' : row.pct >= 0 ? 'up' : 'down'}>
                        <td>
                          <Link className="stock-cell" to={`/stock/${row.code}`}>
                            <span className="sc-name">
                              {row.name}
                              {row.isSt ? <span className="tag tag-danger" style={{ marginLeft: 6 }}>ST</span> : null}
                            </span>
                            <span className="sc-code">{row.code}</span>
                          </Link>
                        </td>
                        <td>
                          <span className="tag tag-outline">{row.board}</span>
                        </td>
                        <td className="t-2">{row.industry ?? '—'}</td>
                        <td className="num mono">{fmt(row.price, 2)}</td>
                        <td className={`num ${tone(row.pct)}`}>{signed(row.pct)}</td>
                        <td className="num hide-mobile">{fmt(row.turnover, 2, '%')}</td>
                        <td className="num hide-mobile">{fmt(row.volRatio, 2)}</td>
                        <td className="num hide-mobile">{fmt(row.pe, 2)}</td>
                        <td className="num hide-mobile">{fmt(row.pb, 2)}</td>
                        <td className="num">{fmt(row.cap, 2, ' 亿')}</td>
                        <td className="fs-11 t-3">{MatchedByText[row.matchedBy]}</td>
                        <td className="col-actions">
                          <span className="row gap-2">
                            <Link className="btn btn-sm btn-outline" to={`/stock/${row.code}`}>
                              查看
                            </Link>
                            {canEditWatchlist ? (
                              <button
                                type="button"
                                className="btn btn-sm btn-ghost"
                                disabled={added.has(row.code) || adding === row.code}
                                onClick={() => void addToWatchlist(row.code)}
                              >
                                {added.has(row.code) ? '已在自选' : adding === row.code ? '加入中' : '加自选'}
                              </button>
                            ) : null}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="tbl-foot">
                <span>
                  共 {total} 条
                  {totalPages > 1 ? ` · 第 ${page}/${totalPages} 页` : ''}
                  {total > rows.length && totalPages === 1 ? ` · 已显示前 ${rows.length} 条` : ''}
                </span>
                {totalPages > 1 ? (
                  <div className="pager">
                    <span
                      aria-disabled={page <= 1}
                      onClick={() => page > 1 && update({ page: String(page - 1) })}
                    >
                      ‹
                    </span>
                    <span className="is-active">{page}</span>
                    <span
                      aria-disabled={page >= totalPages}
                      onClick={() => page < totalPages && update({ page: String(page + 1) })}
                    >
                      ›
                    </span>
                  </div>
                ) : null}
              </div>
            </>
          )}
        </div>
      </div>

      {/* 搜索技巧 */}
      <div className="grid grid-2 mt-4">
        <div className="card">
          <div className="card-head">
            <span className="card-title">搜索技巧</span>
          </div>
          <div className="card-body col gap-2">
            <div className="row gap-3">
              <span className="tag tag-outline mono">300750</span>
              <span className="fs-12 t-2">6 位代码精确匹配（优先级最高）</span>
            </div>
            <div className="row gap-3">
              <span className="tag tag-outline">宁德时代</span>
              <span className="fs-12 t-2">中文名称模糊匹配</span>
            </div>
            <div className="row gap-3">
              <span className="tag tag-outline mono">ndsd</span>
              <span className="fs-12 t-2">拼音首字母前缀匹配</span>
            </div>
            <div className="row gap-3">
              <span className="tag tag-outline">电池</span>
              <span className="fs-12 t-2">行业关键词（返回该行业全部个股）</span>
            </div>
            <div className="row gap-3">
              <span className="tag tag-outline mono">⌘/Ctrl + K</span>
              <span className="fs-12 t-2">在任意页面唤起搜索框</span>
            </div>
          </div>
        </div>

        <div className="card">
          <div className="card-head">
            <span className="card-title">本批尚未接入</span>
            <span className="card-sub">按批次推进，不使用占位数据</span>
          </div>
          <div className="card-body col gap-2 fs-12 t-2">
            <div>· 市值区间筛选（随条件选股器统一实现，第 12 批）</div>
            <div>· 批量加入自选（第 4 批）</div>
            <div>· 导出结果（第 12 批，受 `export.data` 权限与配额约束）</div>
            <div>· 近 30 日走势缩略图（随日线采集接入，第 3 批）</div>
            <div className="legend-block mt-3">
              搜索基于东财行业口径与全市场股票池；命中方式在结果表最后一列标出，便于确认排序为什么是这样。
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
