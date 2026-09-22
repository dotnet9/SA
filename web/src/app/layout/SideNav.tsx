import { useEffect, useState } from 'react';
import { NavLink, useLocation } from 'react-router';
import { isNavItemActive, MoreNav, PrimaryNav, type NavItem } from '@/app/nav';
import { readString, writeString } from '@/lib/storage';

/** 「更多」折叠区的本机记忆键（布局偏好，不该每次刷新都重置）。 */
const MoreOpenKey = 'sa.navmore';

/**
 * 左侧导航：**只有两项**——大盘概况、自选股。
 *
 * 其余功能收进底部可折叠的「更多」区（默认收起）。这是用户明确要的信息架构：
 * 一眼看过去只有两项，功能不丢。
 *
 * 本应用不做权限控制，因此不再有「按角色裁剪菜单」与「未登录锁定」两套分支——
 * 所有项对所有人可见。
 */
export function SideNav() {
  const location = useLocation();
  const [moreOpen, setMoreOpen] = useState(() => readString(MoreOpenKey, '0') === '1');

  useEffect(() => {
    writeString(MoreOpenKey, moreOpen ? '1' : '0');
  }, [moreOpen]);

  // 当前页落在「更多」里时自动展开，否则用户会看不到自己在哪里
  useEffect(() => {
    if (MoreNav.some((item) => isNavItemActive(item, location.pathname))) {
      setMoreOpen(true);
    }
  }, [location.pathname]);

  const renderItem = (item: NavItem) => (
    <NavLink
      key={item.key}
      to={item.path}
      title={item.text}
      className={`sa-navitem${isNavItemActive(item, location.pathname) ? ' is-active' : ''}`}
    >
      <span className="ni-icon">{item.icon}</span>
      <span className="ni-text">{item.text}</span>
    </NavLink>
  );

  return (
    <nav className="sa-sidenav">
      {PrimaryNav.map(renderItem)}

      <details className="sa-nav-more" open={moreOpen}>
        <summary
          className="sa-nav-more-sum"
          onClick={(event) => {
            event.preventDefault();
            setMoreOpen((previous) => !previous);
          }}
        >
          <span>更多</span>
        </summary>
        {moreOpen ? <div className="sa-nav-more-body">{MoreNav.map(renderItem)}</div> : null}
      </details>
    </nav>
  );
}
