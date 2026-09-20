import { NavLink, useLocation } from 'react-router';
import { filterNav, isNavItemActive, navTarget } from '@/app/nav';
import { useCurrentStock } from '@/app/useCurrentStock';
import { useAuth } from '@/providers/AuthProvider';

/**
 * 左侧导航。按功能点裁剪分组，并在有隐藏项时给出提示——
 * 与原型 `renderNav()` 的「已按角色隐藏 N 个无权限菜单」一致。
 */
export function SideNav() {
  const { me, can } = useAuth();
  const { groups, hidden } = filterNav(me?.functionPoints ?? []);
  const stockCode = useCurrentStock();
  const location = useLocation();

  return (
    <nav className="sa-sidenav">
      {groups.map((group) => (
        <div key={group.group}>
          <div className="sa-navgroup">{group.group}</div>
          {group.items.map((item) => {
            const active = isNavItemActive(item, location.pathname);
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
      <div className="sa-navitem is-locked" title="角色由管理员在权限矩阵中分配">
        <span className="ni-icon">◑</span>
        <span className="ni-text">{me?.roleName ?? '未登录'}</span>
      </div>

      {hidden > 0 ? (
        <div className="sa-navnote">
          已按「{me?.roleName}」隐藏 {hidden} 个无权限菜单
        </div>
      ) : null}

      {!can('data.scope.all') ? <div className="sa-navnote">数据范围：仅自选股</div> : null}
    </nav>
  );
}
