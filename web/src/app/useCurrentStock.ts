import { useEffect } from 'react';
import { useLocation } from 'react-router';
import { DefaultStockCode } from './nav';
import { readString, writeString } from '@/lib/storage';

const LastStockKey = 'lastStock';

/**
 * 记住当前个股模块。
 *
 * 存的是模块名（trend / finance …）而不是「Tab」：新信息架构下 Tab 就是 URL 的一段，
 * 这个值只用于「从列表点另一只股票时保持同一维度」——用户在「财务」看 A，
 * 点 B 时期望还是看 B 的财务，而不是被弹回概览。
 */
const LastStockModuleKey = 'lastStockModule';

/**
 * 当前分析的股票代码。
 *
 * 优先取 URL 中的 `:code`，并记住它供导航跳转使用。
 */
export function useCurrentStock(): string {
  const location = useLocation();
  const match = /^\/stock\/([^/?#]+)/.exec(location.pathname);
  const fromUrl = match?.[1];

  useEffect(() => {
    if (fromUrl) {
      writeString(LastStockKey, fromUrl);
    }
  }, [fromUrl]);

  return fromUrl ?? readString(LastStockKey, DefaultStockCode);
}

/** 读取上次查看的股票代码（非 Hook 环境使用）。 */
export function readLastStock(): string {
  return readString(LastStockKey, DefaultStockCode);
}

/** 记住当前个股模块（由个股区在渲染时调用）。 */
export function rememberStockModule(module: string | undefined): void {
  if (module) {
    writeString(LastStockModuleKey, module);
  }
}

/** 读取上次的模块名；无记录返回空串。 */
export function readLastStockModule(): string {
  return readString(LastStockModuleKey, '');
}

/**
 * 列表页里切换标的时用的路径：**保持当前维度**。
 *
 * 在「财务」上看 A 股，从列表点 B 股时期望还是看 B 的财务；
 * 直接跳 `/market/{code}` 会回到概览，打断对比动作。
 *
 * @param code 目标标的代码。
 * @param listPath 列表路径（`/market` 或 `/watchlist`）。
 */
export function stockPathKeepingModule(code: string, listPath: string): string {
  const module = readLastStockModule();
  return module && module !== 'overview' ? `${listPath}/${code}/${module}` : `${listPath}/${code}`;
}
