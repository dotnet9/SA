import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { hbar, useChart } from '@/components/charts';
import { EmptyState, ErrorState } from '@/components/ui/States';
import { errorText } from '@/lib/errorText';
import { useToast } from '@/providers/ToastProvider';
import {
  deleteScreenerRun,
  fetchExportLogs,
  fetchScreenerDistribution,
  fetchScreenerHistory,
  fetchScreenerStrategies,
  renameScreenerStrategy,
  replayScreenerRun,
  type ScreenerDistribution,
  type ScreenerRun,
  type ScreenerRunList
} from '@/features/analysis/api';
import type { ScreenerCondition } from './api';

/**
 * 选股器的三个附加区块：我的策略、分布统计、筛选日志与导出记录。
 *
 * 抽成独立文件而不是塞进 ScreenerPage：那一页已经承载条件编辑器与结果表，
 * 再挂三个区块会让单个组件过大、也难以单独复用（移动端就只用了条件与结果）。
 */

/** 分布统计：分位数 + 直方图。 */
export function ScreenerDistributionPanel({
  condition,
  field
}: {
  condition: ScreenerCondition;
  field: string;
}) {
  const { data, error, isPending, failureCount, refetch } = useQuery({
    queryKey: ['screener', 'distribution', field, JSON.stringify(condition)],
    queryFn: () => fetchScreenerDistribution(field, condition)
  });

  if (isPending) {
    return <div className="sa-boot" style={{ height: 120 }}>正在统计分布…</div>;
  }

  if (!data) {
    return <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />;
  }

  if (data.count === 0) {
    return (
      <EmptyState
        title="该字段没有可用样本"
        hint="结果集里这个字段全为空（例如筛选出的都是亏损股，PE 没有意义）。"
      />
    );
  }

  return (
    <>
      <div className="row gap-4 wrap">
        <QuantileCell label="最小" value={data.min} unit={data.unit} />
        <QuantileCell label="P25" value={data.p25} unit={data.unit} />
        <QuantileCell label="中位数" value={data.median} unit={data.unit} strong />
        <QuantileCell label="P75" value={data.p75} unit={data.unit} />
        <QuantileCell label="最大" value={data.max} unit={data.unit} />
      </div>
      <HistogramChart data={data} />
      <div className="chart-note">
        <span>
          样本 {data.count} 个（缺失值不参与统计）；分位数用线性插值，直方图 {data.bins.length} 个等宽分箱。
          最大值计入最后一箱。
        </span>
      </div>
    </>
  );
}

function QuantileCell({
  label,
  value,
  unit,
  strong
}: {
  label: string;
  value: number | null;
  unit: string;
  strong?: boolean;
}) {
  return (
    <div className="kpi">
      <span className="kpi-label">{label}</span>
      <span className={`kpi-value is-sm ${strong ? 'fw-700' : ''}`}>
        {value === null ? '—' : `${value.toLocaleString('zh-CN', { maximumFractionDigits: 2 })}${unit}`}
      </span>
    </div>
  );
}

/** 直方图：用横条工厂画「箱 -> 数量」，比柱状图在窄容器里更易读。 */
function HistogramChart({ data }: { data: ScreenerDistribution }) {
  const rows = data.bins.map((bin) => ({
    name: `${bin.from.toLocaleString('zh-CN', { maximumFractionDigits: 1 })}~${bin.to.toLocaleString('zh-CN', { maximumFractionDigits: 1 })}`,
    pct: bin.count,
    note: `${bin.count} 只`
  }));

  const { ref } = useChart(hbar, rows, { unit: ' 只', valueKey: 'pct' });

  return <div className="chart chart-lg" ref={ref} />;
}

