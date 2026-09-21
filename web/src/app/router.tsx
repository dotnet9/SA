import { lazy, Suspense } from 'react';
import { createBrowserRouter, Navigate, Outlet, useLocation, useParams } from 'react-router';
import { AppShell } from '@/app/layout/AppShell';
import { MobileShell } from '@/app/mobile/MobileShell';
import { DefaultStockCode, landingPath, StockModuleFunctionPoints, StockModuleNames } from '@/app/nav';
import { PlaceholderPage } from '@/app/PlaceholderPage';
import { readLastStock } from '@/app/useCurrentStock';
import { ChangePasswordPage } from '@/features/auth/pages/ChangePasswordPage';
import { LoginPage } from '@/features/auth/pages/LoginPage';
import { useAuth } from '@/providers/AuthProvider';

/**
 * 应用路由。
 *
 * 守卫策略与概要设计 §2.1 一致：无权限的路由不渲染内容（而不是先渲染再报错），
 * 未登录一律回登录页，首登未改密则锁在改密页。
 *
 * 业务页面按路由分包（详细设计 §12「前端按路由分包」）：市场页会拉起 ECharts，
 * 若内联进首屏会把主包撑到 1MB 以上，因此这两页走动态 import。
 */

/** 市场概览（含 ECharts，单独分包）。 */
const MarketPage = lazy(() =>
  import('@/features/market/MarketPage').then((module) => ({ default: module.MarketPage }))
);

/** 股票搜索。 */
const SearchPage = lazy(() =>
  import('@/features/search/SearchPage').then((module) => ({ default: module.SearchPage }))
);

/** 个股总览（8 张摘要卡）。 */
const StockOverviewPage = lazy(() =>
  import('@/features/stock/StockOverviewPage').then((module) => ({ default: module.StockOverviewPage }))
);

/** 趋势与价格结构（K 线，分包体积最大）。 */
const StockTrendPage = lazy(() =>
  import('@/features/stock/StockTrendPage').then((module) => ({ default: module.StockTrendPage }))
);

/** 投资与股权结构。 */
const StockEquityPage = lazy(() =>
  import('@/features/equity/StockEquityPage').then((module) => ({ default: module.StockEquityPage }))
);

/** 资金面与筹码。 */
const StockCapitalPage = lazy(() =>
  import('@/features/capital/StockCapitalPage').then((module) => ({ default: module.StockCapitalPage }))
);

/** 行业与同业对比。 */
const StockIndustryPage = lazy(() =>
  import('@/features/industry/StockIndustryPage').then((module) => ({ default: module.StockIndustryPage }))
);

/** 事件与影响（含四种拓扑图）。 */
const StockEventsPage = lazy(() =>
  import('@/features/events/StockEventsPage').then((module) => ({ default: module.StockEventsPage }))
);

/** 风险与舆情监控。 */
const StockRiskPage = lazy(() =>
  import('@/features/risk/StockRiskPage').then((module) => ({ default: module.StockRiskPage }))
);

