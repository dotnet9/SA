import type { ReactNode } from 'react';
import { MobileTabBar } from './MobileTabBar';
import { SideNav } from './SideNav';
import { TopBar } from './TopBar';

/**
 * 应用外壳。结构照抄原型每页的骨架：
 * `.sa-app` > 品牌区 + `.sa-topbar` + `.sa-sidenav` + `.sa-main > .sa-page`。
 * 网格与响应式全部由 components.css 提供，这里不重复定义。
 */
export function AppShell({ children, asOf }: { children: ReactNode; asOf?: string }) {
  return (
    <div className="sa-app">
      <div className="sa-brand">
        <div className="sa-logo">SA</div>
        <div className="sa-brand-text">
          <div className="sa-brand-name">股析</div>
          <div className="sa-brand-sub">Stock Analysis</div>
        </div>
      </div>

      <TopBar asOf={asOf} />
      <SideNav />

      <main className="sa-main">
        <div className="sa-page">{children}</div>
      </main>

      <MobileTabBar />
    </div>
  );
}
