import { useEffect, useState } from 'react';
import { NavLink, useLocation } from 'react-router';
import { filterNav, isNavItemActive, requiresLogin, type NavItem } from '@/app/nav';
import { useAuth } from '@/providers/AuthProvider';
import { readString, writeString } from '@/lib/storage';

/** 「更多」折叠区的本机记忆键（布局偏好，不该每次刷新都重置）。 */
const MoreOpenKey = 'sa.navmore';

/**
 * 左侧导航：**只有两项**——大盘概况、自选股。
 *
 * 其余功能收进底部可折叠的「更多」区（默认收起）。这是用户明确要的信息架构：
 * 一眼看过去只有两项，功能不丢。
 *
 * 未登录时需登录的项**锁定显示**（点击回登录页）而不是隐藏——藏起来会让访客
 * 不知道站里有什么。已登录时功能点不足的项隐藏，并提示隐藏数量。
 */
export function SideNav() {
  const { me, can } = useAuth();
  const anonymous = me === null;
  const { primary, more, hidden, locked } = filterNav(me?.functionPoints ?? null);
  const location = useLocation();

  // 「更多」的展开状态存本机
  const [moreOpen, setMoreOpen] = useState(() => readString(MoreOpenKey, '0') === '1');
  useEffect(() => {
    writeString(MoreOpenKey, moreOpen ? '1' : '0');
  }, [moreOpen]);

  // 当前页落在「更多」里时自动展开，否则用户会看不到自己在哪里
  useEffect(() => {
    if (more.some((item) => isNavItemActive(item, location.pathname))) {
      setMoreOpen(true);
    }
  }, [location.pathname, more]);

  const renderItem = (item: NavItem) => {
    const active = isNavItemActive(item, location.pathname);
    const itemLocked = anonymous && requiresLogin(item);

    if (itemLocked) {
      return (
        <NavLink
          key={item.key}
          to={`/login?next=${encodeURIComponent(item.path)}`}
          title={`${item.text}（登录后可用）`}
          className="sa-navitem is-locked"
        >
          <span className="ni-icon">{item.icon}</span>
          <span className="ni-text">{item.text}</span>
          <span className="ni-lock" aria-label="需要登录">
            ⌾
          </span>
        </NavLink>
      );
    }

    return (
      <NavLink
        key={item.key}
        to={item.path}
        title={item.text}
        className={`sa-navitem${active ? ' is-active' : ''}`}
      >
        <span className="ni-icon">{item.icon}</span>
        <span className="ni-text">{item.text}</span>
      </NavLink>
    );
  };

  return (
    <nav className="sa-sidenav">
      {primary.map(renderItem)}

      {more.length > 0 ? (
        <details className="sa-nav-more" open={moreOpen}>
          <summary className="sa-nav-more-sum" onClick={(event) => {
            event.preventDefault();
            setMoreOpen((previous) => !previous);
          }}>
            <span>更多</span>
          </summary>
          {moreOpen ? <div className="sa-nav-more-body">{more.map(renderItem)}</div> : null}
        </details>
      ) : null}

      {anonymous && locked > 0 ? (
        <div className="sa-navnote">有 {locked} 个菜单需登录后使用</div>
      ) : null}

      {!anonymous && hidden > 0 ? (
        <div className="sa-navnote">已按「{me?.roleName}」隐藏 {hidden} 个无权限菜单</div>
      ) : null}

      {!anonymous && !can('data.scope.all') ? <div className="sa-navnote">数据范围：仅自选股</div> : null}
    </nav>
  );
}
