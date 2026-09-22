import type { ReactNode } from 'react';

/**
 * 会话状态。本应用**没有登录**，因此恒为 `authenticated`。
 */
export type AuthStatus = 'authenticated';

/**
 * 当前身份。
 *
 * 保留这个形状（而不是把 `useAuth()` 整个删掉）是因为移动端外壳、移动端页面、搜索页
 * 等处仍在读 `me.nickname` / `can(...)`，并写 `me === null` 的分支。
 * 统一返回常量身份后，那些调用点一行都不用改——`me === null` 的分支自然变成
 * 永不触发的死分支（例如自选页的「需要登录」提示）。
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

/** 会话视图。 */
export interface AuthContextValue {
  status: AuthStatus;
  /** 恒为 {@link LocalUser}；类型保留可空是为了不改动既有调用点。 */
  me: Me | null;
  /** 是否具备全部给定功能点。本应用不做权限控制，恒为 true。 */
  can: (...functionPoints: string[]) => boolean;
}

/** 唯一的身份取值。 */
const Value: AuthContextValue = {
  status: 'authenticated',
  me: LocalUser,
  can: () => true
};

/**
 * 取当前身份与权限判断。
 *
 * 直接返回常量，**不需要 Provider**：没有会话要恢复、没有令牌要刷新，
 * 用 Context 只会多一层「可能忘记挂载」的依赖——去掉登录时正是这样让移动端崩过一次
 * （`useAuth` 抛「必须在 AuthProvider 内使用」）。常量返回值从根上消除这类失败。
 */
export function useAuth(): AuthContextValue {
  return Value;
}

/**
 * 单个功能点判定，用于按钮级显隐。
 *
 * 本应用不做权限控制，恒为 true；保留函数是为了不改动既有调用点。
 */
export function useCan(...functionPoints: string[]): boolean {
  void functionPoints;
  return true;
}

/**
 * 兼容用的空壳 Provider。
 *
 * 已不需要（`useAuth` 不再依赖 Context），保留是为了让「包一层 Provider」的旧写法不报错。
 * 新代码不必使用。
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  return <>{children}</>;
}
