import { useState } from 'react';
import { Link } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ErrorState } from '@/components/ui/States';
import { useAuth } from '@/providers/AuthProvider';
import { useLiveQuote } from '@/providers/RealtimeProvider';
import { useToast } from '@/providers/ToastProvider';
import { searchStocks } from '@/features/search/api';
import { addWatchItems, fetchWatchlist, removeWatchItems, type WatchItem } from './api';

/**
 * 自选股：**只有两列**——名称、涨跌幅。
 *
 * 原来是一个 14 列的大表（分组页签、列设置、批量操作、分组概览 4 卡、
 * 行业分布环形图、资金流条形图），与「自选股就是列出添加的股票名称」相去甚远。
 *
 * 保留：添加（搜索）、移除、**实时推送**（推送值优先于快照值，并标注数据时间）。
 * 点任一行进入 `/watchlist/:code`，右侧整块换成个股区。
 */
export function WatchlistPage() {
  const { me } = useAuth();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [addOpen, setAddOpen] = useState(false);

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['watchlist'],
    queryFn: fetchWatchlist,
    enabled: me !== null
  });

  const remove = useMutation({
    mutationFn: (code: string) => removeWatchItems([code]),
    onSuccess: () => {
      toast('已从自选股移除', 'info');
      void queryClient.invalidateQueries({ queryKey: ['watchlist'] });
    },
    onError: () => toast('移除失败，请稍后重试', 'error')
  });

  if (me === null) {
    return (
      <div className="card">
        <div className="card-body">
          <div className="fs-13">自选股需要登录后使用。</div>
          <Link className="btn btn-primary btn-sm" to="/login" style={{ marginTop: 12 }}>
            去登录
          </Link>
        </div>
      </div>
    );
  }

  return (
    <>
      <div className="card">
        <div className="card-head">
          <span className="card-title">自选股</span>
          <span className="card-sub">{data ? `${data.items.length} 只` : '—'}</span>
          <div className="card-tools">
            <button type="button" className="btn btn-outline btn-sm" onClick={() => setAddOpen(true)}>
              + 添加自选
            </button>
          </div>
        </div>

        <div className="card-body is-flush">
          {isPending && !data ? (
            <div className="sa-boot">正在载入自选股…</div>
          ) : !data ? (
            <div style={{ padding: 16 }}>
              <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />
            </div>
          ) : data.items.length === 0 ? (
            <div className="fs-12 t-3" style={{ padding: 16 }}>
              还没有自选股，点右上角「+ 添加自选」。
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
                    <th style={{ width: 80 }} />
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((item) => (
                    <Row
                      key={item.code}
                      item={item}
                      canEdit={data.canEdit}
                      onRemove={() => remove.mutate(item.code)}
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

function Row({ item, canEdit, onRemove }: { item: WatchItem; canEdit: boolean; onRemove: () => void }) {
  // 推送值优先，未收到推送时回落到快照值（不把快照当实时）
  const live = useLiveQuote(item.code);
  const pct = live?.pct ?? item.pct;
  const cls = pct === null || pct === undefined || pct === 0 ? 'is-flat' : pct > 0 ? 'is-up' : 'is-down';

  return (
    <tr>
      <td>
        <Link className="stock-cell" to={`/watchlist/${item.code}`}>
          <span className="sc-name">
            {item.name}
            {item.isSt ? (
              <span className="tag tag-danger" style={{ marginLeft: 6 }}>
                ST
              </span>
            ) : null}
          </span>
          <span className="sc-code">{item.code}</span>
        </Link>
      </td>
      <td className={`num mono fs-14 ${cls}`}>
        {pct === null || pct === undefined ? '—' : `${pct >= 0 ? '+' : ''}${pct.toFixed(2)}%`}
      </td>
      <td>
        {canEdit ? (
          <button type="button" className="btn btn-ghost btn-sm" onClick={onRemove} title="移除">
            移除
          </button>
        ) : null}
      </td>
    </tr>
  );
}

/** 添加自选：只留「搜索 + 加入」，不做分组与备注（低频，占地方）。 */
function AddDialog({ onClose }: { onClose: () => void }) {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [keyword, setKeyword] = useState('');

  const search = useQuery({
    queryKey: ['search', 'add', keyword],
    queryFn: () => searchStocks({ q: keyword, pageSize: 8 }),
    enabled: keyword.trim().length > 0
  });

  const add = useMutation({
    mutationFn: (code: string) => addWatchItems([code]),
    onSuccess: () => {
      toast('已加入自选', 'ok');
      void queryClient.invalidateQueries({ queryKey: ['watchlist'] });
      onClose();
    },
    onError: () => toast('加入失败（可能超出数量上限）', 'error')
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
              search.data?.rows.map((row) => (
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
                    disabled={add.isPending}
                    onClick={() => add.mutate(row.code)}
                  >
                    加入
                  </button>
                </div>
              ))
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
