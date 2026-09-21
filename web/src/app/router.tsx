import { lazy, Suspense } from 'react';
import { createBrowserRouter, Navigate, Outlet, useLocation, useMatches, useParams } from 'react-router';
import { AppShell } from '@/app/layout/AppShell';
import { MobileShell } from '@/app/mobile/MobileShell';
import { DefaultStockCode, landingPath, AnonymousLandingPath, StockModuleNames } from '@/app/nav';
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

/**
 * 路由元信息。挂在路由对象上，由 {@link Shell} 经 `useMatches()` 读取。
 */
interface RouteHandle {
  /** 该路由需要登录后才能访问。 */
  auth?: boolean;
}

/** 声明「需登录」的路由元信息（放在路由对象上，紧邻它约束的那条路由）。 */
const RequireLogin: RouteHandle = { auth: true };

/**
 * 当前匹配链上是否有路由要求登录。
 *
 * 用路由元信息而不是在守卫里硬编码路径清单：约束与被约束的路由写在一起，
 * 新增页面时不会出现「加了路由却忘了加进守卫名单」的漏网情况。
 */
function useRouteNeedsAuth(): boolean {
  const matches = useMatches();
  return matches.some((match) => (match.handle as RouteHandle | undefined)?.auth === true);
}

/**
 * 应用外壳（桌面与移动共用）。守卫规则：
 *
 * - 会话未恢复完 → 启动态；
 * - <b>仅当当前路由声明了 `auth` 且未登录</b> → 回登录页；
 *   公开路由（行情、搜索、个股、拓扑、景气度）匿名照常渲染，这正是「不登录也能看」的落点；
 * - 已登录但首登未改密 → 锁在改密页（与后端 MustChangePwd 语义一致）。
 *
 * 未登录时 `me` 为 null，`can()` 一律返回 false，因此个人数据相关的按钮会自然消失，
 * 不需要在各个页面里重复判断登录态。
 */
function Shell({ mobile }: { mobile: boolean }) {
  const { status, me } = useAuth();
  const location = useLocation();
  const needsAuth = useRouteNeedsAuth();

  if (status === 'loading') {
    return <div className="sa-boot">正在恢复会话…</div>;
  }

  const authenticated = status === 'authenticated' && me !== null;

  if (needsAuth && !authenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  if (authenticated && me.mustChangePwd && location.pathname !== '/change-password') {
    return <Navigate to="/change-password" replace />;
  }

  return mobile ? (
    <MobileShell>
      <Outlet />
    </MobileShell>
  ) : (
    <AppShell>
      <Outlet />
    </AppShell>
  );
}

/** 桌面外壳。 */
function DesktopShell() {
  return <Shell mobile={false} />;
}

/** 移动端外壳布局（`/m/*`）：守卫与桌面逐条同源，只换外层形态。 */
function MobileShellGuard() {
  return <Shell mobile={true} />;
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
 * 与桌面同样不套功能点守卫——个股模块是公开数据。
 */
function MobileStockModulePage() {
  const params = useParams();
  const module = params.module ?? 'overview';
  const name = StockModuleNames[module];

  if (!name) {
    return <NotFoundPage />;
  }

  return (
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
      ) : (
        <StockRatingPage />
      )}
    </Suspense>
  );
}

/**
 * 功能点守卫：缺少所需功能点时展示明确的拒绝页，而不是空白。
 *
 * 只用在「需登录」的路由上（`handle: RequireLogin`）——那时外壳已经确保登录，
 * 因此这里的 `me` 必然存在，判断的是「登录了但没有这个功能点」。
 * 未登录不会走到这里：外壳会先送回登录页。
 */
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

/**
 * 个股模块页。
 *
 * 不套功能点守卫：个股全部模块都是公开数据（后端 `AllowPublicRead`），
 * 前端再拦一道就会出现「接口放行但页面拒绝」的自相矛盾。
 */
function StockModulePage() {
  const params = useParams();
  const module = params.module ?? 'overview';
  const name = StockModuleNames[module];

  if (!name) {
    return <NotFoundPage />;
  }

  return (
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
      ) : (
        <StockRatingPage />
      )}
    </Suspense>
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

/** 根路由：已登录进落地页，未登录进公开首页（市场概览）。 */
function RootRedirect() {
  const { status, me } = useAuth();

  if (status === 'loading') {
    return <div className="sa-boot">正在恢复会话…</div>;
  }

  if (status !== 'authenticated' || !me) {
    return <Navigate to={AnonymousLandingPath} replace />;
  }

  return <Navigate to={landingPath(me.functionPoints)} replace />;
}

/**
 * 路由表。
 *
 * 公开（无需登录）与需登录的分界由每条路由自己的 `handle: RequireLogin` 声明，
 * 与后端 `PublicEndpointExtensions` 的分区一一对应：
 *
 * - 公开：市场概览、搜索、个股全部模块、拓扑图、行业景气度、因果链、站点信息；
 * - 需登录：自选、选股器、提醒、通知、个人设置、后台。
 *
 * 公开路由<b>不套</b> `RequireFunctionPoint`：对应功能点在后端已放开（公开数据），
 * 前端再拦就会与接口行为不一致。
 */
