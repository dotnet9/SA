import { useState } from 'react';
import { Link } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { EmptyState, ErrorState } from '@/components/ui/States';
import { errorText } from '@/lib/errorText';
import { useToast } from '@/providers/ToastProvider';
import { fetchAuditLogs, fetchSessions, fetchSystemState, updateSettings } from './api';

/**
 * 后台：登录与安全（含系统设置与存储）。
 *
 * 结构与 `design/web/admin-security.html` / `admin-system.html` 对应：
 * 会话概览 → 登录日志 → 审计日志 → 系统设置与存储占用 → 口径说明。
 */
export function AdminSecurityPage() {
  const { data: sessions, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['admin', 'sessions'],
    queryFn: fetchSessions
  });

  const { data: audit } = useQuery({
    queryKey: ['admin', 'audit'],
    queryFn: () => fetchAuditLogs()
  });

  const { data: system } = useQuery({
    queryKey: ['admin', 'system'],
    queryFn: fetchSystemState
  });

  if (isPending) {
    return <div className="sa-boot">正在载入安全信息…</div>;
  }

  if (!sessions) {
    return (
      <>
        <Head active={0} failed={0} />
        <div className="card">
          <div className="card-body">
            <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />
          </div>
        </div>
      </>
    );
  }

  return (
    <>
      <Head active={sessions.activeSessionCount} failed={sessions.failedToday} />

      <div className="grid grid-3">
        <StatCard label="当前有效会话" value={String(sessions.activeSessionCount)} />
        <StatCard label="今日失败登录" value={String(sessions.failedToday)} tone={sessions.failedToday > 0 ? 'down' : undefined} />
        <StatCard label="登录记录条数" value={String(sessions.logs.length)} />
      </div>

      {/* 登录日志 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">登录日志</span>
          <span className="card-sub">最近 {sessions.logs.length} 条（含失败尝试）</span>
        </div>
        <div className="card-body is-flush">
          {sessions.logs.length === 0 ? (
            <EmptyState title="暂无登录记录" hint="服务启动后有人登录即会出现。" />
          ) : (
            <div className="tbl-wrap">
              <table className="tbl">
                <thead>
                  <tr>
                    <th>结果</th>
                    <th>登录名</th>
                    <th>IP</th>
                    <th>设备</th>
                    <th>备注</th>
                    <th>时间</th>
                  </tr>
                </thead>
                <tbody>
                  {sessions.logs.map((log) => (
                    <tr key={log.id} data-chg={log.success ? 'up' : 'down'}>
                      <td>
                        <span className={`tag ${log.success ? 'tag-up' : log.result === 'denied' ? 'tag-warn' : 'tag-down'}`}>
                          {log.success ? '成功' : log.result === 'denied' ? '被拒' : '失败'}
                        </span>
                      </td>
                      <td className="mono fs-12">{log.userName ?? '—'}</td>
                      <td className="fs-11 t-2">{log.ip ?? '—'}</td>
                      <td className="fs-11 t-3">{log.device ?? '—'}</td>
                      <td className="fs-11 t-down">{log.note ?? '—'}</td>
                      <td className="fs-11 t-3">{log.createdAt}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {/* 审计日志 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">管理操作审计</span>
          <span className="card-sub">最近 {audit?.length ?? 0} 条</span>
        </div>
        <div className="card-body is-flush">
          {!audit || audit.length === 0 ? (
            <EmptyState title="暂无审计记录" hint="管理类操作（改用户、改权限、改设置、撤销会话）会记录在此。" />
          ) : (
            <div className="tbl-wrap">
              <table className="tbl">
                <thead>
                  <tr>
                    <th>操作人</th>
                    <th>动作</th>
                    <th>目标</th>
                    <th>变更</th>
                    <th>时间</th>
                  </tr>
                </thead>
                <tbody>
                  {audit.map((row) => (
                    <tr key={row.id}>
                      <td className="fs-12">{row.username ?? '—'}</td>
                      <td className="mono fs-11">{row.action}</td>
                      <td className="fs-11 t-3">{row.target ?? '—'}</td>
                      <td className="fs-11 t-2">{row.detail ?? '—'}</td>
                      <td className="fs-11 t-3">{row.createdAt}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {/* 系统设置与存储 */}
      {system ? <SystemPanel system={system} /> : null}

      <div className="legend-block mt-4">
        <b>数据来源与口径</b>
        <br />
        登录日志记录成功、失败与被拒三种结果，失败尝试也保留登录名（撞库时正是需要看到用了哪些用户名）。
        <br />
        会话为刷新令牌记录：重置密码或「强制下线」会撤销该用户全部会话，旧令牌立即失效。
        <br />
        审计只记管理类操作（改用户 / 改权限 / 改设置 / 撤销会话），不记普通读操作。
        <br />
        系统设置只开放白名单内的键：密钥类与数据目录类必须改配置文件并重启（改了会导致数据找不到或会话失效）。
      </div>
    </>
  );
}

