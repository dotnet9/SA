import { useState } from 'react';
import { NavLink } from 'react-router';
import { useTheme } from '@/providers/ThemeProvider';
import { useToast } from '@/providers/ToastProvider';
import { CaliberDrawer } from '@/features/spec/CaliberDrawer';
import { GlobalSearch } from './GlobalSearch';

/**
 * 顶栏：全局搜索、数据口径入口、涨跌色与主题切换、通知入口、设置入口。
 *
 * 与原型的 `renderTopbar()` 一致。本应用**没有登录**，因此这里不再有
 * 登录/退出按钮与用户头像——原来那个「账号入口」换成直接进设置页。
 */
export function TopBar({ asOf }: { asOf?: string }) {
  const { theme, updown, toggleTheme, toggleUpdown } = useTheme();
  const { toast } = useToast();
  const [specOpen, setSpecOpen] = useState(false);

  return (
    <>
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
            title="数据口径"
            onClick={() => setSpecOpen(true)}
          >
            ⓘ
          </button>

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

          <NavLink className="icon-btn" to="/notifications" title="通知中心">
            ◍
          </NavLink>

          <NavLink className="icon-btn" to="/settings" title="设置">
            ⚒
          </NavLink>
        </div>
      </header>

      <CaliberDrawer open={specOpen} onClose={() => setSpecOpen(false)} />
    </>
  );
}
