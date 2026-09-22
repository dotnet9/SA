import { apiGet } from '@/lib/api';

/**
 * 运维接口类型，对应后端 `SA.Contracts.Admin`。
 *
 * 原先这里还有用户管理、角色与权限、会话与审计、系统设置四组接口。用户决定
 * **去掉登录与权限**（所有功能免费开放），那四组随页面与端点一并删除；
 * 只保留数据源监控——它展示采集任务与上游连通性，是运维信息，与权限无关。
 */

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

/** 数据源与采集任务监控。 */
export function fetchDataSources(): Promise<DataSourceMonitor> {
  return apiGet<DataSourceMonitor>('/api/admin/datasources');
}
