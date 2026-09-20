import { apiDelete, apiGet, apiPut } from '@/lib/api';

/** 事件接口类型，对应后端 `SA.Contracts.Events`。 */

export interface EventItem {
  key: string;
  date: string;
  type: string;
  typeName: string;
  title: string;
  detail: string | null;
  tone: 'up' | 'down' | 'neutral';
  impact: number;
  source: string;
  annotated: boolean;
  annotationNote: string | null;
}

export interface EventSummary {
  type: string;
  typeName: string;
  count: number;
  latestDate: string | null;
  netTone: string;
}

export interface TopologyNode {
  id: string;
  name: string;
  category: number;
  value: number;
  highlight: boolean;
  note: string | null;
}

export interface TopologyEdge {
  source: string;
  target: string;
  label: string | null;
  tone: string;
}

export interface Topology {
  key: 'shareholder' | 'industry' | 'event' | 'counterparty';
  name: string;
  description: string;
  categories: string[];
  nodes: TopologyNode[];
  edges: TopologyEdge[];
  available: boolean;
  unavailableReason: string | null;
}

export interface EventTimeline {
  code: string;
  name: string;
  asOf: string | null;
  events: EventItem[];
  summary: EventSummary[];
  topologies: Topology[];
  insights: string[];
  canAnnotate: boolean;
  /** 仍在采集：界面据此自动轮询刷新，避免「首次打开只看到 0 条事件」。 */
  collecting: boolean;
  notes: string[];
}

/** 取事件时间线与拓扑图。 */
export function fetchEvents(code: string): Promise<EventTimeline> {
  return apiGet<EventTimeline>(`/api/stocks/${encodeURIComponent(code)}/events`);
}

/** 写入人工标注（覆盖派生判读）。 */
export function annotateEvent(
  code: string,
  eventKey: string,
  tone: 'up' | 'down' | 'neutral',
  impact: number,
  note?: string
): Promise<number> {
  return apiPut<number>(`/api/stocks/${encodeURIComponent(code)}/events/annotation`, {
    eventKey,
    tone,
    impact,
    note: note ?? null
  });
}

/** 删除人工标注，恢复为派生判读。 */
export function removeAnnotation(code: string, eventKey: string): Promise<number> {
  return apiDelete<number>(`/api/stocks/${encodeURIComponent(code)}/events/annotation/${encodeURIComponent(eventKey)}`);
}
