import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { SignalRConnection, type SignalRStatus } from '@/lib/signalr';
import { useWatchlist } from '@/features/watchlist/WatchlistProvider';

/** 一条推送行情。 */
export interface LiveQuote {
  code: string;
  price: number;
  chg: number;
  pct: number;
  volume: number;
  amount: number;
  turnover: number;
  volRatio: number;
  asOf: string | null;
}

/** 实时上下文。 */
interface RealtimeContextValue {
  /** 连接状态。 */
  status: SignalRStatus;
  /** 最新行情，按代码索引。 */
  quotes: Record<string, LiveQuote>;
  /** 最近一次收到推送的时间。 */
  lastPushAt: number | null;
  /** 当前推送间隔（秒）。 */
  intervalSeconds: number;
  /** 设置推送间隔（收敛到 3/5/10）。 */
  setIntervalSeconds: (seconds: number) => void;
  /** 已订阅的代码。 */
  subscribed: string[];
}

const RealtimeContext = createContext<RealtimeContextValue>({
  status: 'idle',
  quotes: {},
  lastPushAt: null,
  intervalSeconds: 3,
  setIntervalSeconds: () => undefined,
  subscribed: []
});

/** 使用实时行情。 */
export function useRealtime(): RealtimeContextValue {
  return useContext(RealtimeContext);
}

/** 取某只股票的实时行情（无推送时返回 undefined，调用方回落到快照值）。 */
export function useLiveQuote(code: string): LiveQuote | undefined {
  return useRealtime().quotes[code];
}

/**
 * 行情推送接入。
 *
 * 三条职责：
 * 1. 应用启动即连接（**没有登录**，因此不再需要「已登录才连」的判断）；
 * 2. 订阅集合始终与**本地自选**一致，重连后自动重新订阅（服务端的订阅登记随连接消失）；
 * 3. 推送间隔由服务端按连接生效，前端只提交偏好。
 *
 * 放在 Provider 里而不是页面里：连接与订阅是会话级的，切换页面不应反复建连。
 */
export function RealtimeProvider({ children }: { children: React.ReactNode }) {
  const { codes } = useWatchlist();
  const [status, setStatus] = useState<SignalRStatus>('idle');
  const [quotes, setQuotes] = useState<Record<string, LiveQuote>>({});
  const [lastPushAt, setLastPushAt] = useState<number | null>(null);
  const [intervalSeconds, setIntervalState] = useState(3);

  const connectionRef = useRef<SignalRConnection | null>(null);
  const codesRef = useRef<string[]>([]);

  codesRef.current = codes;

  /* --- 建连 --- */
  useEffect(() => {
    const connection = new SignalRConnection({
      hubUrl: '/hubs/quotes',
      onStatus: setStatus,
      onConnected: () => {
        // 每次连上（含重连）都重新订阅：服务端的订阅登记是随连接存活的
        const current = codesRef.current;
        if (current.length > 0) {
          void connection.invoke('Subscribe', current).catch(() => undefined);
        }

        void connection.invoke('SetInterval', intervalSeconds).catch(() => undefined);
      }
    });

    connection.on('Quote', (args) => {
      const payload = (args[0] as LiveQuote[] | undefined) ?? [];
      if (payload.length === 0) {
        return;
      }

      setQuotes((previous) => {
        const next = { ...previous };
        for (const quote of payload) {
          next[quote.code] = quote;
        }

        return next;
      });
      setLastPushAt(Date.now());
    });

    connectionRef.current = connection;
    connection.start();

    return () => {
      connection.stop();
      connectionRef.current = null;
    };
    // intervalSeconds 与 codes 都在连接建立后由下方 effect 单独提交，
    // 放进依赖会导致每次自选变化都重建连接
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  /* --- 订阅集合随自选变化 --- */
  useEffect(() => {
    const connection = connectionRef.current;
    if (!connection || status !== 'connected') {
      return;
    }

    if (codes.length > 0) {
      void connection.invoke('Subscribe', codes).catch(() => undefined);
    }

    // 已取消订阅的代码要从本地行情里移除，避免界面继续显示过期值
    setQuotes((previous) => {
      const next: Record<string, LiveQuote> = {};
      for (const code of codes) {
        if (previous[code]) {
          next[code] = previous[code];
        }
      }

      return next;
    });
  }, [codes, status]);

  /* --- 推送间隔 --- */
  const setIntervalSeconds = useCallback((seconds: number) => {
    const normalized = [3, 5, 10].includes(seconds) ? seconds : 3;
    setIntervalState(normalized);
    void connectionRef.current?.invoke('SetInterval', normalized).catch(() => undefined);
  }, []);

  const value = useMemo<RealtimeContextValue>(
    () => ({
      status,
      quotes,
      lastPushAt,
      intervalSeconds,
      setIntervalSeconds,
      subscribed: codes
    }),
    [status, quotes, lastPushAt, intervalSeconds, setIntervalSeconds, codes]
  );

  return <RealtimeContext.Provider value={value}>{children}</RealtimeContext.Provider>;
}
