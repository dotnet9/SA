import { NavLink } from 'react-router';
import { isPublicNavItem, MobileTabs, requiresLogin } from '@/app/nav';
import { useAuth } from '@/providers/AuthProvider';

/**
 * 移动端底部 Tab（需求规格 §8.3：市场 / 自选 / 选股 / 通知 / 我的）。
 *
 * 类名与结构照抄原型的手机外框（`mock-frame.js` 里的 `.app-tabbar` / `.app-tab`），
 * 但真实响应式布局下需要固定到底部——原型是靠手机外框的网格区域实现的，
 * 因此这条定位规则补在 `styles/app.css` 里并在该文件注明来源。
 *
 * 未登录时保留全部 Tab（市场可直接进入，其余点进登录页），与侧边导航同一套规则：
 * 少一个入口不代表多一分安全，反而让访客不知道站里有什么。
 */
export function MobileTabBar() {
  const { me } = useAuth();
  const anonymous = me === null;
  const functionPoints = me?.functionPoints ?? [];

  const tabs = MobileTabs.filter((tab) => {
    // 公开项对所有人可见（后端已放开），不能因为「当前角色没有该功能点」而隐藏
    if (isPublicNavItem(tab)) return true;
    if (anonymous) return true;
    return tab.fp === null || functionPoints.includes(tab.fp);
  });

  return (
    <nav className="app-tabbar">
      {tabs.map((tab) => {
        const locked = anonymous && requiresLogin(tab);

        return (
          <NavLink
            key={tab.key}
            to={locked ? `/login?next=${encodeURIComponent(tab.path)}` : tab.path}
            title={locked ? `${tab.text}（登录后可用）` : tab.text}
            className={({ isActive }) =>
              `app-tab${isActive && !locked ? ' is-active' : ''}${locked ? ' is-locked' : ''}`
            }
          >
            <span className="at-icon">{tab.icon}</span>
            {tab.text}
          </NavLink>
        );
      })}
    </nav>
  );
}