/** 机构评级与预测。 */
const StockRatingPage = lazy(() =>
  import('@/features/rating/StockRatingPage').then((module) => ({ default: module.StockRatingPage }))
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

/** 后台：数据源监控。 */
const AdminDataSourcesPage = lazy(() =>
  import('@/features/admin/AdminDataSourcesPage').then((module) => ({ default: module.AdminDataSourcesPage }))
);

/** 后台：用户与权限。 */
const AdminUsersPage = lazy(() =>
  import('@/features/admin/AdminUsersPage').then((module) => ({ default: module.AdminUsersPage }))
);

/** 后台：登录与安全。 */
const AdminSecurityPage = lazy(() =>
  import('@/features/admin/AdminSecurityPage').then((module) => ({ default: module.AdminSecurityPage }))
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

/** 移动端页面（与桌面端共用接口与口径，只换排布）。 */
const MobilePages = {
  Home: lazy(() => import('@/app/mobile/MobilePages').then((module) => ({ default: module.MobileHomePage }))),
  Watchlist: lazy(() => import('@/app/mobile/MobilePages').then((module) => ({ default: module.MobileWatchlistPage }))),
  Search: lazy(() => import('@/app/mobile/MobilePages').then((module) => ({ default: module.MobileSearchPage }))),
  Alerts: lazy(() => import('@/app/mobile/MobilePages').then((module) => ({ default: module.MobileAlertsPage }))),
  Notifications: lazy(() =>
    import('@/app/mobile/MobilePages').then((module) => ({ default: module.MobileNotificationsPage }))
  ),
  Screener: lazy(() => import('@/app/mobile/MobilePages').then((module) => ({ default: module.MobileScreenerPage }))),
  Settings: lazy(() => import('@/app/mobile/MobilePages').then((module) => ({ default: module.MobileSettingsPage }))),
  About: lazy(() => import('@/app/mobile/MobilePages').then((module) => ({ default: module.MobileAboutPage })))
};

/** 自选股（含实时推送）。 */
const WatchlistPage = lazy(() =>
  import('@/features/watchlist/WatchlistPage').then((module) => ({ default: module.WatchlistPage }))
);

/** 盈利与财务表现。 */
const StockFinancePage = lazy(() =>
  import('@/features/finance/StockFinancePage').then((module) => ({ default: module.StockFinancePage }))
);

/** 分包加载占位：沿用启动态样式，避免白屏。 */
function RouteFallback() {
  return <div className="sa-boot">正在载入页面…</div>;
}

/** 会话恢复中或未登录时的处理。 */
function ProtectedShell() {
  const { status, me } = useAuth();
  const location = useLocation();

  if (status === 'loading') {
    return <div className="sa-boot">正在恢复会话…</div>;
  }

  if (status !== 'authenticated' || !me) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  // 强制改密：除改密页外一律重定向（与后端 MustChangePwd 语义一致）
  if (me.mustChangePwd && location.pathname !== '/change-password') {
    return <Navigate to="/change-password" replace />;
  }

  return (
    <AppShell>
      <Outlet />
    </AppShell>
  );
}

/**
 * 移动端外壳布局（`/m/*`）。
 *
 * 守卫与桌面壳逐条一致（登录态、强制改密），只是外层容器换成底部 Tab 的形态。
 * 两侧的判断必须保持同源语义：移动端不该变成「另一套权限体系」。
 */
function ProtectedMobileShell() {
  const { status, me } = useAuth();
  const location = useLocation();

  if (status === 'loading') {
    return <div className="sa-boot">正在恢复会话…</div>;
  }

  if (status !== 'authenticated' || !me) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  if (me.mustChangePwd && location.pathname !== '/change-password') {
    return <Navigate to="/change-password" replace />;
  }

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
 * 避免为 8 个移动页面各写一段重复的 Suspense 包裹。
 */
type MobilePageName = 'home' | 'search' | 'watchlist' | 'alerts' | 'notifications' | 'screener' | 'settings' | 'about';

function MobileRoute({ page }: { page: MobilePageName }) {
  const Component =
    page === 'home'
      ? MobilePages.Home
      : page === 'search'
        ? MobilePages.Search
        : page === 'watchlist'
          ? MobilePages.Watchlist
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
 * 移动端个股模块页。
 *
 * 直接复用桌面组件：这些页面本身就是卡片式布局（K 线、指标、表格都是自适应的），
 * 为移动端再写一遍只会产生两份需要同步维护的实现。
 */
function MobileStockModulePage() {
  const params = useParams();
  const code = params.code ?? DefaultStockCode;
  const module = params.module ?? 'overview';
  const name = StockModuleNames[module];

  if (!name) {
    return <NotFoundPage />;
  }

  return (
    <RequireFunctionPoint codes={[StockModuleFunctionPoints[module] ?? 'stock.trend']}>
      <Suspense fallback={<RouteFallback />}>
        {module === 'overview' ? (
          <StockOverviewPage />
        ) : module === 'trend' ? (
          <StockTrendPage />
        ) : module === 'finance' ? (
          <StockFinancePage />
        ) : module === 'equity' ? (
          <StockEquityPage />
        ) : module === 'capital' ? (
          <StockCapitalPage />
        ) : module === 'industry' ? (
          <StockIndustryPage />
        ) : module === 'events' ? (
          <StockEventsPage />
        ) : module === 'causal' ? (
          <CausalChainPage />
        ) : module === 'risk' ? (
          <StockRiskPage />
        ) : module === 'rating' ? (
          <StockRatingPage />
        ) : (
          <PlaceholderPage title={`${name} · ${code}`} batch="后续批次" />
        )}
      </Suspense>
    </RequireFunctionPoint>
  );
}

/** 功能点守卫：缺少所需功能点时展示明确的拒绝页，而不是空白。 */
function RequireFunctionPoint({ codes, children }: { codes: string[]; children: React.ReactNode }) {
  const { can, me } = useAuth();

  if (!can(...codes)) {
    return (
      <>
        <div className="sa-pagehead">
          <div>
            <h1>无访问权限</h1>
            <div className="sub">当前角色「{me?.roleName}」缺少所需功能点</div>
          </div>
        </div>
        <div className="placeholder-note">
          <div>
            需要功能点：
            {codes.map((code) => (
              <span key={code} className="tag tag-danger" style={{ marginRight: 6 }}>
                {code}
              </span>
            ))}
          </div>
          <div>请联系管理员在「角色与权限」中为该角色开启对应功能点。</div>
        </div>
      </>
    );
  }

  return <>{children}</>;
}

/** `/stock` 落到上次查看的股票，避免出现没有代码的个股页。 */
function StockRedirect() {
  const code = useParams().code ?? readLastStock() ?? DefaultStockCode;
  return <Navigate to={`/stock/${code}`} replace />;
}

/** 个股模块页：按模块分派到已实现的页面，未实现的仍显示占位并说明批次。 */
function StockModulePage() {
  const params = useParams();
  const code = params.code ?? DefaultStockCode;
  const module = params.module ?? 'overview';
  const name = StockModuleNames[module];

  if (!name) {
    return <NotFoundPage />;
  }

  const batches: Record<string, string> = {
    finance: '第 5 批',
    equity: '第 6 批',
    capital: '第 7 批',
    industry: '第 8 批',
    events: '第 9 批',
    risk: '第 10 批',
    rating: '第 10 批'
  };

  return (
    <RequireFunctionPoint codes={[StockModuleFunctionPoints[module] ?? 'stock.trend']}>
      <Suspense fallback={<RouteFallback />}>
        {module === 'overview' ? (
          <StockOverviewPage />
        ) : module === 'trend' ? (
          <StockTrendPage />
        ) : module === 'finance' ? (
          <StockFinancePage />
        ) : module === 'equity' ? (
          <StockEquityPage />
        ) : module === 'capital' ? (
          <StockCapitalPage />
        ) : module === 'industry' ? (
          <StockIndustryPage />
        ) : module === 'events' ? (
          <StockEventsPage />
        ) : module === 'causal' ? (
          <CausalChainPage />
        ) : module === 'risk' ? (
          <StockRiskPage />
        ) : module === 'rating' ? (
          <StockRatingPage />
        ) : (
          <PlaceholderPage title={`${name} · ${code}`} batch={batches[module] ?? '后续批次'} />
        )}
      </Suspense>
    </RequireFunctionPoint>
  );
}

/** 未匹配到路由。 */
function NotFoundPage() {
  const location = useLocation();

  return (
    <>
      <div className="sa-pagehead">
        <div>
          <h1>页面不存在</h1>
          <div className="sub">未匹配的路径：{location.pathname}</div>
        </div>
      </div>
      <div className="placeholder-note">
        <div>请从左侧导航进入，或用 ⌘/Ctrl+K 搜索股票。</div>
      </div>
    </>
  );
}

/** 根路由：已登录进落地页，未登录由 ProtectedShell 送回登录页。 */
function RootRedirect() {
  const { status, me } = useAuth();

  if (status === 'loading') {
    return <div className="sa-boot">正在恢复会话…</div>;
  }

  if (status !== 'authenticated' || !me) {
    return <Navigate to="/login" replace />;
  }

  return <Navigate to={landingPath(me.functionPoints)} replace />;
}

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/change-password', element: <ChangePasswordPage /> },
  /**
   * 移动端路由（对应原型 `design/app/` 的 18 个页面）。
   *
   * 与桌面端共用登录与强制改密的守卫，只是外壳换成底部 Tab；
   * 数据接口与口径完全相同，因此不存在「移动端看到的是另一套数字」。
   */
  {
    path: '/m',
    element: <ProtectedMobileShell />,
    children: [
      { index: true, element: <MobileRoute page="home" /> },
      // 原型里有 index.html（移动入口）与 home.html 两个页面，这里都指向首页
      { path: 'index', element: <MobileRoute page="home" /> },
      { path: 'home', element: <MobileRoute page="home" /> },
      { path: 'search', element: <MobileRoute page="search" /> },
      { path: 'watchlist', element: <MobileRoute page="watchlist" /> },
      { path: 'alerts', element: <MobileRoute page="alerts" /> },
      { path: 'notifications', element: <MobileRoute page="notifications" /> },
      { path: 'screener', element: <MobileRoute page="screener" /> },
      { path: 'settings', element: <MobileRoute page="settings" /> },
      { path: 'about', element: <MobileRoute page="about" /> },
      // 提醒形态预览是移动端独有的页面（FR-ALERT-09），直接复用桌面实现
      {
        path: 'notify-preview',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <NotifyPreviewPage />
          </Suspense>
        )
      },
      { path: 'stock/:code', element: <MobileStockModulePage /> },
      { path: 'stock/:code/:module', element: <MobileStockModulePage /> }
    ]
  },
  {
    path: '/',
    element: <ProtectedShell />,
    children: [
      { index: true, element: <RootRedirect /> },
      {
        path: 'market',
        element: (
          <RequireFunctionPoint codes={['market.view']}>
            <Suspense fallback={<RouteFallback />}>
              <MarketPage />
            </Suspense>
          </RequireFunctionPoint>
        )
      },
      {
        path: 'search',
        element: (
          <RequireFunctionPoint codes={['stock.search']}>
            <Suspense fallback={<RouteFallback />}>
              <SearchPage />
            </Suspense>
          </RequireFunctionPoint>
        )
      },
      {
        path: 'watchlist',
        element: (
          <RequireFunctionPoint codes={['watchlist.view']}>
            <Suspense fallback={<RouteFallback />}>
              <WatchlistPage />
            </Suspense>
          </RequireFunctionPoint>
        )
      },
      {
        path: 'screener',
        element: (
          <RequireFunctionPoint codes={['screener.use']}>
            <Suspense fallback={<RouteFallback />}>
              <ScreenerPage />
            </Suspense>
          </RequireFunctionPoint>
        )
      },
      {
        path: 'alerts',
        element: (
          <RequireFunctionPoint codes={['alert.manage']}>
            <Suspense fallback={<RouteFallback />}>
              <AlertsPage />
            </Suspense>
          </RequireFunctionPoint>
        )
      },
      {
        path: 'notifications',
        element: (
          <RequireFunctionPoint codes={['notify.view']}>
            <Suspense fallback={<RouteFallback />}>
              <NotificationsPage />
            </Suspense>
          </RequireFunctionPoint>
        )
      },
      {
        path: 'notify-preview',
        element: (
          <RequireFunctionPoint codes={['alert.manage']}>
            <Suspense fallback={<RouteFallback />}>
              <NotifyPreviewPage />
            </Suspense>
          </RequireFunctionPoint>
        )
      },
      {
        path: 'settings',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <SettingsPage />
          </Suspense>
        )
      },
      { path: 'stock', element: <StockRedirect /> },
      { path: 'stock/:code', element: <StockModulePage /> },
      { path: 'stock/:code/:module', element: <StockModulePage /> },
      {
        path: 'topology',
        element: <Navigate to={`/topology/${readLastStock()}`} replace />
      },
      {
        path: 'topology/:code',
        element: (
          <RequireFunctionPoint codes={['topology.view']}>
            <Suspense fallback={<RouteFallback />}>
              <TopologyPage />
            </Suspense>
          </RequireFunctionPoint>
        )
      },
      {
        path: 'prosperity',
        element: (
          <RequireFunctionPoint codes={['stock.industry']}>
            <Suspense fallback={<RouteFallback />}>
              <ProsperityPage />
            </Suspense>
          </RequireFunctionPoint>
        )
      },
      {
        path: 'stock/:code/causal',
        element: (
          <RequireFunctionPoint codes={['stock.trend']}>
            <Suspense fallback={<RouteFallback />}>
              <CausalChainPage />
            </Suspense>
          </RequireFunctionPoint>
        )
      },
      {
        path: 'admin/users',
        element: (
          <RequireFunctionPoint codes={['admin.users']}>
            <Suspense fallback={<RouteFallback />}>
              <AdminUsersPage />
            </Suspense>
          </RequireFunctionPoint>
        )
      },
      {
        path: 'admin/permissions',
        element: (
          <RequireFunctionPoint codes={['admin.permissions']}>
            <Suspense fallback={<RouteFallback />}>
              <AdminUsersPage />
            </Suspense>
          </RequireFunctionPoint>
        )
      },
      {
        path: 'admin/datasource',
        element: (
          <RequireFunctionPoint codes={['admin.datasource']}>
            <Suspense fallback={<RouteFallback />}>
              <AdminDataSourcesPage />
            </Suspense>
          </RequireFunctionPoint>
        )
      },
      {
        path: 'admin/security',
        element: (
          <RequireFunctionPoint codes={['admin.security']}>
            <Suspense fallback={<RouteFallback />}>
              <AdminSecurityPage />
            </Suspense>
          </RequireFunctionPoint>
        )
      },
      { path: '*', element: <NotFoundPage /> }
    ]
  }
]);
