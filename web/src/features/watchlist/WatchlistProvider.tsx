import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

/**
 * 自选股：存在浏览器本地（localStorage）。
 *
 * 用户决定去掉登录，自选股不再有服务端账号可挂，因此改为纯客户端数据。
 * 代价要知道：**换浏览器或清站点数据就没了**，也不会在多设备间同步。
 * 这符合「本机自用、无需登录」的取舍。
 *
 * 只存代码数组：名称、涨跌幅、行业都从行情接口取（`/api/market/quotes`），
 * 避免本地存一份会过期的行情副本。
 */

const StorageKey = 'sa.watchlist';

/** 自选数量上限：行情批量接口一次最多 500 只，留一点余量。 */
export const MaxWatchItems = 500;

interface WatchlistContextValue {
  /** 自选代码，按加入顺序。 */
  codes: string[];
  /** 是否已加入。 */
  has: (code: string) => boolean;
  /** 加入（已在自选中则忽略）；超出上限时返回 false。 */
  add: (code: string) => boolean;
  /** 批量加入，返回实际新增的数量。 */
  addMany: (codes: readonly string[]) => number;
  /** 移除。 */
  remove: (code: string) => void;
  /** 清空。 */
  clear: () => void;
  /** 上移一位（列表里调整顺序用）。 */
  moveUp: (code: string) => void;
  /** 下移一位。 */
  moveDown: (code: string) => void;
}

const WatchlistContext = createContext<WatchlistContextValue | null>(null);

/** 读取本地自选（去重、过滤非法值、截断到上限）。 */
function readStored(): string[] {
  try {
    const raw = window.localStorage.getItem(StorageKey);
    if (!raw) {
      return [];
    }

    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed)) {
      return [];
    }

    const seen = new Set<string>();
    const out: string[] = [];
    for (const item of parsed) {
      if (typeof item !== 'string') continue;
      const code = item.trim();
      if (!/^\d{6}$/.test(code) || seen.has(code)) continue;
      seen.add(code);
      out.push(code);
      if (out.length >= MaxWatchItems) break;
    }

    return out;
  } catch {
    return [];
  }
}

function writeStored(codes: readonly string[]): void {
  try {
    window.localStorage.setItem(StorageKey, JSON.stringify(codes));
  } catch {
    // 隐私模式或配额满：静默忽略，内存里仍然可用（只是刷新后丢失）
  }
}

/**
 * 自选股状态。
 *
 * 放在 Provider 而不是各页面自己读 localStorage：搜索面板的「加自选」、
 * 自选页的列表、侧栏的计数都要立刻同步，各读各的会不一致。
 */
export function WatchlistProvider({ children }: { children: ReactNode }) {
  const [codes, setCodes] = useState<string[]>(() => readStored());

  /* 跨标签页同步：另一个标签改了自选，这里跟着更新 */
  useEffect(() => {
    function onStorage(event: StorageEvent) {
      if (event.key === StorageKey) {
        setCodes(readStored());
      }
    }

    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  }, []);

  const commit = useCallback((next: string[]) => {
    setCodes(next);
    writeStored(next);
  }, []);

  const has = useCallback((code: string) => codes.includes(code), [codes]);

  const add = useCallback(
    (code: string) => {
      if (!/^\d{6}$/.test(code) || codes.includes(code)) {
        return false;
      }

      if (codes.length >= MaxWatchItems) {
        return false;
      }

      // 新加入的放最前：刚加的标的通常马上要看
      commit([code, ...codes]);
      return true;
    },
    [codes, commit]
  );

  const addMany = useCallback(
    (incoming: readonly string[]) => {
      const fresh = incoming.filter((code) => /^\d{6}$/.test(code) && !codes.includes(code));
      if (fresh.length === 0) {
        return 0;
      }

      const next = [...fresh, ...codes].slice(0, MaxWatchItems);
      commit(next);
      return next.length - codes.length;
    },
    [codes, commit]
  );

  const remove = useCallback(
    (code: string) => {
      commit(codes.filter((item) => item !== code));
    },
    [codes, commit]
  );

  const clear = useCallback(() => commit([]), [commit]);

  const move = useCallback(
    (code: string, delta: number) => {
      const at = codes.indexOf(code);
      if (at < 0) return;
      const to = at + delta;
      if (to < 0 || to >= codes.length) return;

      const next = [...codes];
      const [item] = next.splice(at, 1);
      next.splice(to, 0, item);
      commit(next);
    },
    [codes, commit]
  );

  const value = useMemo<WatchlistContextValue>(
    () => ({
      codes,
      has,
      add,
      addMany,
      remove,
      clear,
      moveUp: (code: string) => move(code, -1),
      moveDown: (code: string) => move(code, 1)
    }),
    [codes, has, add, addMany, remove, clear, move]
  );

  return <WatchlistContext.Provider value={value}>{children}</WatchlistContext.Provider>;
}

/** 取自选股状态。 */
export function useWatchlist(): WatchlistContextValue {
  const value = useContext(WatchlistContext);
  if (!value) {
    throw new Error('useWatchlist 必须在 WatchlistProvider 内使用');
  }

  return value;
}
