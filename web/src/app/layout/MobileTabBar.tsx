import { NavLink } from 'react-router';
import { MobileTabs } from '@/app/nav';
import { useAuth } from '@/providers/AuthProvider';

/**
 * 移动端底部 Tab（需求规格 §8.3：市场 / 自选 / 选股 / 通知 / 我的）。
 *
 * 类名与结构照抄原型的手机外框（`mock-frame.js` 里的 `.app-tabbar` / `.app-tab`），
 * 但真实响应式布局下需要固定到底部——原型是靠手机外框的网格区域实现的，
 * 因此这条定位规则补在 `styles/app.css` 里并在该文件注明来源。
 */
export function MobileTabBar() {
  const { me } = useAuth();
  const functionPoints = me?.functionPoints ?? [];

  const tabs = MobileTabs.filter((tab) => tab.fp === null || functionPoints.includes(tab.fp));

  return (
    <nav className="app-tabbar">
      {tabs.map((tab) => (
        <NavLink
          key={tab.key}
          to={tab.path}
          className={({ isActive }) => `app-tab${isActive ? ' is-active' : ''}`}
        >
          <span className="at-icon">{tab.icon}</span>
          {tab.text}
        </NavLink>
      ))}
    </nav>
  );
}
