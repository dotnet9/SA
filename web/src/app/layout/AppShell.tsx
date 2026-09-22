import type { ReactNode } from 'react';
import { MobileTabBar } from './MobileTabBar';
import { SideNav } from './SideNav';
import { TopBar } from './TopBar';
import { useSiteInfo } from './useSiteInfo';

/**
 * 应用外壳。结构照抄原型每页的骨架：
 * `.sa-app` > 品牌区 + `.sa-topbar` + `.sa-sidenav` + `.sa-main > .sa-page`。
 *
 * 品牌名与全局公告来自后台「系统设置」（`/api/system/site`）：
 * 这样那两个设置项才真正生效，而不是「改了没反应」的开关。
 */
export function AppShell({ children, asOf }: { children: ReactNode; asOf?: string }) {
  const site = useSiteInfo();

  return (
    <div className="sa-app">
      <div className="sa-brand">
        <div className="sa-logo">SA</div>
        <div className="sa-brand-text">
          <div className="sa-brand-name">{site.name}</div>
          <div className="sa-brand-sub">Stock Analysis</div>
        </div>
      </div>

      <TopBar asOf={asOf} />
      <SideNav />

      <main className="sa-main">
        {site.notice ? (
          <div className="sa-notice" role="status">
            {site.notice}
          </div>
        ) : null}
        <div className="sa-page">{children}</div>
      </main>

      <MobileTabBar />
    </div>
  );
}
