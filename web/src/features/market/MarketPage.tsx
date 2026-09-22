import { useState } from 'react';
import { Link } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { ErrorState } from '@/components/ui/States';
import { fetchMarketOverview, fetchMarketStocks, type MarketStockRow } from './api';

/**
 * 大盘概况：**一页搞定整个 A 股市场**。
 *
 * 只有两个区块：
 *   1. 指数横条（单行五格，名称 / 点位 / 涨跌幅）
 *   2. 全市场表（板块页签 + 搜索 + 排序 + 分页）
 *
 * 原来这一页有 9 个区块（涨跌家数环形图、北向资金、两市资金与杠杆、行业热力、
 * 行业排行、事件热点、三个榜单、17 个数据源状态点）——那是「看着就没重点」的来源，
 * 全部移除。数据源健康状态属于运维信息，挪到后台「数据源监控」页。
 *
 * 点表格任一行进入 `/market/:code`，右侧整块换成个股区（Tab 在个股区里）。
 */

const PageSize = 60;

/** 排序字段：与后端 `sortBy` 取值一致。 */
type SortKey = 'cap' | 'pct' | 'price' | 'turnover' | 'volRatio' | 'pe';

const SortColumns: { key: SortKey; label: string }[] = [
  { key: 'price', label: '现价' },
  { key: 'pct', label: '涨跌幅' },
  { key: 'turnover', label: '换手率' },
  { key: 'volRatio', label: '量比' },
  { key: 'cap', label: '总市值' },
  { key: 'pe', label: 'PE' }
];

function fmt(value: number | null | undefined, digits = 2): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '—';
  }

  return value.toLocaleString('zh-CN', { minimumFractionDigits: digits, maximumFractionDigits: digits });
}

function signedPct(value: number | null | undefined): string {
  if (value === null || value === undefined) {
    return '—';
  }

  return `${value >= 0 ? '+' : ''}${fmt(value)}%`;
}

function tone(value: number | null | undefined): string {
  if (value === null || value === undefined || value === 0) return 'is-flat';
  return value > 0 ? 'is-up' : 'is-down';
}