function SystemPanel({ system }: { system: Awaited<ReturnType<typeof fetchSystemState>> }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const [draft, setDraft] = useState<Record<string, string>>({});

  const saveMutation = useMutation({
    mutationFn: (values: Record<string, string>) => updateSettings(values),
    onSuccess: async () => {
      toast.toast('设置已保存', 'ok');
      setDraft({});
      await queryClient.invalidateQueries({ queryKey: ['admin', 'system'] });
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  return (
    <>
      <div className="grid grid-2 mt-4">
        <div className="card">
          <div className="card-head">
            <span className="card-title">存储占用</span>
            <span className="card-sub">数据目录：{system.dataRoot}</span>
          </div>
          <div className="card-body col gap-2">
            <SizeRow label="数据库（SQLite）" path={system.databasePath} bytes={system.databaseSizeBytes} />
            <SizeRow label="时序数据（Parquet）" path={system.parquetPath} bytes={system.parquetSizeBytes} />
            <SizeRow label="日志" path={system.logPath} bytes={system.logSizeBytes} />
            <div className="chart-note">
              只读展示：清理与备份属于运维动作，本页不提供删除入口（误删时序数据会丢失历史）。
            </div>
          </div>
        </div>

        <div className="card">
          <div className="card-head">
            <span className="card-title">系统设置</span>
            <span className="card-sub">仅白名单内的键可改</span>
          </div>
          <div className="card-body col gap-3">
            {system.settings.map((setting) => (
              <label key={setting.key} className="col gap-1">
                <span className="fs-11 t-3">
                  {setting.name}
                  <span className="mono" style={{ marginLeft: 6 }}>{setting.key}</span>
                  {setting.configured ? <span className="tag tag-outline" style={{ marginLeft: 6 }}>已配置</span> : null}
                </span>
                <input
                  className="input input-sm"
                  value={draft[setting.key] ?? setting.value}
                  placeholder={setting.description}
                  onChange={(event) => setDraft((previous) => ({ ...previous, [setting.key]: event.target.value }))}
                />
              </label>
            ))}

            <button
              type="button"
              className="btn btn-sm btn-primary"
              disabled={saveMutation.isPending || Object.keys(draft).length === 0}
              onClick={() => saveMutation.mutate(draft)}
            >
              保存设置
            </button>
          </div>
        </div>
      </div>
    </>
  );
}

function SizeRow({ label, path, bytes }: { label: string; path: string; bytes: number }) {
  return (
    <div className="col gap-1">
      <div className="row-between">
        <span className="fs-12 t-2">{label}</span>
        <span className="mono fs-12">{formatBytes(bytes)}</span>
      </div>
      <span className="fs-11 t-3" style={{ wordBreak: 'break-all' }}>
        {path}
      </span>
      <div className="divider" />
    </div>
  );
}

function Head({ active, failed }: { active: number; failed: number }) {
  return (
    <div className="sa-pagehead">
      <div>
        <div className="breadcrumb">
          <Link to="/market">市场概览</Link>
          <span className="sep">/</span>
          <span>登录与安全</span>
        </div>
        <h1>登录与安全</h1>
        <div className="sub">
          {active} 个有效会话
          {failed > 0 ? ` · 今日失败登录 ${failed} 次` : ' · 今日无失败登录'}
        </div>
      </div>
    </div>
  );
}

function StatCard({ label, value, tone }: { label: string; value: string; tone?: string }) {
  return (
    <div className="card">
      <div className="card-body is-tight">
        <div className="kpi">
          <span className="kpi-label">{label}</span>
          <span className={`kpi-value is-sm ${tone === 'down' ? 't-down' : ''}`}>{value}</span>
        </div>
      </div>
    </div>
  );
}

function formatBytes(bytes: number): string {
  if (bytes <= 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const index = Math.min(units.length - 1, Math.floor(Math.log(bytes) / Math.log(1024)));
  return `${(bytes / 1024 ** index).toFixed(index === 0 ? 0 : 2)} ${units[index]}`;
}
