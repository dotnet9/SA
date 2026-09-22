import { lazy, Suspense } from 'react';
import { createBrowserRouter, Navigate, Outlet, useParams } from 'react-router';
import { AppShell } from '@/app/layout/AppShell';
import { MobileShell } from '@/app/mobile/MobileShell';
import { DefaultStockCode } from '@/app/nav';
import { StockPane } from '@/features/stock/StockPane';
import { readLastStock } from '@/app/useCurrentStock';

/**
 * 应用路由。
 *
 * 本应用**没有登录、没有权限控制**：所有页面直接可访问，不再有守卫、登录页、
 * 改密页与后台的用户/权限/安全三页。原先那套「无权限不渲染 + 未登录回登录页」的
 * 策略随之删除——留着只会在每个路由上多一层恒真的判断。
 *
 * 业务页面按路由分包：市场页会拉起 ECharts，若内联进首屏会把主包撑到 1MB 以上。
 */

/** 市场概览（含 ECharts，单独分包）。 */
const MarketPage = lazy(() =>
  import('@/features/market/MarketPage').then((module) => ({ default: module.MarketPage }))
);

/** 股票搜索。 */
const SearchPage = lazy(() =>
  import('@/features/search/SearchPage').then((module) => ({ default: module.SearchPage }))
);

/** 提醒规则。 */
const AlertsPage = lazy(() =>
  import('@/features/alerts/AlertsPage').then((module) => ({ default: module.AlertsPage }))
);

/** 通知中心。 */
const NotificationsPage = lazy(() =>
  import('@/features/alerts/NotificationsPage').then((module) => ({ default: module.NotificationsPage }))
);

/** 条件选股器。 */
const ScreenerPage = lazy(() =>
  import('@/features/screener/ScreenerPage').then((module) => ({ default: module.ScreenerPage }))
);

/** 运维：数据源监控（保留，与权限无关）。 */
const AdminDataSourcesPage = lazy(() =>
  import('@/features/admin/AdminDataSourcesPage').then((module) => ({ default: module.AdminDataSourcesPage }))
);

/** 个人设置。 */
const SettingsPage = lazy(() =>
  import('@/features/settings/SettingsPage').then((module) => ({ default: module.SettingsPage }))
);

/** 四种拓扑图总览。 */
const TopologyPage = lazy(() =>
  import('@/features/topology/TopologyPage').then((module) => ({ default: module.TopologyPage }))
);

/** 提醒形态预览。 */
const NotifyPreviewPage = lazy(() =>
  import('@/features/alerts/NotifyPreviewPage').then((module) => ({ default: module.NotifyPreviewPage }))
);

/** 行业景气度（规则引擎打分）。 */
const ProsperityPage = lazy(() =>
  import('@/features/analysis/AnalysisPages').then((module) => ({ default: module.ProsperityPage }))
);

/** 因果链与传导带宽（规则引擎推算）。 */
const CausalChainPage = lazy(() =>
  import('@/features/analysis/AnalysisPages').then((module) => ({ default: module.CausalChainPage }))
);

/** 自选股（含实时推送）。 */
const WatchlistPage = lazy(() =>
  import('@/features/watchlist/WatchlistPage').then((module) => ({ default: module.WatchlistPage }))
);

/** 移动端页面（与桌面端共用接口与口径，只换排布）。 */
const MobilePages = {
  Home: lazy(() => import('@/app/mobile/MobilePages').then((module) => ({ default: module.MobileHomePage }))),
  Watchlist: lazy(() => import('@/app/mobile/MobilePages').then((module) => ({ default: module.MobileWatchlistPage }))),
  Search: lazy(() => import('@/app/mobile/MobilePages').then((module) => ({ default: module.MobileSearchPage }))),
  More: lazy(() => import('@/app/mobile/MobilePages').then((module) => ({ default: module.MobileMorePage }))),
  Alerts: lazy(() => import('@/app/mobile/MobilePages').then((module) => ({ default: module.MobileAlertsPage }))),
  Notifications: lazy(() =>
    import('@/app/mobile/MobilePages').then((module) => ({ default: module.MobileNotificationsPage }))
  ),
  Screener: lazy(() => import('@/app/mobile/MobilePages').then((module) => ({ default: module.MobileScreenerPage }))),
  Settings: lazy(() => import('@/app/mobile/MobilePages').then((module) => ({ default: module.MobileSettingsPage }))),
  About: lazy(() => import('@/app/mobile/MobilePages').then((module) => ({ default: module.MobileAboutPage })))
};

/** 分包加载占位：沿用启动态样式，避免白屏。 */
function RouteFallback() {
  return <div className="sa-boot">正在载入页面…</div>;
}

/** 桌面外壳。 */
function DesktopShell() {
  return (
    <AppShell>
      <Outlet />
    </AppShell>
  );
}

/** 移动端外壳（`/m/*`）：同一套页面与接口，只换外层形态。 */
function MobileShellGuard() {
  return (
    <MobileShell>
      <Outlet />
    </MobileShell>
  );
}

/**
 * 移动端路由派发。
 *
 * 路由里用小写页面名（与 URL 段一致），这里映射到懒加载组件，
 * 避免为多个移动页面各写一段重复的 Suspense 包裹。
 */
type MobilePageName =
  | 'home'
  | 'search'
  | 'watchlist'
  | 'more'
  | 'alerts'
  | 'notifications'
  | 'screener'
  | 'settings'
  | 'about';

