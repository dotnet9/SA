import { useEffect } from 'react';
import { useLocation } from 'react-router';
import { DefaultStockCode, StockTabModules, tabOfModule } from './nav';
import { readString, writeString } from '@/lib/storage';

const LastStockKey = 'lastStock';

/**
 * 记住当前 Tab 的存储键。
 *
 * 存的是 Tab 键而不是模块名：同一个 Tab 下有多个模块（如「基本面」含财务与行业），
 * 记模块名会让用户下次进来落到上次那个子模块，而用户记住的是 Tab。
 */
const LastStockTabKey = 'lastStockTab';

/**
 * 当前分析的股票代码。
 *
 * 原型用一个固定的「焦点股」贯穿所有模块页；实现版把代码放进 URL，
 * 因此这里优先取 URL 中的 `:code`，并记住它供侧栏导航跳转使用。
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

/** 记住当前 Tab（由个股模块页在渲染时调用）。 */
export function rememberStockTab(module: string | undefined): void {
  writeString(LastStockTabKey, tabOfModule(module));
}

/** 读取上次的 Tab 键；无记录返回 null。 */
export function readLastStockTab(): string | null {
  const stored = readString(LastStockTabKey, '');
  return stored.length > 0 ? stored : null;
}

/**
 * 自选股等列表里切换标的时用的路径：**保持当前 Tab 不跳回第一个**。
 *
 * 实测体验问题：在「机构观点」Tab 上从自选股切到另一只股票，
 * 若直接跳 `/stock/{code}` 会回到「价值研究」，用户得重新点一次 Tab——
 * 而切换标的的目的恰恰是对比同一维度，跳回第一个 Tab 直接打断这个动作。
 *
 * @param code 目标标的代码。
 * @param currentModule 当前页面的模块（取自 URL）；为空时读上次记住的 Tab。
 */
export function stockPathKeepingTab(code: string, currentModule?: string): string {
  const tab = currentModule ? tabOfModule(currentModule) : readLastStockTab();
  if (!tab) {
    return `/stock/${code}`;
  }

  // 落到该 Tab 的默认模块，而不是保持具体子模块：
  // 「上次看的是财务」不代表「下一只股票也要看财务」，但「上次看的是基本面 Tab」有意义
  const modules = StockTabModules[tab];
  return modules && modules.length > 0 ? `/stock/${code}/${modules[0]}` : `/stock/${code}`;
}
