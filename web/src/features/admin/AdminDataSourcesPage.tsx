import { Link } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { EmptyState, ErrorState, SourceDot } from '@/components/ui/States';
import { fetchDataSources } from './api';

/**
 * 后台：数据源监控。
 *
 * 结构与 `design/web/admin-datasources.html` 对应：健康概览 → 各源状态 → 最近采集任务 → 口径说明。
 *
 * 这一页是「界面上的数字为什么是旧的」的第一落点：任何一源降级或失败，都应该在这里一眼看到。
 */
export function AdminDataSourcesPage() {
  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['admin', 'datasources'],
    queryFn: fetchDataSources,
    // 采集状态变化快，30 秒自动刷新一次
    refetchInterval: 30_000
  });

  if (isPending) {
    return <div className="sa-boot">正在载入数据源状态…</div>;
  }

  if (!data) {
    return (
      <>
        <Head degraded={0} total={0} />
        <div className="card">
          <div className="card-body">
            <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />
          </div>
        </div>
      </>
    );
  }

  const healthy = data.sources.filter((source) => source.status === 'ok').length;

  return (
    <>
      <Head degraded={data.degradedCount} total={data.sources.length} />

      <div className="grid grid-3">
        <StatCard label="数据源总数" value={String(data.sources.length)} />
        <StatCard label="正常" value={String(healthy)} />
        <StatCard label="降级 / 不可用" value={String(data.degradedCount)} tone={data.degradedCount > 0 ? 'down' : 'neutral'} />
      </div>

      {data.degradedCount > 0 ? (
        <div className="card mt-3">
          <div className="card-body is-tight">
            <span className="fs-12 t-down">
              有 {data.degradedCount} 个数据源处于降级或不可用状态，相关区块的数字可能不是最新的。
              连续失败达到阈值后该源会进入冷却期，期间不再请求上游。
            </span>
          </div>
        </div>
      ) : null}

      {/* 各源状态 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">数据源状态</span>
          <span className="card-sub">每 30 秒自动刷新</span>
        </div>
        <div className="card-body is-flush">
          {data.sources.length === 0 ? (
            <EmptyState title="尚无数据源状态" hint="服务启动后完成第一轮采集即会出现。" />
          ) : (
            <div className="tbl-wrap">
              <table className="tbl is-comfort">
                <thead>
                  <tr>
                    <th>状态</th>
                    <th>数据源</th>
                    <th>类型</th>
                    <th>承载域</th>
                    <th className="num">延迟</th>
                    <th className="num">连续失败</th>
                    <th>最近成功</th>
                    <th>最近错误</th>
                  </tr>
                </thead>
                <tbody>
                  {data.sources.map((source) => (
                    <tr key={source.name} data-chg={source.status === 'ok' ? 'up' : 'down'}>
                      <td>
                        <SourceDot status={source.status as 'ok' | 'warn' | 'err' | 'idle'} />
                      </td>
                      <td className="fs-12">{source.name}</td>
                      <td className="fs-11 t-2">{source.type ?? '—'}</td>
                      <td className="fs-11 t-2">{source.domains ?? '—'}</td>
                      <td className="num">{source.latencyMs === null ? '—' : `${source.latencyMs}ms`}</td>
                      <td className="num">{source.failCount}</td>
                      <td className="fs-11 t-3">{source.lastOkAt ?? '—'}</td>
                      <td className="fs-11 t-down">{source.lastError ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {/* 最近任务 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">最近采集任务</span>
          <span className="card-sub">最近 {data.tasks.length} 条</span>
        </div>
        <div className="card-body is-flush">
          {data.tasks.length === 0 ? (
            <EmptyState title="暂无任务记录" hint="调度尚未执行任务。" />
          ) : (
            <div className="tbl-wrap">
              <table className="tbl">
                <thead>
                  <tr>
                    <th>状态</th>
                    <th>任务</th>
                    <th>数据源</th>
                    <th>开始时间</th>
                    <th className="num">耗时</th>
                    <th className="num">写入</th>
                    <th>错误</th>
                  </tr>
                </thead>
                <tbody>
                  {data.tasks.map((task) => (
                    <tr key={task.id} data-chg={task.status === 'ok' ? 'up' : 'down'}>
                      <td>
                        <span className={`tag ${task.status === 'ok' ? 'tag-up' : task.status === 'warn' ? 'tag-warn' : 'tag-down'}`}>
                          {task.status === 'ok' ? '成功' : task.status === 'warn' ? '降级' : '失败'}
                        </span>
                      </td>
                      <td className="fs-11 mono">{task.taskName}</td>
                      <td className="fs-11 t-2">{task.source ?? '—'}</td>
                      <td className="fs-11 t-3">{task.startedAt}</td>
                      <td className="num">{task.costMs === null ? '—' : `${task.costMs}ms`}</td>
                      <td className="num">{task.rowsWritten ?? '—'}</td>
                      <td className="fs-11 t-down">{task.error ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      <div className="legend-block mt-4">
        <b>数据来源与口径</b>
        <br />
        状态由采集任务在每轮执行后写入：成功则清零连续失败并刷新延迟，失败则累加。
        <br />
        连续失败达到阈值（默认 3 次）后该源标记为不可用并进入冷却期（默认 5 分钟），期间不再请求上游。
        <br />
        任务日志保留最近 2000 条，界面展示最近 30 条。
        <br />
        单个数据源失败不会中断调度：失败明细会记在此页，相关页面按区块降级并标注数据时间。
      </div>
    </>
  );
}

function Head({ degraded, total }: { degraded: number; total: number }) {
  return (
    <div className="sa-pagehead">
      <div>
        <div className="breadcrumb">
          <Link to="/market">市场概览</Link>
          <span className="sep">/</span>
          <span>数据源监控</span>
        </div>
        <h1>数据源监控</h1>
        <div className="sub">
          {total} 个数据源
          {degraded > 0 ? ` · ${degraded} 个异常` : ' · 全部正常'}
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
