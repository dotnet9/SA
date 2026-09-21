import { NavLink, useNavigate } from 'react-router';
import { useAuth } from '@/providers/AuthProvider';
import { useTheme } from '@/providers/ThemeProvider';
import { useToast } from '@/providers/ToastProvider';
import { GlobalSearch } from './GlobalSearch';

/**
 * 顶栏：全局搜索、数据时间、涨跌色与主题切换、通知入口、账号入口。
 * 与原型的 `renderTopbar()` 结构一致，差别是角色来自真实登录身份而不是本地切换。
 *
 * 未登录时把「通知」与「账号」两处换成登录入口：通知中心与个人设置都需要登录，
 * 留着按钮只会点一下弹一次错误。
 */
export function TopBar({ asOf }: { asOf?: string }) {
  const { me, signOut } = useAuth();
  const { theme, updown, toggleTheme, toggleUpdown } = useTheme();
  const { toast } = useToast();
  const navigate = useNavigate();

  const isAdmin = me?.roleId === 'admin';
  const anonymous = me === null;

  return (
    <header className="sa-topbar">
      <GlobalSearch />

      <div className="sa-topactions">
        {asOf ? (
          <span className="tag tag-outline hide-mobile" title="数据截止时间">
            {asOf}
          </span>
        ) : null}

        <button
          type="button"
          className="icon-btn"
          title="切换涨跌色（默认红涨绿跌）"
          onClick={() => {
            toggleUpdown();
            toast(`涨跌色已切换为「${updown === 'red-up' ? '绿涨红跌' : '红涨绿跌'}」`, 'info');
          }}
        >
          ⇅
        </button>

        <button
          type="button"
          className="icon-btn"
          title="切换深浅主题"
          onClick={() => {
            toggleTheme();
            toast(`已切换到${theme === 'dark' ? '浅色' : '深色'}主题`, 'info');
          }}
        >
          {theme === 'dark' ? '☾' : '☀'}
        </button>

        {anonymous ? (
          <NavLink className="btn btn-primary btn-sm" to="/login" title="登录后可查看自选、选股与提醒">
            登录
          </NavLink>
        ) : (
          <>
            <NavLink className="icon-btn" to="/notifications" title="通知中心">
              ◍
            </NavLink>

            <button
              type="button"
              className="user-chip"
              title="当前登录用户与角色"
              onClick={() => {
                navigate(isAdmin ? '/admin/users' : '/settings');
              }}
            >
              <span className="avatar">{(me?.nickname ?? me?.username ?? '?').charAt(0)}</span>
              <span className="fs-12 hide-mobile">{me?.nickname ?? me?.username}</span>
            </button>

            <button
              type="button"
              className="btn btn-ghost btn-sm"
              title="退出登录"
              onClick={() => {
                void signOut().then(() => {
                  navigate('/login', { replace: true });
                });
              }}
            >
              退出
            </button>
          </>
        )}
      </div>
    </header>
  );
}
