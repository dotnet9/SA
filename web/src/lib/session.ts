import { apiGet, apiPost, apiPut, setAccessToken, setRefreshHandler } from './api';
import { ApiError } from './errors';

/**
 * 当前用户与权限快照，对应后端 SA.Contracts.Me.MeDto。
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
  dataScope: 'all' | 'watchlist';
  functionPoints: string[];
  quotas: Record<string, number>;
}

/** 登录响应，对应后端 SA.Contracts.Auth.LoginResponse。 */
export interface LoginResponse {
  accessToken: string;
  tokenType: string;
  expiresIn: number;
  user: Me;
  serverTime: string;
}

/**
 * 登录。成功后内存持有访问令牌，刷新令牌由服务端写入 HttpOnly Cookie。
 */
export async function login(
  username: string,
  password: string,
  options: { totpCode?: string; rememberMe?: boolean } = {}
): Promise<LoginResponse> {
  const response = await apiPost<LoginResponse>(
    '/api/auth/login',
    {
      username,
      password,
      totpCode: options.totpCode ?? null,
      rememberMe: options.rememberMe ?? true
    },
    { anonymous: true }
  );

  setAccessToken(response.accessToken);
  return response;
}

/**
 * 用刷新 Cookie 换新访问令牌并取回权限快照。
 * 返回 null 表示当前没有有效会话（未登录或已过期），不是错误。
 */
export async function restoreSession(): Promise<Me | null> {
  try {
    const response = await apiPost<LoginResponse>('/api/auth/refresh', undefined, { anonymous: true });
    setAccessToken(response.accessToken);
    return response.user;
  } catch {
    setAccessToken(null);
    return null;
  }
}

/**
 * 刷新访问令牌（供 api 层在 401 时回调）。失败返回 null。
 */
export async function refreshAccessToken(): Promise<string | null> {
  try {
    const response = await apiPost<LoginResponse>('/api/auth/refresh', undefined, { anonymous: true });
    setAccessToken(response.accessToken);
    return response.accessToken;
  } catch {
    setAccessToken(null);
    return null;
  }
}

/** 登出。幂等：即使会话已失效也返回成功。 */
export async function logout(): Promise<void> {
  try {
    await apiPost<boolean>('/api/auth/logout', undefined, { anonymous: true });
  } catch (error) {
    // 登出失败不应阻塞界面：本地状态照常清理
    if (!(error instanceof ApiError)) {
      throw error;
    }
  } finally {
    setAccessToken(null);
  }
}

/** 取当前用户与权限快照。 */
export function fetchMe(): Promise<Me> {
  return apiGet<Me>('/api/me');
}

/** 取轻量权限快照（矩阵变更后刷新用）。 */
export function fetchPermissions(): Promise<{
  roleId: string;
  roleName: string;
  dataScope: 'all' | 'watchlist';
  functionPoints: string[];
  quotas: Record<string, number>;
  mustChangePwd: boolean;
}> {
  return apiGet('/api/me/permissions');
}

/** 修改密码。成功后服务端吊销全部会话，前端应回到登录页。 */
export function changePassword(currentPassword: string, newPassword: string): Promise<boolean> {
  return apiPost<boolean>('/api/auth/change-password', { currentPassword, newPassword });
}

/** 个人设置。 */
export function fetchSettings(): Promise<Record<string, unknown>> {
  return apiGet<Record<string, unknown>>('/api/me/settings');
}

/** 保存个人设置（整体替换）。 */
export function saveSettings(settings: Record<string, unknown>): Promise<Record<string, unknown>> {
  return apiPut<Record<string, unknown>>('/api/me/settings', settings);
}

/** 二次验证状态。 */
export interface TotpStatus {
  enabled: boolean;
  required: boolean;
}

/** 取二次验证状态。 */
export function fetchTotpStatus(): Promise<TotpStatus> {
  return apiGet<TotpStatus>('/api/auth/totp/status');
}

/** 开始绑定二次验证，返回密钥与 otpauth URI。 */
export function setupTotp(): Promise<{ secret: string; otpAuthUri: string }> {
  return apiPost<{ secret: string; otpAuthUri: string }>('/api/auth/totp/setup');
}

/** 校验并启用二次验证。 */
export function enableTotp(secret: string, code: string): Promise<boolean> {
  return apiPost<boolean>('/api/auth/totp/enable', { secret, code });
}

/** 关闭二次验证。 */
export function disableTotp(): Promise<boolean> {
  return apiPost<boolean>('/api/auth/totp/disable');
}

/** 把刷新回调注册进 api 层（AuthProvider 挂载时调用一次）。 */
export function registerRefreshHandler(): void {
  setRefreshHandler(refreshAccessToken);
}

/** 注销刷新回调（AuthProvider 卸载时调用）。 */
export function unregisterRefreshHandler(): void {
  setRefreshHandler(null);
}
