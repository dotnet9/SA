import { useState } from 'react';
import { Link } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { stockPathKeepingTab } from '@/app/useCurrentStock';
import { useAuth } from '@/providers/AuthProvider';
import { fetchMarketOverview } from '@/features/market/api';
import { fetchWatchlist, type WatchItem } from '@/features/watchlist/api';

/**
 * 自选股横条（实施计划 §7.2）。
 *
 * 常驻在顶栏下方，**不占常驻栏位**（这是与原型最大的结构差别）：
 * 横向滚动显示 名称 + 涨跌幅，点击即在内容区打开该股；可一键收起为一行。
 *
 * 未登录时改为展示**公开榜单**（涨幅榜，数据本就公开），并给出登录入口——
 * 自选股需要登录，留一个空横条只会让人以为功能坏了。
 */
export function WatchStrip({ collapsed, onToggle }: { collapsed: boolean; onToggle: () => void }) {
  const { me } = useAuth();
  const anonymous = me === null;
  const [drawerOpen, setDrawerOpen] = useState(false);

  const watchlist = useQuery({
    queryKey: ['watchlist'],
    queryFn: fetchWatchlist,
    enabled: !anonymous
  });

  const overview = useQuery({
    queryKey: ['market', 'overview'],
    queryFn: fetchMarketOverview,
    enabled: anonymous
  });

  const items: WatchItem[] = anonymous ? [] : (watchlist.data?.items ?? []);
  const gainers = anonymous ? (overview.data?.rankings?.gainers ?? []).slice(0, 20) : [];

  if (collapsed) {
    return (
      <div className="sa-strip is-collapsed">
        <button type="button" className="sa-strip-toggle" onClick={onToggle} title="展开自选股">
          ▸ {anonymous ? '公开榜单' : '自选股'}
          {!anonymous && items.length > 0 ? `（${items.length}）` : ''}
        </button>
      </div>
    );
  }

  return (
    <>
      <div className="sa-strip">
        <button type="button" className="sa-strip-toggle" onClick={onToggle} title="收起">
          ▾
        </button>
        <span className="sa-strip-label">{anonymous ? '公开榜单' : '自选股'}</span>

        <div className="sa-strip-list">
          {anonymous ? (
            gainers.length === 0 ? (
              <span className="fs-12 t-3">{overview.isPending ? '正在载入…' : '暂无榜单数据'}</span>
            ) : (
              gainers.map((row) => (
                <Link
                  key={row.code}
                  className="sa-strip-item"
                  to={`/stock/${row.code}`}
                  title={`${row.name} ${row.code}`}
                >
                  <span className="ellipsis" style={{ maxWidth: 72 }}>
                    {row.name}
                  </span>
                  <span className={`mono fs-11 ${tone(row.pct)}`}>{fmtPct(row.pct)}</span>
                </Link>
              ))
            )
          ) : items.length === 0 ? (
            <span className="fs-12 t-3">
              {watchlist.isPending ? '正在载入…' : '还没有自选股，去自选页添加或点个股页的「加自选」'}
            </span>
          ) : (
            items.map((item) => (
              <Link
                key={item.code}
                className="sa-strip-item"
                to={stockPathKeepingTab(item.code)}
                title={`${item.name} ${item.code}${item.note ? ` · ${item.note}` : ''}`}
              >
                <span className="ellipsis" style={{ maxWidth: 72 }}>
                  {item.name}
                </span>
                <span className={`mono fs-11 ${tone(item.pct)}`}>{fmtPct(item.pct)}</span>
              </Link>
            ))
          )}
        </div>

        {anonymous ? (
          <Link className="btn btn-primary btn-sm" to="/login" title="登录后可自建自选股">
            登录后自建自选
          </Link>
        ) : (
          <button
            type="button"
            className="btn btn-outline btn-sm"
            onClick={() => setDrawerOpen(true)}
            title="浏览与管理全部自选"
          >
            管理
          </button>
        )}
      </div>

      {/* 抽屉：需要浏览/管理较多标的时从左侧滑出，覆盖式，不占常驻栏位 */}
      {drawerOpen ? (
        <div className="sa-drawer-backdrop" onClick={() => setDrawerOpen(false)}>
          <aside className="sa-drawer" onClick={(event) => event.stopPropagation()}>
            <div className="card-head">
              <span className="card-title">自选股（{items.length}）</span>
              <button type="button" className="icon-btn" onClick={() => setDrawerOpen(false)} title="关闭">
                ✕
              </button>
            </div>
            <div className="card-body is-flush">
              {items.length === 0 ? (
                <div className="fs-12 t-3" style={{ padding: 12 }}>
                  还没有自选股
                </div>
              ) : (
                items.map((item) => (
                  <Link key={item.code} className="sa-drawer-row" to={stockPathKeepingTab(item.code)}>
                    <span className="col grow">
                      <span className="fs-13">{item.name}</span>
                      <span className="mono fs-11 t-3">
                        {item.code}
                        {item.industry ? ` · ${item.industry}` : ''}
                      </span>
                    </span>
                    <span className={`mono fs-12 ${tone(item.pct)}`}>{fmtPct(item.pct)}</span>
                  </Link>
                ))
              )}
            </div>
            <div style={{ padding: 12 }}>
              <Link className="btn btn-outline btn-sm" to="/watchlist">
                打开自选股页（分组 / 排序 / 备注）
              </Link>
            </div>
          </aside>
        </div>
      ) : null}
    </>
  );
}

function tone(pct: number | null): string {
  if (pct === null || pct === 0) {
    return 'is-flat';
  }

  return pct > 0 ? 'is-up' : 'is-down';
}

function fmtPct(pct: number | null): string {
  if (pct === null) {
    return '—';
  }

  return `${pct >= 0 ? '+' : ''}${pct.toFixed(2)}%`;
}
