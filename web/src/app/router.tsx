import { createBrowserRouter, Navigate, Outlet, useLocation, useParams } from 'react-router';
import { AppShell } from '@/app/layout/AppShell';
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
 */

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

/** 个股模块页：本批为占位，第 3–10 批逐域接入数据。 */
function StockModulePage() {
  const params = useParams();
  const code = params.code ?? DefaultStockCode;
  const module = params.module ?? 'overview';
  const name = StockModuleNames[module];

  if (!name) {
    return <NotFoundPage />;
  }

  const batches: Record<string, string> = {
    overview: '第 3 批（先接趋势卡，其余卡随各域补齐）',
    trend: '第 3 批',
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
      <PlaceholderPage title={`${name} · ${code}`} batch={batches[module] ?? '后续批次'} />
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
  {
    path: '/',
    element: <ProtectedShell />,
    children: [
      { index: true, element: <RootRedirect /> },
      {
        path: 'market',
        element: (
          <RequireFunctionPoint codes={['market.view']}>
            <PlaceholderPage title="市场概览" batch="第 2 批" />
          </RequireFunctionPoint>
        )
      },
      {
        path: 'search',
        element: (
          <RequireFunctionPoint codes={['stock.search']}>
            <PlaceholderPage title="股票搜索" batch="第 2 批" />
          </RequireFunctionPoint>
        )
      },
      {
        path: 'watchlist',
        element: (
          <RequireFunctionPoint codes={['watchlist.view']}>
            <PlaceholderPage title="自选股盯盘" batch="第 4 批" />
          </RequireFunctionPoint>
        )
      },
      {
        path: 'screener',
        element: (
          <RequireFunctionPoint codes={['screener.use']}>
            <PlaceholderPage title="条件选股器" batch="第 12 批" />
          </RequireFunctionPoint>
        )
      },
      {
        path: 'alerts',
        element: (
          <RequireFunctionPoint codes={['alert.manage']}>
            <PlaceholderPage title="提醒规则" batch="第 11 批" />
          </RequireFunctionPoint>
        )
      },
      {
        path: 'notifications',
        element: (
          <RequireFunctionPoint codes={['notify.view']}>
            <PlaceholderPage title="通知中心" batch="第 11 批" />
          </RequireFunctionPoint>
        )
      },
      {
        path: 'notify-preview',
        element: (
          <RequireFunctionPoint codes={['alert.manage']}>
            <PlaceholderPage title="提醒形态预览" batch="第 11 批" />
          </RequireFunctionPoint>
        )
      },
      { path: 'settings', element: <PlaceholderPage title="个人设置" batch="第 11 批" /> },
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
            <PlaceholderPage title="四种拓扑图总览" batch="第 9 批" />
          </RequireFunctionPoint>
        )
      },
      {
        path: 'admin/users',
        element: (
          <RequireFunctionPoint codes={['admin.users']}>
            <PlaceholderPage title="用户管理" batch="第 12 批" />
          </RequireFunctionPoint>
        )
      },
      {
        path: 'admin/permissions',
        element: (
          <RequireFunctionPoint codes={['admin.permissions']}>
            <PlaceholderPage title="角色与权限" batch="第 12 批" />
          </RequireFunctionPoint>
        )
      },
      {
        path: 'admin/datasource',
        element: (
          <RequireFunctionPoint codes={['admin.datasource']}>
            <PlaceholderPage title="数据源监控" batch="第 12 批" />
          </RequireFunctionPoint>
        )
      },
      {
        path: 'admin/security',
        element: (
          <RequireFunctionPoint codes={['admin.security']}>
            <PlaceholderPage title="登录与安全" batch="第 12 批" />
          </RequireFunctionPoint>
        )
      },
      { path: '*', element: <NotFoundPage /> }
    ]
  }
]);
