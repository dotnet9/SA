import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api';

/** 后台接口类型，对应后端 `SA.Contracts.Admin`。 */

export interface DataSourceStatusRow {
  name: string;
  type: string | null;
  domains: string | null;
  status: string;
  lastOkAt: string | null;
  latencyMs: number | null;
  failCount: number;
  lastError: string | null;
}

export interface CollectTaskRow {
  id: number;
  taskName: string;
  source: string | null;
  status: string;
  startedAt: string;
  costMs: number | null;
  rowsWritten: number | null;
  error: string | null;
}

export interface DataSourceMonitor {
  sources: DataSourceStatusRow[];
  tasks: CollectTaskRow[];
  degradedCount: number;
}

export interface UserRow {
  id: string;
  username: string;
  nickname: string;
  roleId: string;
  roleName: string;
  status: string;
  mustChangePwd: boolean;
  hasTotp: boolean;
  lastLoginAt: string | null;
  createdAt: string;
}

export interface UserList {
  items: UserRow[];
  total: number;
  activeCount: number;
  disabledCount: number;
  lockedCount: number;
}

export interface FunctionPoint {
  code: string;
  name: string;
  group: string;
  /**
   * 是否公开功能点（不登录即可访问）。公开功能点在后端没有挂校验，勾选与否都不改变行为，
   * 因此矩阵里必须标注为「公开」并禁用开关——否则就是一个关不掉的假开关。
   */
  isPublic: boolean;
}

export interface RoleRow {
  id: string;
  name: string;
  description: string | null;
  isBuiltin: boolean;
  functionPoints: string[];
  quotas: Record<string, number>;
  userCount: number;
}

export interface DataScopeOption {
  code: string;
  name: string;
  description: string;
}

export interface PermissionMatrix {
  functionPoints: FunctionPoint[];
  roles: RoleRow[];
  quotaKeys: string[];
  dataScopes: DataScopeOption[];
}

export interface LoginLogRow {
  id: number;
  userName: string | null;
  result: string;
  ip: string | null;
  device: string | null;
  note: string | null;
  createdAt: string;
  success: boolean;
}

export interface SessionList {
  logs: LoginLogRow[];
  activeSessionCount: number;
  failedToday: number;
}

export interface SettingRow {
  key: string;
  name: string;
  description: string;
  value: string;
  configured: boolean;
}

export interface SystemState {
  dataRoot: string;
  databasePath: string;
  parquetPath: string;
  logPath: string;
  databaseSizeBytes: number;
  parquetSizeBytes: number;
  logSizeBytes: number;
  settings: SettingRow[];
}

export interface AuditRow {
  id: number;
  username: string | null;
  action: string;
  target: string | null;
  detail: string | null;
  ip: string | null;
  createdAt: string;
}

/** 数据源监控。 */
export function fetchDataSources(): Promise<DataSourceMonitor> {
  return apiGet<DataSourceMonitor>('/api/admin/datasources');
}

/** 用户列表。 */
export function fetchUsers(keyword?: string, status?: string): Promise<UserList> {
  return apiGet<UserList>('/api/admin/users', { query: { keyword, status, limit: 200 } });
}

/** 改用户。 */
export function updateUser(
  userId: string,
  patch: { nickname?: string; roleId?: string; status?: string; mustChangePwd?: boolean }
): Promise<number> {
  return apiPut<number>(`/api/admin/users/${encodeURIComponent(userId)}`, {
    nickname: patch.nickname ?? null,
    roleId: patch.roleId ?? null,
    status: patch.status ?? null,
    mustChangePwd: patch.mustChangePwd ?? null
  });
}

/** 新建用户（初始密码留空时由服务端生成并返回一次）。 */
export function createUser(username: string, nickname: string, roleId: string): Promise<string> {
  return apiPost<string>('/api/admin/users', { username, nickname, roleId, password: null });
}

/** 重置密码（返回明文，只此一次）。 */
export function resetPassword(userId: string): Promise<string> {
  return apiPost<string>(`/api/admin/users/${encodeURIComponent(userId)}/reset-password`, { newPassword: null });
}

/** 删除用户。 */
export function deleteUser(userId: string): Promise<number> {
  return apiDelete<number>(`/api/admin/users/${encodeURIComponent(userId)}`);
}

/** 权限矩阵。 */
export function fetchPermissionMatrix(): Promise<PermissionMatrix> {
  return apiGet<PermissionMatrix>('/api/admin/permissions');
}

/** 更新角色功能点（全量覆盖）。 */
export function updateRoleFunctionPoints(roleId: string, codes: string[]): Promise<number> {
  return apiPut<number>(`/api/admin/permissions/${encodeURIComponent(roleId)}/function-points`, { codes });
}

/** 更新角色配额。 */
export function updateRoleQuotas(roleId: string, quotas: Record<string, number>): Promise<number> {
  return apiPut<number>(`/api/admin/permissions/${encodeURIComponent(roleId)}/quotas`, { quotas });
}

/** 会话与登录日志。 */
export function fetchSessions(): Promise<SessionList> {
  return apiGet<SessionList>('/api/admin/security/sessions', { query: { loginLimit: 100 } });
}

/** 强制下线（撤销该用户全部会话）。 */
export function revokeUserSessions(userId: string): Promise<number> {
  return apiPost<number>(`/api/admin/security/sessions/${encodeURIComponent(userId)}/revoke`, {});
}

/** 审计日志。 */
export function fetchAuditLogs(userId?: string): Promise<AuditRow[]> {
  return apiGet<AuditRow[]>('/api/admin/security/audit', { query: { userId, limit: 100 } });
}

/** 系统设置与存储占用。 */
export function fetchSystemState(): Promise<SystemState> {
  return apiGet<SystemState>('/api/admin/system/state');
}

/** 更新系统设置。 */
export function updateSettings(values: Record<string, string>): Promise<number> {
  return apiPut<number>('/api/admin/system/settings', { values });
}
