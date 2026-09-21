import type { ReactNode } from 'react';
import { Link, useLocation } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '@/providers/AuthProvider';
import { useRealtime } from '@/providers/RealtimeProvider';
import { fetchNotifications } from '@/features/alerts/api';
import { useSiteInfo } from '@/app/layout/useSiteInfo';

/**
 * 移动端外壳：紧凑页头 + 底部 Tab + 安全区内边距。
 *
 * 与桌面外壳（`AppShell`）的分工：桌面用左侧导航 + 页头工具条，移动端用底部 Tab。
 * 两者共用同一套 tokens 与卡片体系，因此这里的样式几乎全部复用现有类名，
 * 只补移动端特有的粘性页头与安全区（见 app.css 的 `.m-*` 与 `safe-area` 规则）。
 */
export function MobileShell({ children }: { children: ReactNode }) {
  const site = useSiteInfo();
  const realtime = useRealtime();
  const { me } = useAuth();
  const location = useLocation();

  const { data: notifications } = useQuery({
    queryKey: ['notifications', true],
    queryFn: () => fetchNotifications(true),
    staleTime: 30_000
  });

  return (
    <div className="m-shell">
      <header className="m-header">
        <Link className="m-brand" to="/m">
          <span className="m-logo">SA</span>
          <span className="m-brand-name">{site.name}</span>
        </Link>
        <span className="m-header-actions">
          <Link className="m-icon-btn" to="/m/search" aria-label="搜索">
            ⌕
          </Link>
          <Link className="m-icon-btn" to="/m/notifications" aria-label="通知">
            ◍
            {notifications && notifications.unread > 0 ? <span className="m-badge">{notifications.unread}</span> : null}
          </Link>
        </span>
      </header>

      {site.notice ? <div className="sa-notice m-notice">{site.notice}</div> : null}

      <main className="m-main">{children}</main>

      <nav className="m-tabbar">
        <MobileTab to="/m" icon="◈" text="市场" active={location.pathname === '/m'} />
        <MobileTab to="/m/watchlist" icon="★" text="自选" active={location.pathname.startsWith('/m/watchlist')} />
        <MobileTab to="/m/screener" icon="⚙" text="选股" active={location.pathname.startsWith('/m/screener')} />
        <MobileTab
          to="/m/notifications"
          icon="◍"
          text="通知"
          active={location.pathname.startsWith('/m/notifications')}
          badge={notifications?.unread}
        />
        <MobileTab to="/m/settings" icon="⚒" text="我的" active={location.pathname.startsWith('/m/settings')} />
      </nav>

      {/* 桌面壳里没有的东西：底部一行实时状态，手机上看不到页头工具条 */}
      <div className="m-statusbar">
        <span className={`tag ${realtime.status === 'connected' ? 'tag-up' : 'tag-outline'}`}>
          {realtime.status === 'connected' ? `实时已连接（${realtime.subscribed.length} 只）` : '实时未连接'}
        </span>
        <Link className="fs-11 t-3" to="/market">
          切到桌面版 →
        </Link>
      </div>

      <div className="m-tabbar-space" />

      {/* me 仅用于确认已登录：移动端不做权限提示，受限页面由路由守卫处理 */}
      {me ? null : null}
    </div>
  );
}

function MobileTab({
  to,
  icon,
  text,
  active,
  badge
}: {
  to: string;
  icon: string;
  text: string;
  active: boolean;
  badge?: number;
}) {
  return (
    <Link className={`m-tab ${active ? 'is-active' : ''}`} to={to}>
      <span className="m-tab-icon">
        {icon}
        {badge && badge > 0 ? <span className="m-badge">{badge > 99 ? '99+' : badge}</span> : null}
      </span>
      <span className="m-tab-text">{text}</span>
    </Link>
  );
}

/**
 * 移动端页头。移动端没有面包屑与工具条，因此用「返回 + 标题 + 右侧动作」三段式。
 */
export function MobilePageHead({
  title,
  sub,
  back,
  actions
}: {
  title: string;
  sub?: ReactNode;
  back?: { to: string; text: string };
  actions?: ReactNode;
}) {
  return (
    <div className="m-pagehead">
      {back ? (
        <Link className="m-back" to={back.to}>
          ‹ {back.text}
        </Link>
      ) : null}
      <div className="row-between wrap gap-2">
        <h1 className="m-title">{title}</h1>
        {actions}
      </div>
      {sub ? <div className="m-sub">{sub}</div> : null}
    </div>
  );
}
