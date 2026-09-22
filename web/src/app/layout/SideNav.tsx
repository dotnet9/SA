import { useEffect, useState } from 'react';
import { NavLink, useLocation } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { MoreNav, type NavItem } from '@/app/nav';
import { fetchMarketQuotes } from '@/features/market/api';
import { useWatchlist } from '@/features/watchlist/WatchlistProvider';
import { useLiveQuote } from '@/providers/RealtimeProvider';
import { readString, writeString } from '@/lib/storage';

/** 「更多」折叠区的本机记忆键（布局偏好，不该每次刷新都重置）。 */
const MoreOpenKey = 'sa.navmore';

/** 自选股组的展开状态。默认展开：用户要的就是「加入后直接显示在左侧」。 */
const WatchOpenKey = 'sa.navwatch';

/**
 * 左侧导航：**只有两项**——大盘概况、自选股。
 *
 * 自选股不是单纯的链接，而是**展开成个股列表**（用户要求）：
 * 从搜索面板或任意列表加入自选后，个股立刻出现在这一组下面；
 * 点其中一只，右侧（内容区）就是它的详情页。组头「自选股」进的是自选列表页（管理用）。
 *
 * 样式复用原型的 `.sa-navgroup`（组标签）与 `.sa-navsub`（缩进子列表），
 * 只补了「组头带折叠箭头」「个股行名称省略 + 涨跌幅右对齐」这几条。
 *
 * 其余功能收进底部可折叠的「更多」区（默认收起）。
 * 本应用不做权限控制，因此没有「按角色裁剪菜单」与「未登录锁定」两套分支。
 */
export function SideNav() {
  const location = useLocation();
  const watchlist = useWatchlist();
  const [moreOpen, setMoreOpen] = useState(() => readString(MoreOpenKey, '0') === '1');
  const [watchOpen, setWatchOpen] = useState(() => readString(WatchOpenKey, '1') !== '0');

  useEffect(() => {
    writeString(MoreOpenKey, moreOpen ? '1' : '0');
  }, [moreOpen]);

  useEffect(() => {
    writeString(WatchOpenKey, watchOpen ? '1' : '0');
  }, [watchOpen]);

  // 当前页落在「更多」里时自动展开，否则用户会看不到自己在哪里
  useEffect(() => {
    if (MoreNav.some((item) => isMoreItemActive(item, location.pathname))) {
      setMoreOpen(true);
    }
  }, [location.pathname]);

  // 看个股详情时自选组自动展开（要能看见当前这只在组里的位置）
  useEffect(() => {
    if (location.pathname.startsWith('/stock/') || location.pathname.startsWith('/watchlist/')) {
      setWatchOpen(true);
    }
  }, [location.pathname]);

  const codes = watchlist.codes;
  const quotes = useQuery({
    queryKey: ['sidebar', 'watchlist-quotes', codes.join(',')],
    queryFn: () => fetchMarketQuotes(codes),
    enabled: codes.length > 0,
    staleTime: 30_000
  });

  const byCode = new Map((quotes.data ?? []).map((row) => [row.code, row]));
  const activeCode = activeStockCode(location.pathname);

  return (
    <nav className="sa-sidenav">
      <NavLink
        to="/market"
        title="大盘概况"
        className={`sa-navitem${isMarketActive(location.pathname, activeCode, codes) ? ' is-active' : ''}`}
      >
        <span className="ni-icon">▦</span>
        <span className="ni-text">大盘概况</span>
      </NavLink>

      {/* ---- 自选股：组头 + 展开的个股列表 ---- */}
      <div className="sa-navgroup-head">
        <NavLink
          to="/watchlist"
          title="自选股（打开列表页管理）"
          className={`sa-navgroup${isWatchlistActive(location.pathname) ? ' is-active' : ''}`}
        >
          自选股
          <span className="ni-count">{codes.length}</span>
        </NavLink>
        <button
          type="button"
          className="sa-navgroup-toggle"
          title={watchOpen ? '收起自选股' : '展开自选股'}
          aria-expanded={watchOpen}
          onClick={() => setWatchOpen((previous) => !previous)}
        >
          {watchOpen ? '▾' : '▸'}
        </button>
      </div>

      {watchOpen ? (
        <div className="sa-navsub">
          {codes.length === 0 ? (
            <div className="sa-navsub-empty">还没有自选股</div>
          ) : (
            codes.map((code) => (
              <WatchStockItem
                key={code}
                code={code}
                name={byCode.get(code)?.name ?? code}
                snapshotPct={byCode.get(code)?.pct ?? null}
                active={activeCode === code}
              />
            ))
          )}
        </div>
      ) : null}

      <details className="sa-nav-more" open={moreOpen}>
        <summary
          className="sa-nav-more-sum"
          onClick={(event) => {
            event.preventDefault();
            setMoreOpen((previous) => !previous);
          }}
        >
          <span>更多</span>
        </summary>
        {moreOpen ? (
          <div className="sa-nav-more-body">
            {MoreNav.map((item) => (
              <NavLink
                key={item.key}
                to={item.path}
                title={item.text}
                className={`sa-navitem${isMoreItemActive(item, location.pathname) ? ' is-active' : ''}`}
              >
                <span className="ni-icon">{item.icon}</span>
                <span className="ni-text">{item.text}</span>
              </NavLink>
            ))}
          </div>
        ) : null}
      </details>
    </nav>
  );
}

/** 自选组里的一只股票：名称 + 涨跌幅（推送值优先于批量快照）。 */
function WatchStockItem({
  code,
  name,
  snapshotPct,
  active
}: {
  code: string;
  name: string;
  snapshotPct: number | null;
  active: boolean;
}) {
  const live = useLiveQuote(code);
  const pct = live?.pct ?? snapshotPct;
  const tone = pct === null || pct === undefined || pct === 0 ? 'is-flat' : pct > 0 ? 'is-up' : 'is-down';

  return (
    <NavLink to={`/stock/${code}`} title={`${name} ${code}`} className={`sa-navitem${active ? ' is-active' : ''}`}>
      <span className="ns-name">{name}</span>
      <span className={`ns-pct mono ${tone}`}>
        {pct === null || pct === undefined ? '—' : `${pct >= 0 ? '+' : ''}${pct.toFixed(2)}%`}
      </span>
    </NavLink>
  );
}

/** 当前打开的个股代码（`/stock/:code` 与 `/watchlist/:code` 都算）。 */
function activeStockCode(pathname: string): string | null {
  const m = /^\/(?:stock|watchlist)\/([^/?#]+)/.exec(pathname);
  return m ? m[1] : null;
}

/**
 * 大盘概况的高亮。
 *
 * `/market/:code`（从大盘表点进去的个股）算在大盘下面；
 * 但若这只股票在自选里，则由自选组高亮——同一只票不该同时点亮两处。
 */
function isMarketActive(pathname: string, activeCode: string | null, watchCodes: readonly string[]): boolean {
  if (pathname === '/market' || pathname.startsWith('/market/')) {
    return true;
  }

  if (pathname.startsWith('/stock/') && activeCode) {
    return !watchCodes.includes(activeCode);
  }

  return false;
}

/** 自选股组头：自选列表页与自选内的个股详情都算这一组。 */
function isWatchlistActive(pathname: string): boolean {
  return pathname === '/watchlist' || pathname.startsWith('/watchlist/');
}

/** 「更多」区里的项：路径匹配即可（这些页面没有子路由）。 */
function isMoreItemActive(item: NavItem, pathname: string): boolean {
  return pathname === item.path || pathname.startsWith(`${item.path}/`);
}
