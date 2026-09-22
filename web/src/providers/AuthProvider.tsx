import { createContext, useContext, useMemo, type ReactNode } from 'react';

/**
 * 会话状态。本应用**没有登录**，因此恒为 `authenticated`。
 */
export type AuthStatus = 'authenticated';

/**
 * 当前身份。
 *
 * 保留这个形状（而不是把 `useAuth()` 整个删掉）是因为导航裁剪、实时推送、设置页、
 * 自选页等处仍在读 `me.nickname` / `me.functionPoints`，并写 `me === null` 的分支。
 * 全部改成「本机用户 + 全部功能点」后，那些调用点一行都不用改——
 * `me === null` 的分支自然变成永不触发的死分支（例如自选页的「需要登录」提示）。
 */
export interface Me {
  id: string;
  username: string;
  nickname: string;
  roleId: string;
  roleName: string;
  mustChangePwd: boolean;
  totpEnabled: boolean;
  totpRequired: boolean;
  dataScope: 'all';
  functionPoints: string[];
  quotas: Record<string, number>;
}

/** 本机用户：不需要登录，拥有全市场数据范围。 */
const LocalUser: Me = {
  id: 'local',
  username: 'local',
  nickname: '本机用户',
  roleId: 'local',
  roleName: '本机用户',
  mustChangePwd: false,
  totpEnabled: false,
  totpRequired: false,
  dataScope: 'all',
  functionPoints: [],
  quotas: {}
};

interface AuthContextValue {
  status: AuthStatus;
  /** 恒为 {@link LocalUser}；类型保留可空是为了不改动既有调用点。 */
  me: Me | null;
  /** 是否具备全部给定功能点。本应用不做权限控制，恒为 true。 */
  can: (...functionPoints: string[]) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

/**
 * 本机身份。
 *
 * 用户决定去掉登录与权限、所有功能免费开放，因此这里不再有会话恢复、令牌刷新、
 * 登出等逻辑——只提供一个常量身份，让依赖 `useAuth()` 的既有代码继续工作。
 *
 * `can()` 恒为 true 而不是「按功能点判断」：既然不做权限控制，
 * 再让某个菜单或按钮因为功能点缺失而消失，就与「所有功能都开放」自相矛盾。
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const value = useMemo<AuthContextValue>(
    () => ({
      status: 'authenticated',
      me: LocalUser,
      can: () => true
    }),
    []
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

/** 取当前身份与权限判断。 */
export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (!value) {
    throw new Error('useAuth 必须在 AuthProvider 内使用');
  }

  return value;
}

/**
 * 单个功能点判定，用于按钮级显隐。
 *
 * 本应用不做权限控制，恒为 true；保留函数是为了不改动既有调用点。
 */
export function useCan(...functionPoints: string[]): boolean {
  return useAuth().can(...functionPoints);
}
