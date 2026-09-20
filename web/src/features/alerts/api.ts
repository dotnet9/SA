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
