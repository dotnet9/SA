import { NavLink } from 'react-router';
import { MobileTabs } from '@/app/nav';

/**
 * 移动端底部 Tab（3 项：大盘 / 自选 / 更多）。
 *
 * 类名与结构照抄原型的手机外框（`mock-frame.js` 里的 `.app-tabbar` / `.app-tab`），
 * 但真实响应式布局下需要固定到底部——原型是靠手机外框的网格区域实现的，
 * 因此这条定位规则补在 `styles/app.css` 里并在该文件注明来源。
 *
 * 与桌面端一致：两项为主，其余收进「更多」。本应用不做权限控制，
 * 因此不再有「按功能点隐藏」与「未登录锁定」两套分支。
 */
export function MobileTabBar() {
  return (
    <nav className="app-tabbar">
      {MobileTabs.map((tab) => (
        <NavLink
          key={tab.key}
          to={tab.path}
          title={tab.text}
          className={({ isActive }) => `app-tab${isActive ? ' is-active' : ''}`}
        >
          <span className="at-icon">{tab.icon}</span>
          {tab.text}
        </NavLink>
      ))}
    </nav>
  );
}
