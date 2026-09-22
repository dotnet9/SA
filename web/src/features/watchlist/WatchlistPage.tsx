import { useState } from 'react';
import { Link } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { ErrorState } from '@/components/ui/States';
import { fetchMarketQuotes, type MarketQuote } from '@/features/market/api';
import { searchStocks } from '@/features/search/api';
import { useLiveQuote } from '@/providers/RealtimeProvider';
import { useToast } from '@/providers/ToastProvider';
import { MaxWatchItems, useWatchlist } from './WatchlistProvider';

/**
 * 自选股：**只有两列**——名称、涨跌幅。
 *
 * 自选代码存在浏览器本地（{@link useWatchlist}），名称与涨跌幅从批量行情接口取。
 * 代价要知道：换浏览器或清站点数据会丢自选，也不会多设备同步——这是「无需登录」的取舍。
 *
 * 保留：添加（搜索）、移除、上移/下移、清空、**实时推送**（推送值优先于快照值）。
 * 点任一行进入 `/watchlist/:code`，右侧整块换成个股区。
 */
export function WatchlistPage() {
  const { codes, remove, clear, moveUp, moveDown } = useWatchlist();
  const { toast } = useToast();
  const [addOpen, setAddOpen] = useState(false);

  const quotes = useQuery({
    queryKey: ['watchlist', 'quotes', codes.join(',')],
    queryFn: () => fetchMarketQuotes(codes),
    enabled: codes.length > 0
  });

  // 后端只返回查得到的代码，这里按本地顺序取值，取不到就显示「暂无行情」
  const byCode = new Map((quotes.data ?? []).map((row) => [row.code, row]));

  return (
    <>
      <div className="card">
        <div className="card-head">
          <span className="card-title">自选股</span>
          <span className="card-sub">
            {codes.length} 只{codes.length >= MaxWatchItems ? `（已达上限 ${MaxWatchItems}）` : ''}
          </span>
          <div className="card-tools">
            {codes.length > 0 ? (
              <button
                type="button"
                className="btn btn-ghost btn-sm"
                onClick={() => {
                  clear();
                  toast('已清空自选股', 'info');
                }}
              >
                清空
              </button>
            ) : null}
            <button type="button" className="btn btn-outline btn-sm" onClick={() => setAddOpen(true)}>
              + 添加自选
            </button>
          </div>
        </div>
        <div className="card-body is-flush">
          {codes.length === 0 ? (
            <div className="fs-12 t-3" style={{ padding: 16 }}>
              还没有自选股。可以点右上角「+ 添加自选」，或在顶栏搜索框里直接把股票加入自选。
            </div>
          ) : quotes.isPending && !quotes.data ? (
            <div className="sa-boot">正在载入自选股行情…</div>
          ) : !quotes.data ? (
            <div style={{ padding: 16 }}>
              <ErrorState
                error={quotes.error}
                onRetry={() => void quotes.refetch()}
                retryCount={quotes.failureCount}
              />
            </div>
          ) : (
            <div className="tbl-wrap">
              <table className="tbl">
                <thead>
                  <tr>
                    <th>名称</th>
                    <th className="num" style={{ width: 130 }}>
                      涨跌幅
                    </th>
                    <th style={{ width: 150 }} />
                  </tr>
                </thead>
                <tbody>
                  {codes.map((code, index) => (
                    <Row
                      key={code}
                      code={code}
                      quote={byCode.get(code)}
                      first={index === 0}
                      last={index === codes.length - 1}
                      onRemove={() => {
                        remove(code);
                        toast('已从自选股移除', 'info');
                      }}
                      onMoveUp={() => moveUp(code)}
                      onMoveDown={() => moveDown(code)}
                    />
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {addOpen ? <AddDialog onClose={() => setAddOpen(false)} /> : null}
    </>
  );
}

function Row({
  code,
  quote,
  first,
  last,
  onRemove,
  onMoveUp,
  onMoveDown
}: {
  code: string;
  quote: MarketQuote | undefined;
  first: boolean;
  last: boolean;
  onRemove: () => void;
  onMoveUp: () => void;
  onMoveDown: () => void;
}) {
  // 推送值优先，未收到推送时回落到批量快照（不把快照当实时）
  const live = useLiveQuote(code);
  const pct = live?.pct ?? quote?.pct ?? null;
  const cls = pct === null || pct === 0 ? 'is-flat' : pct > 0 ? 'is-up' : 'is-down';

  return (
    <tr>
      <td>
        <Link className="stock-cell" to={`/watchlist/${code}`}>
          <span className="sc-name">
            {quote?.name ?? code}
            {quote?.isSt ? (
              <span className="tag tag-danger" style={{ marginLeft: 6 }}>
                ST
              </span>
            ) : null}
          </span>
          <span className="sc-code">
            {code}
            {quote?.industry ? ` · ${quote.industry}` : ''}
          </span>
        </Link>
      </td>
      <td className={`num mono fs-14 ${cls}`}>
        {pct === null ? '—' : `${pct >= 0 ? '+' : ''}${pct.toFixed(2)}%`}
      </td>
      <td>
        <span className="row gap-1">
          <button type="button" className="icon-btn" title="上移" disabled={first} onClick={onMoveUp}>
            ↑
          </button>
          <button type="button" className="icon-btn" title="下移" disabled={last} onClick={onMoveDown}>
            ↓
          </button>
          <button type="button" className="btn btn-ghost btn-sm" title="移除" onClick={onRemove}>
            移除
          </button>
        </span>
      </td>
    </tr>
  );
}

/** 添加自选：只留「搜索 + 加入」，不做分组与备注（低频，占地方）。 */
function AddDialog({ onClose }: { onClose: () => void }) {
  const { codes, add } = useWatchlist();
  const { toast } = useToast();
  const [keyword, setKeyword] = useState('');

  const search = useQuery({
    queryKey: ['search', 'add', keyword],
    queryFn: () => searchStocks({ q: keyword, pageSize: 8 }),
    enabled: keyword.trim().length > 0
  });

  return (
    <div className="overlay is-open" onClick={onClose}>
      <div className="dialog" onClick={(event) => event.stopPropagation()}>
        <div className="dialog-head">
          <h3>添加自选</h3>
          <button type="button" className="icon-btn" style={{ marginLeft: 'auto' }} onClick={onClose}>
            ✕
          </button>
        </div>
        <div className="dialog-body">
          <input
            className="input"
            type="search"
            autoFocus
            placeholder="代码 / 名称 / 拼音首字母，如 300750、宁德时代、ndsd"
            value={keyword}
            onChange={(event) => setKeyword(event.target.value)}
          />

          <div className="col gap-2" style={{ marginTop: 12 }}>
            {keyword.trim().length === 0 ? (
              <span className="fs-12 t-3">输入代码或名称开始搜索</span>
            ) : search.isPending ? (
              <span className="fs-12 t-3">搜索中…</span>
            ) : (search.data?.rows.length ?? 0) === 0 ? (
              <span className="fs-12 t-3">没有匹配的股票</span>
            ) : (
              search.data?.rows.map((row) => {
                const added = codes.includes(row.code);

                return (
                  <div key={row.code} className="row-between" style={{ padding: '6px 0' }}>
                    <span className="stock-cell">
                      <span className="sc-name">{row.name}</span>
                      <span className="sc-code">
                        {row.code} · {row.board}
                      </span>
                    </span>
                    <button
                      type="button"
                      className="btn btn-outline btn-sm"
                      disabled={added}
                      onClick={() => {
                        if (add(row.code)) {
                          toast('已加入自选', 'ok');
                        } else {
                          toast(`加入失败（已在自选或超出上限 ${MaxWatchItems}）`, 'error');
                        }
                      }}
                    >
                      {added ? '已在自选' : '加入'}
                    </button>
                  </div>
                );
              })
            )}
          </div>
        </div>
        <div className="dialog-foot">
          <button type="button" className="btn btn-ghost" onClick={onClose}>
            关闭
          </button>
        </div>
      </div>
    </div>
  );
}
