import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import * as session from '@/lib/session';
import type { Me } from '@/lib/session';

/** 会话状态。 */
export type AuthStatus = 'loading' | 'anonymous' | 'authenticated';

interface AuthContextValue {
  status: AuthStatus;
  me: Me | null;
  /** 是否具备全部给定功能点。 */
  can: (...functionPoints: string[]) => boolean;
  /** 登录；失败会抛出 ApiError，由调用方展示提示。 */
  signIn: (username: string, password: string, options?: { totpCode?: string; rememberMe?: boolean }) => Promise<Me>;
  /** 登出并清空本地会话状态。 */
  signOut: () => Promise<void>;
  /** 强制改密成功后重新拉取快照（mustChangePwd 会变为 false）。 */
  reload: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

/**
 * 会话与权限。
 *
 * 启动时先用刷新 Cookie 恢复会话（访问令牌只在内存，刷新页面即失效），
 * 拿到权限快照后交给路由与菜单裁剪（需求规格 §7.3）。
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('loading');
  const [me, setMe] = useState<Me | null>(null);

  // api 层遇到 2001 时会回调这里换新令牌
  useEffect(() => {
    session.registerRefreshHandler();
    return () => {
      session.unregisterRefreshHandler();
    };
  }, []);

  useEffect(() => {
    let alive = true;

    session
      .restoreSession()
      .then((restored) => {
        if (!alive) return;
        setMe(restored);
        setStatus(restored ? 'authenticated' : 'anonymous');
      })
      .catch(() => {
        if (!alive) return;
        setMe(null);
        setStatus('anonymous');
      });

    return () => {
      alive = false;
    };
  }, []);

  const can = useCallback(
    (...functionPoints: string[]) => {
      if (functionPoints.length === 0) return true;
      if (!me) return false;
      return functionPoints.every((fp) => me.functionPoints.includes(fp));
    },
    [me]
  );

  const signIn = useCallback(
    async (username: string, password: string, options?: { totpCode?: string; rememberMe?: boolean }) => {
      const response = await session.login(username, password, options);
      setMe(response.user);
      setStatus('authenticated');
      return response.user;
    },
    []
  );

  const signOut = useCallback(async () => {
    await session.logout();
    setMe(null);
    setStatus('anonymous');
  }, []);

  const reload = useCallback(async () => {
    const fresh = await session.fetchMe();
    setMe(fresh);
    setStatus('authenticated');
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({ status, me, can, signIn, signOut, reload }),
    [status, me, can, signIn, signOut, reload]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

/** 取会话与权限。 */
export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (!value) {
    throw new Error('useAuth 必须在 AuthProvider 内使用');
  }

  return value;
}

/**
 * 单个功能点判定，用于按钮级显隐（如导出、编辑标注）。
 */
export function useCan(...functionPoints: string[]): boolean {
  const { can } = useAuth();
  return can(...functionPoints);
}