function MobileRoute({ page }: { page: MobilePageName }) {
  const Component =
    page === 'home'
      ? MobilePages.Home
      : page === 'search'
        ? MobilePages.Search
        : page === 'watchlist'
          ? MobilePages.Watchlist
          : page === 'more'
            ? MobilePages.More
            : page === 'alerts'
              ? MobilePages.Alerts
              : page === 'notifications'
                ? MobilePages.Notifications
                : page === 'screener'
                  ? MobilePages.Screener
                  : page === 'settings'
                    ? MobilePages.Settings
                    : MobilePages.About;

  return (
    <Suspense fallback={<RouteFallback />}>
      <Component />
    </Suspense>
  );
}

/**
 * 移动端个股页：直接复用桌面的个股区（9 个 Tab 横向滚动）。
 */
function MobileStockModulePage() {
  return (
    <Suspense fallback={<RouteFallback />}>
      <StockPane listPath={null} />
    </Suspense>
  );
}

/** `/stock` 落到上次查看的股票，避免出现没有代码的个股页。 */
function StockRedirect() {
  const code = useParams().code ?? readLastStock() ?? DefaultStockCode;
  return <Navigate to={`/stock/${code}`} replace />;
}

/**
 * 个股独立页（`/stock/:code`）：整页就是个股区，没有列表可返回。
 * 与大盘页 / 自选页共用同一份 StockPane，不存在两套实现。
 */
function StockModulePage() {
  return (
    <Suspense fallback={<RouteFallback />}>
      <StockPane listPath={null} />
    </Suspense>
  );
}

/** 大盘概况内联的个股区（`/market/:code`）：右侧整块替换列表，带「返回列表」。 */
function MarketStockPage() {
  return (
    <Suspense fallback={<RouteFallback />}>
      <StockPane listPath="/market" />
    </Suspense>
  );
}

/** 自选股内联的个股区（`/watchlist/:code`）。 */
function WatchlistStockPage() {
  return (
    <Suspense fallback={<RouteFallback />}>
      <StockPane listPath="/watchlist" />
    </Suspense>
  );
}

/** 未匹配到路由。 */
function NotFoundPage() {
  return (
    <>
      <div className="sa-pagehead">
        <div>
          <h1>页面不存在</h1>
          <div className="sub">请从左侧导航进入，或用 ⌘/Ctrl+K 搜索股票。</div>
        </div>
      </div>
    </>
  );
}

/**
 * 路由表。
 *
 * 所有路由都可直接访问——本应用不做权限控制。原先标注「需登录」的页面
 * （自选、选股器、提醒、通知、设置）现在同样开放；后台只剩「数据源监控」。
 */
export const router = createBrowserRouter([
  {
    path: '/m',
    element: <MobileShellGuard />,
    children: [
      { index: true, element: <MobileRoute page="home" /> },
      { path: 'index', element: <MobileRoute page="home" /> },
      { path: 'home', element: <MobileRoute page="home" /> },
      { path: 'search', element: <MobileRoute page="search" /> },
      { path: 'about', element: <MobileRoute page="about" /> },
      { path: 'stock/:code', element: <MobileStockModulePage /> },
      { path: 'stock/:code/:module', element: <MobileStockModulePage /> },
      { path: 'watchlist', element: <MobileRoute page="watchlist" /> },
      { path: 'more', element: <MobileRoute page="more" /> },
      { path: 'alerts', element: <MobileRoute page="alerts" /> },
      { path: 'notifications', element: <MobileRoute page="notifications" /> },
      { path: 'screener', element: <MobileRoute page="screener" /> },
      { path: 'settings', element: <MobileRoute page="settings" /> },
      // 提醒形态预览是移动端独有的页面，直接复用桌面实现
      {
        path: 'notify-preview',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <NotifyPreviewPage />
          </Suspense>
        )
      }
    ]
  },
  {
    path: '/',
    element: <DesktopShell />,
    children: [
      // 首页即大盘概况（没有登录页可跳）
      { index: true, element: <Navigate to="/market" replace /> },

      // ---- 行情与个股 ----
      {
        path: 'market',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <MarketPage />
          </Suspense>
        )
      },
      // 个股区内联在大盘概况里：点表格任一行进来，右侧整块替换列表
      { path: 'market/:code', element: <MarketStockPage /> },
      { path: 'market/:code/:module', element: <MarketStockPage /> },
      {
        path: 'search',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <SearchPage />
          </Suspense>
        )
      },
      { path: 'stock', element: <StockRedirect /> },
      { path: 'stock/:code', element: <StockModulePage /> },
      { path: 'stock/:code/:module', element: <StockModulePage /> },

      // ---- 自选（存浏览器本地）----
      {
        path: 'watchlist',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <WatchlistPage />
          </Suspense>
        )
      },
      { path: 'watchlist/:code', element: <WatchlistStockPage /> },
      { path: 'watchlist/:code/:module', element: <WatchlistStockPage /> },

      // ---- 分析工具 ----
      {
        path: 'screener',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <ScreenerPage />
          </Suspense>
        )
      },
      {
        path: 'topology',
        element: <Navigate to={`/topology/${readLastStock()}`} replace />
      },
      {
        path: 'topology/:code',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <TopologyPage />
          </Suspense>
        )
      },
      {
        path: 'prosperity',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <ProsperityPage />
          </Suspense>
        )
      },
      {
        path: 'stock/:code/causal',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <CausalChainPage />
          </Suspense>
        )
      },

      // ---- 提醒与通知 ----
      {
        path: 'alerts',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <AlertsPage />
          </Suspense>
        )
      },
      {
        path: 'notifications',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <NotificationsPage />
          </Suspense>
        )
      },
      {
        path: 'notify-preview',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <NotifyPreviewPage />
          </Suspense>
        )
      },

      // ---- 设置与运维 ----
      {
        path: 'settings',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <SettingsPage />
          </Suspense>
        )
      },
      {
        path: 'admin/datasource',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <AdminDataSourcesPage />
          </Suspense>
        )
      },
      { path: '*', element: <NotFoundPage /> }
    ]
  }
]);
