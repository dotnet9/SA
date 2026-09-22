import { useState, type ReactNode } from 'react';
import { MobileTabBar } from './MobileTabBar';
import { SideNav } from './SideNav';
import { TopBar } from './TopBar';
import { WatchStrip } from './WatchStrip';
import { useSiteInfo } from './useSiteInfo';
import { readString, StorageKeys, writeString } from '@/lib/storage';

/**
 * 应用外壳。结构照抄原型每页的骨架：
 * `.sa-app` > 品牌区 + `.sa-topbar` + `.sa-sidenav` + `.sa-main > .sa-page`。
 * 网格与响应式由 components.css 提供，这里只追加「自选横条」这一行（见 app.css 第 11 节）。
 *
 * 品牌名与全局公告来自后台「系统设置」（`/api/system/site`）：
 * 这样那两个设置项才真正生效，而不是「改了没反应」的开关。
 */
export function AppShell({ children, asOf }: { children: ReactNode; asOf?: string }) {
  const site = useSiteInfo();

  // 横条的展开/收起状态持久化：这是用户对布局的偏好，不该每次刷新都重置
  const [stripCollapsed, setStripCollapsed] = useState(
    () => readString(StorageKeys.WatchStripCollapsed, '0') === '1'
  );

  const toggleStrip = () => {
    setStripCollapsed((previous) => {
      const next = !previous;
      writeString(StorageKeys.WatchStripCollapsed, next ? '1' : '0');
      return next;
    });
  };

  return (
    <div className={`sa-app has-strip${stripCollapsed ? ' is-strip-collapsed' : ''}`}>
      <div className="sa-brand">
        <div className="sa-logo">SA</div>
        <div className="sa-brand-text">
          <div className="sa-brand-name">{site.name}</div>
          <div className="sa-brand-sub">Stock Analysis</div>
        </div>
      </div>

      <TopBar asOf={asOf} />
      <SideNav />
      <WatchStrip collapsed={stripCollapsed} onToggle={toggleStrip} />

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
