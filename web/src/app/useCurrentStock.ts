import { useEffect } from 'react';
import { useLocation } from 'react-router';
import { DefaultStockCode } from './nav';
import { readString, writeString } from '@/lib/storage';

const LastStockKey = 'lastStock';

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
