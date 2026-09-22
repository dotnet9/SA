import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { readString, StorageKeys, writeString } from '@/lib/storage';

/** 深浅主题。 */
export type ThemeName = 'dark' | 'light';

/** 涨跌配色：默认红涨绿跌。 */
export type UpDownMode = 'red-up' | 'green-up';

/** 表格密度，对应设计令牌 `--row-h` / `--row-h-comfort`。 */
export type Density = 'compact' | 'comfortable';

interface ThemeContextValue {
  theme: ThemeName;
  updown: UpDownMode;
  density: Density;
  toggleTheme: () => void;
  toggleUpdown: () => void;
  setTheme: (theme: ThemeName) => void;
  setUpdown: (mode: UpDownMode) => void;
  setDensity: (density: Density) => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

/**
 * 主题、涨跌色与密度。
 *
 * 实现方式与原型 `app.js` 的 `applyTheme()` 一致：在 `<html>` 上写
 * `data-theme` / `data-updown` / `data-density`，全部颜色由 tokens.css 的变量派生，
 * 因此图表只要从 CSS 变量取色就会自动跟随（实施计划 §3.4、§5.9）。
 */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<ThemeName>(() => readString(StorageKeys.Theme, 'light') as ThemeName);
  const [updown, setUpdownState] = useState<UpDownMode>(() => readString(StorageKeys.UpDown, 'red-up') as UpDownMode);
  const [density, setDensityState] = useState<Density>(
    () => readString(StorageKeys.Density, 'compact') as Density
  );

  useEffect(() => {
    const root = document.documentElement;
    root.setAttribute('data-theme', theme);
    root.setAttribute('data-updown', updown);
    root.setAttribute('data-density', density);

    writeString(StorageKeys.Theme, theme);
    writeString(StorageKeys.UpDown, updown);
    writeString(StorageKeys.Density, density);

    // 图表层监听该事件重建实例（原型 charts.js 的 rebuildAll 等价物）
    document.dispatchEvent(
      new CustomEvent('sa:themechange', { detail: { theme, updown, density } })
    );
  }, [theme, updown, density]);

  const toggleTheme = useCallback(() => {
    setThemeState((current) => (current === 'dark' ? 'light' : 'dark'));
  }, []);

  const toggleUpdown = useCallback(() => {
    setUpdownState((current) => (current === 'red-up' ? 'green-up' : 'red-up'));
  }, []);

  const value = useMemo<ThemeContextValue>(
    () => ({
      theme,
      updown,
      density,
      toggleTheme,
      toggleUpdown,
      setTheme: setThemeState,
      setUpdown: setUpdownState,
      setDensity: setDensityState
    }),
    [theme, updown, density, toggleTheme, toggleUpdown]
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

/** 取主题状态与切换方法。 */
export function useTheme(): ThemeContextValue {
  const value = useContext(ThemeContext);
  if (!value) {
    throw new Error('useTheme 必须在 ThemeProvider 内使用');
  }

  return value;
}