export function MarketPage() {
  const [board, setBoard] = useState('');
  const [keyword, setKeyword] = useState('');
  const [draft, setDraft] = useState('');
  const [sortBy, setSortBy] = useState<SortKey>('cap');
  const [desc, setDesc] = useState(true);
  const [page, setPage] = useState(1);

  const indices = useQuery({
    queryKey: ['market', 'indices'],
    queryFn: fetchMarketOverview,
    select: (data) => data.indices
  });

  const stocks = useQuery({
    queryKey: ['market', 'stocks', board, keyword, sortBy, desc, page],
    queryFn: () => fetchMarketStocks({ board, q: keyword, sortBy, desc, page, pageSize: PageSize })
  });

  const data = stocks.data;
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  const toggleSort = (key: SortKey) => {
    if (key === sortBy) {
      setDesc((previous) => !previous);
    } else {
      setSortBy(key);
      setDesc(true);
    }
    setPage(1);
  };

  return (
    <>
      {/* 1. 指数横条 */}
      {indices.data && indices.data.length > 0 ? (
        <div className="index-strip">
          {indices.data.map((index) => (
            <div key={index.code} className="index-strip-cell">
              <span className="index-strip-name">{index.name}</span>
              <span className={`index-strip-value mono ${tone(index.pct)}`}>{fmt(index.price)}</span>
              <span className={`index-strip-pct mono ${tone(index.pct)}`}>{signedPct(index.pct)}</span>
            </div>
          ))}
        </div>
      ) : null}

      {/* 2. 全市场表 */}
      <div className="card">
        <div className="card-head">
          <span className="card-title">全市场</span>
          <span className="card-sub">{data ? `共 ${data.total} 只` : '—'}</span>
          <div className="card-tools">
            <span className="segmented">
              <button
                type="button"
                className={board === '' ? 'is-active' : ''}
                onClick={() => {
                  setBoard('');
                  setPage(1);
                }}
              >
                全部
              </button>
              {(data?.boards ?? []).map((item) => (
                <button
                  key={item}
                  type="button"
                  className={board === item ? 'is-active' : ''}
                  onClick={() => {
                    setBoard(item);
                    setPage(1);
                  }}
                >
                  {item}
                </button>
              ))}
            </span>

            <form
              onSubmit={(event) => {
                event.preventDefault();
                setKeyword(draft);
                setPage(1);
              }}
            >
              <input
                className="input input-sm"
                style={{ width: 190 }}
                type="search"
                placeholder="搜索代码 / 名称 / 拼音"
                value={draft}
                onChange={(event) => setDraft(event.target.value)}
              />
            </form>
          </div>
        </div>

        <div className="card-body is-flush">
          {stocks.isPending && !data ? (
            <div className="sa-boot">正在载入全市场行情…</div>
          ) : !data ? (
            <div style={{ padding: 16 }}>
              <ErrorState
                error={stocks.error}
                onRetry={() => void stocks.refetch()}
                retryCount={stocks.failureCount}
              />
            </div>
          ) : (
            <>
              {data.scopeNote ? <div className="chart-note" style={{ padding: '8px 12px' }}>{data.scopeNote}</div> : null}
              <div className="tbl-wrap">
                <table className="tbl market-table">
                  <thead>
                    <tr>
                      <th>名称 / 代码</th>
                      {SortColumns.slice(0, 2).map((column) => (
                        <th key={column.key} className="num">
                          <button type="button" className="sort-btn" onClick={() => toggleSort(column.key)}>
                            {column.label}
                            {sortBy === column.key ? <span className="sort-caret">{desc ? '▼' : '▲'}</span> : null}
                          </button>
                        </th>
                      ))}
                      <th className="num hide-mobile">换手率</th>
                      <th className="num hide-mobile">量比</th>
                      <th className="num">总市值</th>
                      <th className="num hide-mobile">PE</th>
                      <th className="hide-mobile">行业</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.rows.map((row) => (
                      <Row key={row.code} row={row} />
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="card-foot row-between">
                <span className="fs-11 t-3">
                  第 {data.page} / {pages} 页
                  {data.asOf ? ` · 行情口径 ${data.asOf}` : ''}
                </span>
                <span className="row gap-2">
                  <button
                    type="button"
                    className="btn btn-outline btn-sm"
                    disabled={data.page <= 1}
                    onClick={() => setPage((previous) => Math.max(1, previous - 1))}
                  >
                    上一页
                  </button>
                  <button
                    type="button"
                    className="btn btn-outline btn-sm"
                    disabled={data.page >= pages}
                    onClick={() => setPage((previous) => previous + 1)}
                  >
                    下一页
                  </button>
                </span>
              </div>
            </>
          )}
        </div>
      </div>
    </>
  );
}

/** 一行：整行可点，进入个股区。 */
function Row({ row }: { row: MarketStockRow }) {
  return (
    <tr>
      <td>
        <Link className="stock-cell" to={`/market/${row.code}`}>
          <span className="sc-name">
            {row.name}
            {row.isSt ? (
              <span className="tag tag-danger" style={{ marginLeft: 6 }}>
                ST
              </span>
            ) : null}
          </span>
          <span className="sc-code">{row.code}</span>
        </Link>
      </td>
      <td className="num mono">{fmt(row.price)}</td>
      <td className={`num mono ${tone(row.pct)}`}>{signedPct(row.pct)}</td>
      <td className="num mono hide-mobile">
        {row.turnover === null ? '—' : `${fmt(row.turnover)}%`}
      </td>
      <td className="num mono hide-mobile">{fmt(row.volRatio)}</td>
      <td className="num mono">{row.cap === null ? '—' : `${fmt(row.cap, 0)} 亿`}</td>
      <td className="num mono hide-mobile">{fmt(row.pe)}</td>
      <td className="fs-11 t-2 hide-mobile">{row.industry ?? '—'}</td>
    </tr>
  );
}