export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/change-password', element: <ChangePasswordPage /> },
  /**
   * 移动端路由（对应原型 `design/app/` 的 18 个页面）。
   *
   * 与桌面端共用同一个守卫与同一套接口，只是外壳换成底部 Tab；
   * 公开页（首页、搜索、个股、关于）未登录可直接进入，其余回登录页。
   */
  {
    path: '/m',
    element: <MobileShellGuard />,
    children: [
      { index: true, element: <MobileRoute page="home" /> },
      // 原型里有 index.html（移动入口）与 home.html 两个页面，这里都指向首页
      { path: 'index', element: <MobileRoute page="home" /> },
      { path: 'home', element: <MobileRoute page="home" /> },
      { path: 'search', element: <MobileRoute page="search" /> },
      { path: 'about', element: <MobileRoute page="about" /> },
      { path: 'stock/:code', element: <MobileStockModulePage /> },
      { path: 'stock/:code/:module', element: <MobileStockModulePage /> },
      { path: 'watchlist', element: <MobileRoute page="watchlist" />, handle: RequireLogin },
      { path: 'alerts', element: <MobileRoute page="alerts" />, handle: RequireLogin },
      { path: 'notifications', element: <MobileRoute page="notifications" />, handle: RequireLogin },
      { path: 'screener', element: <MobileRoute page="screener" />, handle: RequireLogin },
      { path: 'settings', element: <MobileRoute page="settings" />, handle: RequireLogin },
      // 提醒形态预览是移动端独有的页面（FR-ALERT-09），直接复用桌面实现
      {
        path: 'notify-preview',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <NotifyPreviewPage />
          </Suspense>
        ),
        handle: RequireLogin
      }
    ]
  },
  {
    path: '/',
    element: <DesktopShell />,
    children: [
      { index: true, element: <RootRedirect /> },

      // ---- 公开：行情、搜索、个股、拓扑、景气度 ----
      {
        path: 'market',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <MarketPage />
          </Suspense>
        )
      },
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

      // ---- 需登录：自选、选股、提醒、通知、设置、后台 ----
      {
        path: 'watchlist',
        element: (
          <RequireFunctionPoint codes={['watchlist.view']}>
            <Suspense fallback={<RouteFallback />}>
              <WatchlistPage />
            </Suspense>
          </RequireFunctionPoint>
        ),
        handle: RequireLogin
      },
      {
        path: 'screener',
        element: (
          <RequireFunctionPoint codes={['screener.use']}>
            <Suspense fallback={<RouteFallback />}>
              <ScreenerPage />
            </Suspense>
          </RequireFunctionPoint>
        ),
        handle: RequireLogin
      },
      {
        path: 'alerts',
        element: (
          <RequireFunctionPoint codes={['alert.manage']}>
            <Suspense fallback={<RouteFallback />}>
              <AlertsPage />
            </Suspense>
          </RequireFunctionPoint>
        ),
        handle: RequireLogin
      },
      {
        path: 'notifications',
        element: (
          <RequireFunctionPoint codes={['notify.view']}>
            <Suspense fallback={<RouteFallback />}>
              <NotificationsPage />
            </Suspense>
          </RequireFunctionPoint>
        ),
        handle: RequireLogin
      },
      {
        path: 'notify-preview',
        element: (
          <RequireFunctionPoint codes={['alert.manage']}>
            <Suspense fallback={<RouteFallback />}>
              <NotifyPreviewPage />
            </Suspense>
          </RequireFunctionPoint>
        ),
        handle: RequireLogin
      },
      {
        path: 'settings',
        element: (
          <Suspense fallback={<RouteFallback />}>
            <SettingsPage />
          </Suspense>
        ),
        handle: RequireLogin
      },
      {
        path: 'admin/users',
        element: (
          <RequireFunctionPoint codes={['admin.users']}>
            <Suspense fallback={<RouteFallback />}>
              <AdminUsersPage />
            </Suspense>
          </RequireFunctionPoint>
        ),
        handle: RequireLogin
      },
      {
        path: 'admin/permissions',
        element: (
          <RequireFunctionPoint codes={['admin.permissions']}>
            <Suspense fallback={<RouteFallback />}>
              <AdminUsersPage />
            </Suspense>
          </RequireFunctionPoint>
        ),
        handle: RequireLogin
      },
      {
        path: 'admin/datasource',
        element: (
          <RequireFunctionPoint codes={['admin.datasource']}>
            <Suspense fallback={<RouteFallback />}>
              <AdminDataSourcesPage />
            </Suspense>
          </RequireFunctionPoint>
        ),
        handle: RequireLogin
      },
      {
        path: 'admin/security',
        element: (
          <RequireFunctionPoint codes={['admin.security']}>
            <Suspense fallback={<RouteFallback />}>
              <AdminSecurityPage />
            </Suspense>
          </RequireFunctionPoint>
        ),
        handle: RequireLogin
      },
      { path: '*', element: <NotFoundPage /> }
    ]
  }
]);