/** 我的策略：保存、回放、重命名、删除。 */
export function ScreenerStrategiesPanel() {
  const queryClient = useQueryClient();
  const toast = useToast();

  const { data, error, isPending, failureCount, refetch } = useQuery<ScreenerRunList>({
    queryKey: ['screener', 'strategies'],
    queryFn: fetchScreenerStrategies
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['screener', 'strategies'] });
    void queryClient.invalidateQueries({ queryKey: ['screener', 'history'] });
  };

  const renameMutation = useMutation({
    mutationFn: ({ id, name }: { id: number; name: string | null }) => renameScreenerStrategy(id, name),
    onSuccess: () => {
      toast.toast('策略已更新', 'ok');
      invalidate();
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => deleteScreenerRun(id),
    onSuccess: () => {
      toast.toast('已删除', 'ok');
      invalidate();
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const replayMutation = useMutation({
    mutationFn: (id: number) => replayScreenerRun(id),
    onSuccess: () => {
      toast.toast('已按策略条件重新筛选（结果见上方表格）', 'ok');
      invalidate();
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  if (isPending) {
    return <div className="sa-boot" style={{ height: 120 }}>正在载入策略…</div>;
  }

  if (!data) {
    return <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />;
  }

  if (data.items.length === 0) {
    return (
      <EmptyState
        title="还没有保存的策略"
        hint="先在上方设置条件并筛选，再点结果区右上角的「存为策略」；保存后可一键回放。"
      />
    );
  }

  return (
    <div className="tbl-wrap">
      <table className="tbl is-comfort">
        <thead>
          <tr>
            <th>策略名</th>
            <th>条件摘要</th>
            <th className="num">上次命中</th>
            <th>保存时间</th>
            <th className="col-actions" style={{ width: 200 }}>
              操作
            </th>
          </tr>
        </thead>
        <tbody>
          {data.items.map((row) => (
            <tr key={row.id}>
              <td className="fs-12 fw-600">{row.name}</td>
              <td className="fs-11 t-2">{row.summary ?? '—'}</td>
              <td className="num">{row.total}</td>
              <td className="fs-11 t-3">{row.createdAt}</td>
              <td className="col-actions">
                <span className="row gap-2">
                  <button
                    type="button"
                    className="btn btn-sm btn-outline"
                    disabled={replayMutation.isPending}
                    onClick={() => replayMutation.mutate(row.id)}
                  >
                    回放
                  </button>
                  <button
                    type="button"
                    className="btn btn-sm btn-ghost"
                    onClick={() => {
                      const next = window.prompt('新的策略名（留空表示取消策略标记）', row.name ?? '');
                      if (next !== null) {
                        renameMutation.mutate({ id: row.id, name: next.trim() === '' ? null : next.trim() });
                      }
                    }}
                  >
                    改名
                  </button>
                  <button
                    type="button"
                    className="btn btn-sm btn-ghost"
                    disabled={deleteMutation.isPending}
                    onClick={() => deleteMutation.mutate(row.id)}
                  >
                    删除
                  </button>
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** 筛选日志与导出记录。 */
export function ScreenerHistoryPanel() {
  const queryClient = useQueryClient();
  const toast = useToast();

  const { data: history, isPending } = useQuery<ScreenerRunList>({
    queryKey: ['screener', 'history'],
    queryFn: fetchScreenerHistory
  });

  const { data: exports } = useQuery({
    queryKey: ['screener', 'exports'],
    queryFn: fetchExportLogs
  });

  const saveMutation = useMutation({
    mutationFn: ({ id, name }: { id: number; name: string }) =>
      import('@/features/analysis/api').then((module) => module.saveScreenerStrategy(name, id)),
    onSuccess: () => {
      toast.toast('已存为策略', 'ok');
      void queryClient.invalidateQueries({ queryKey: ['screener', 'strategies'] });
      void queryClient.invalidateQueries({ queryKey: ['screener', 'history'] });
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  if (isPending) {
    return <div className="sa-boot" style={{ height: 120 }}>正在载入筛选日志…</div>;
  }

  return (
    <>
      <div className="card-head">
        <span className="card-title">筛选日志</span>
        <span className="card-sub">
          {history?.items.length ?? 0} 条 · 策略 {history?.strategies ?? 0}
          {history && history.quota >= 0 ? ` / ${history.quota}` : '（不限）'}
        </span>
      </div>
      <div className="card-body is-flush">
        {(history?.items.length ?? 0) === 0 ? (
          <EmptyState title="还没有筛选记录" hint="每次点「开始筛选」都会留一条日志，便于回溯在筛什么。" />
        ) : (
          <div className="tbl-wrap">
            <table className="tbl">
              <thead>
                <tr>
                  <th>时间</th>
                  <th>条件摘要</th>
                  <th className="num">命中</th>
                  <th>来源</th>
                  <th className="col-actions" style={{ width: 130 }}>
                    操作
                  </th>
                </tr>
              </thead>
              <tbody>
                {(history?.items ?? []).map((row: ScreenerRun) => (
                  <tr key={row.id} data-chg={row.isStrategy ? 'up' : 'flat'}>
                    <td className="fs-11 t-3">{row.createdAt}</td>
                    <td className="fs-11 t-2">{row.summary ?? '—'}</td>
                    <td className="num">{row.total}</td>
                    <td className="fs-11 t-3">
                      {row.isStrategy ? <span className="tag tag-up">{row.name}</span> : row.presetKey ? `预设 ${row.presetKey}` : '自定义'}
                    </td>
                    <td className="col-actions">
                      {!row.isStrategy ? (
                        <button
                          type="button"
                          className="btn btn-sm btn-ghost"
                          disabled={saveMutation.isPending}
                          onClick={() => {
                            const name = window.prompt('策略名（用于保存这次筛选的条件）', '');
                            if (name && name.trim()) {
                              saveMutation.mutate({ id: row.id, name: name.trim() });
                            }
                          }}
                        >
                          存为策略
                        </button>
                      ) : (
                        <span className="fs-11 t-3">已保存</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
      <div className="card-head mt-4">
        <span className="card-title">导出记录</span>
        <span className="card-sub">{exports?.length ?? 0} 条</span>
      </div>
      <div className="card-body is-flush">
        {!exports || exports.length === 0 ? (
          <EmptyState title="还没有导出记录" hint="导出 CSV 后会在这里留下记录（数据集、格式、行数）。" />
        ) : (
          <div className="tbl-wrap">
            <table className="tbl">
              <thead>
                <tr>
                  <th>时间</th>
                  <th>数据集</th>
                  <th>格式</th>
                  <th className="num">行数</th>
                </tr>
              </thead>
              <tbody>
                {exports.map((row) => (
                  <tr key={row.id}>
                    <td className="fs-11 t-3">{row.createdAt}</td>
                    <td className="fs-11">{row.dataset}</td>
                    <td className="fs-11 t-2">{row.format.toUpperCase()}</td>
                    <td className="num">{row.rows}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </>
  );
}
