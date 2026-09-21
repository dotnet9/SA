import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api';

/** 提醒与通知接口类型，对应后端 `SA.Contracts.Alerts`。 */

export interface AlertTypeOption {
  type: string;
  name: string;
  unit: string | null;
  hint: string;
}

export interface AlertRule {
  id: string;
  code: string;
  name: string;
  ruleType: string;
  ruleTypeName: string;
  threshold: number | null;
  thresholdUnit: string | null;
  note: string | null;
  enabled: boolean;
  createdAt: string;
  lastTriggeredAt: string | null;
  triggerCount: number;
}

export interface AlertRuleList {
  rules: AlertRule[];
  total: number;
  quota: number;
  types: AlertTypeOption[];
  asOf: string | null;
}

export interface Notification {
  id: number;
  title: string;
  body: string;
  level: string;
  code: string | null;
  ruleId: string | null;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationList {
  items: Notification[];
  unread: number;
  total: number;
}

/**
 * 提醒偏好（免打扰与推送开关）。
 *
 * 存在服务端而不是浏览器本地：浏览器关掉时前端没有机会判断「现在该不该响」，
 * 因此免打扰必须由服务端执行。
 */
export interface NotifySettings {
  dndEnabled: boolean;
  dndFrom: string;
  dndTo: string;
  dndKeepInbox: boolean;
  pushEnabled: boolean;
}

/** 一条推送订阅（服务端视角；端点是长 URL，只回主机名）。 */
export interface PushSubscriptionRow {
  id: number;
  host: string;
  userAgent: string | null;
  enabled: boolean;
  failCount: number;
  lastOkAt: string | null;
  lastError: string | null;
  createdAt: string;
}

/** 取提醒偏好。 */
export function fetchNotifySettings(): Promise<NotifySettings> {
  return apiGet<NotifySettings>('/api/notifications/settings');
}

/** 保存提醒偏好。 */
export function saveNotifySettings(settings: NotifySettings): Promise<number> {
  return apiPut<number>('/api/notifications/settings', settings);
}

/** 取当前账号的推送订阅（界面展示「哪台设备还收得到」）。 */
export function fetchPushSubscriptions(): Promise<PushSubscriptionRow[]> {
  return apiGet<PushSubscriptionRow[]>('/api/notifications/push/subscriptions');
}

/** 取提醒规则列表。 */
export function fetchAlertRules(): Promise<AlertRuleList> {
  return apiGet<AlertRuleList>('/api/alerts');
}

/** 新建规则。 */
export function createAlertRule(
  code: string,
  ruleType: string,
  threshold: number | null,
  note?: string
): Promise<string> {
  return apiPost<string>('/api/alerts', { code, ruleType, threshold, note: note ?? null });
}

/** 修改规则（启停 / 阈值 / 备注）。 */
export function updateAlertRule(
  ruleId: string,
  patch: { threshold?: number | null; note?: string | null; enabled?: boolean }
): Promise<number> {
  return apiPut<number>(`/api/alerts/${encodeURIComponent(ruleId)}`, {
    threshold: patch.threshold ?? null,
    note: patch.note ?? null,
    enabled: patch.enabled ?? null
  });
}

/** 删除规则。 */
export function removeAlertRule(ruleId: string): Promise<number> {
  return apiDelete<number>(`/api/alerts/${encodeURIComponent(ruleId)}`);
}

/** 取通知列表。 */
export function fetchNotifications(unreadOnly = false, limit = 50): Promise<NotificationList> {
  return apiGet<NotificationList>('/api/notifications', { query: { unreadOnly, limit } });
}

/** 标记已读（ids 为空表示全部已读）。 */
export function markNotificationsRead(ids: number[] = []): Promise<number> {
  return apiPost<number>('/api/notifications/read', { ids });
}
