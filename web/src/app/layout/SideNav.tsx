import { NavLink, useLocation } from 'react-router';
import { filterNav, isNavItemActive, navTarget, requiresLogin } from '@/app/nav';
import { useCurrentStock } from '@/app/useCurrentStock';
import { useAuth } from '@/providers/AuthProvider';

/**
 * 左侧导航。按登录态与功能点裁剪分组：
 *
 * - <b>未登录</b>：公开项可正常进入；需登录的项<b>锁定显示</b>（点击回登录页），
 *   而不是整块隐藏——藏起来会让访客不知道站里有什么。
 * - <b>已登录</b>：功能点不足的项隐藏，并提示隐藏数量（与原型的 `renderNav()` 一致）。
 */
export function SideNav() {
  const { me, can } = useAuth();
  const anonymous = me === null;
  const { groups, hidden, locked } = filterNav(me?.functionPoints ?? null);
  const stockCode = useCurrentStock();
  const location = useLocation();

  return (
    <nav className="sa-sidenav">
      {groups.map((group) => (
        <div key={group.group}>
          <div className="sa-navgroup">{group.group}</div>
          {group.items.map((item) => {
            const active = isNavItemActive(item, location.pathname);
            const itemLocked = anonymous && requiresLogin(item);

            if (itemLocked) {
              return (
                <NavLink
                  key={item.key}
                  to={`/login?next=${encodeURIComponent(navTarget(item, stockCode))}`}
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
                to={navTarget(item, stockCode)}
                title={item.text}
                className={`sa-navitem${active ? ' is-active' : ''}`}
              >
                <span className="ni-icon">{item.icon}</span>
                <span className="ni-text">{item.text}</span>
              </NavLink>
            );
          })}
        </div>
      ))}

      <div className="sa-navgroup" style={{ marginTop: 12 }}>
        当前账号
      </div>

      {anonymous ? (
        <NavLink className="sa-navitem" to="/login" title="登录后可查看自选、选股与提醒">
          <span className="ni-icon">◑</span>
          <span className="ni-text">未登录 · 去登录</span>
        </NavLink>
      ) : (
        <div className="sa-navitem is-locked" title="角色由管理员在权限矩阵中分配">
          <span className="ni-icon">◑</span>
          <span className="ni-text">{me?.roleName ?? '未登录'}</span>
        </div>
      )}

      {anonymous && locked > 0 ? (
        <div className="sa-navnote">有 {locked} 个菜单需登录后使用</div>
      ) : null}

      {!anonymous && hidden > 0 ? (
        <div className="sa-navnote">
          已按「{me?.roleName}」隐藏 {hidden} 个无权限菜单
        </div>
      ) : null}

      {!anonymous && !can('data.scope.all') ? <div className="sa-navnote">数据范围：仅自选股</div> : null}
    </nav>
  );
}
