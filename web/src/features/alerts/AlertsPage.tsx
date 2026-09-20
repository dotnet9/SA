import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { EmptyState, ErrorState, FreshnessNote } from '@/components/ui/States';
import { errorText } from '@/lib/errorText';
import { useToast } from '@/providers/ToastProvider';
import { createAlertRule, fetchAlertRules, removeAlertRule, updateAlertRule, type AlertRule } from './api';

/**
 * 提醒规则。
 *
 * 结构与 `design/web/alerts.html` 对应：规则统计 → 新建规则 → 规则列表 → 口径说明。
 *
 * 阈值单位随规则类型变化（元 / % / 倍 / 日），因此单位由服务端下发的类型定义决定，
 * 不在前端硬编码——否则新增规则类型时前端会给出错误的单位提示。
 */
export function AlertsPage() {
  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['alerts'],
    queryFn: fetchAlertRules
  });

  if (isPending) {
    return <div className="sa-boot">正在载入提醒规则…</div>;
  }

  if (!data) {
    return (
      <>
        <Head total={0} quota={0} />
        <div className="card">
          <div className="card-body">
            <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />
          </div>
        </div>
      </>
    );
  }

  return <Content data={data} />;
}

function Head({ total, quota }: { total: number; quota: number }) {
  return (
    <div className="sa-pagehead">
      <div>
        <div className="breadcrumb">
          <Link to="/market">市场概览</Link>
          <span className="sep">/</span>
          <span>提醒规则</span>
        </div>
        <h1>提醒规则</h1>
        <div className="sub">
          {total} / {quota} 条 · 触发后写入通知中心并经实时通道推送
        </div>
      </div>
    </div>
  );
}

function Content({ data }: { data: Awaited<ReturnType<typeof fetchAlertRules>> }) {
  const queryClient = useQueryClient();
  const toast = useToast();

  const [code, setCode] = useState('');
  const [ruleType, setRuleType] = useState(data.types[0]?.type ?? '');
  const [threshold, setThreshold] = useState('');
  const [note, setNote] = useState('');

  const selectedType = useMemo(
    () => data.types.find((option) => option.type === ruleType),
    [data.types, ruleType]
  );

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['alerts'] });

  const createMutation = useMutation({
    mutationFn: () =>
      createAlertRule(
        code.trim(),
        ruleType,
        threshold.trim() === '' ? null : Number(threshold),
        note.trim() === '' ? undefined : note.trim()
      ),
    onSuccess: async () => {
      toast.toast('规则已创建', 'ok');
      setCode('');
      setThreshold('');
      setNote('');
      await invalidate();
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, enabled }: { id: string; enabled: boolean }) => updateAlertRule(id, { enabled }),
    onSuccess: async () => {
      await invalidate();
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const removeMutation = useMutation({
    mutationFn: (id: string) => removeAlertRule(id),
    onSuccess: async () => {
      toast.toast('规则已删除（已产生的通知保留）', 'ok');
      await invalidate();
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const triggered = data.rules.filter((rule) => rule.triggerCount > 0).length;
  const enabled = data.rules.filter((rule) => rule.enabled).length;
  const quotaReached = data.total >= data.quota;

  return (
    <>
      <Head total={data.total} quota={data.quota} />

      <div className="grid grid-4">
        <StatCard label="规则总数" value={`${data.total} / ${data.quota}`} />
        <StatCard label="启用中" value={String(enabled)} />
        <StatCard label="曾触发" value={String(triggered)} />
        <StatCard label="累计触发次数" value={String(data.rules.reduce((sum, rule) => sum + rule.triggerCount, 0))} />
      </div>

      {/* 新建规则 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">新建规则</span>
          <span className="card-sub">{quotaReached ? '已达规则上限' : '阈值单位随类型变化'}</span>
        </div>
        <div className="card-body">
          <div className="row gap-3 wrap" style={{ alignItems: 'flex-end' }}>
            <label className="col gap-1">
              <span className="label" style={{ width: 'auto' }}>
                证券代码
              </span>
              <input
                className="input input-sm"
                style={{ width: 130 }}
                placeholder="如 300750"
                value={code}
                onChange={(event) => setCode(event.target.value)}
              />
            </label>

            <label className="col gap-1">
              <span className="label" style={{ width: 'auto' }}>
                规则类型
              </span>
              <select
                className="input input-sm"
                style={{ width: 200 }}
                value={ruleType}
                onChange={(event) => setRuleType(event.target.value)}
              >
                {data.types.map((option) => (
                  <option key={option.type} value={option.type}>
                    {option.name}
                  </option>
                ))}
              </select>
            </label>

            <label className="col gap-1">
              <span className="label" style={{ width: 'auto' }}>
                阈值 {selectedType?.unit ? `（${selectedType.unit}）` : '（无需填）'}
              </span>
              <input
                className="input input-sm"
                style={{ width: 130 }}
                placeholder={selectedType?.unit ?? '—'}
                value={threshold}
                disabled={selectedType?.unit === null}
                onChange={(event) => setThreshold(event.target.value)}
              />
            </label>

            <label className="col gap-1">
              <span className="label" style={{ width: 'auto' }}>
                备注
              </span>
              <input
                className="input input-sm"
                style={{ width: 220 }}
                placeholder="为什么关注这一条（可选）"
                value={note}
                onChange={(event) => setNote(event.target.value)}
              />
            </label>

            <button
              type="button"
              className="btn btn-sm btn-primary"
              disabled={createMutation.isPending || code.trim().length === 0 || quotaReached}
              onClick={() => createMutation.mutate()}
            >
              创建规则
            </button>
          </div>

          {selectedType ? <div className="chart-note mt-3">{selectedType.hint}</div> : null}
        </div>
      </div>

      {/* 规则列表 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">规则列表</span>
          <span className="card-sub">停用的规则不再参与评估</span>
        </div>
        <div className="card-body is-flush">
          {data.rules.length === 0 ? (
            <EmptyState
              title="还没有提醒规则"
              hint="在上方新建一条规则；触发后会在通知中心留下记录，并通过实时通道推送到当前页面。"
            />
          ) : (
            <div className="tbl-wrap">
              <table className="tbl is-comfort">
                <thead>
                  <tr>
                    <th>名称 / 代码</th>
                    <th>规则</th>
                    <th className="num">阈值</th>
                    <th>备注</th>
                    <th>状态</th>
                    <th>最近触发</th>
                    <th className="num">触发次数</th>
                    <th className="col-actions" style={{ width: 130 }}>
                      操作
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {data.rules.map((rule) => (
                    <AlertRow
                      key={rule.id}
                      rule={rule}
                      onToggle={() => toggleMutation.mutate({ id: rule.id, enabled: !rule.enabled })}
                      onRemove={() => removeMutation.mutate(rule.id)}
                    />
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      <FreshnessNote asOf={data.asOf} source="行情快照 + 本地日线（评估节拍 30 秒，单条规则冷却 30 分钟）" />

      <div className="legend-block mt-4">
        <b>数据来源与口径</b>
        <br />
        规则评估每 30 秒一轮，判定只用当前快照与本地日线的确定值，不做预测或模糊匹配。
        <br />
        同一条规则触发后进入 30 分钟冷却，避免价格在阈值附近震荡时反复通知。
        <br />
        数据不足时保持沉默（例如日线不足 20 根无法判断「跌破均线」），并在规则状态里给出原因。
        <br />
        规则数量受角色配额 <code>alert.max</code> 限制；删除规则不会删除已产生的通知（历史可回溯）。
        <br />
        「事件触发」类规则由事件链路直接产生通知，评估器不重复判定，避免同一条事件推两次。
      </div>
    </>
  );
}

function StatCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="card">
      <div className="card-body is-tight">
        <div className="kpi">
          <span className="kpi-label">{label}</span>
          <span className="kpi-value is-sm">{value}</span>
        </div>
      </div>
    </div>
  );
}

function AlertRow({
  rule,
  onToggle,
  onRemove
}: {
  rule: AlertRule;
  onToggle: () => void;
  onRemove: () => void;
}) {
  return (
    <tr>
      <td>
        <Link className="stock-cell" to={`/stock/${rule.code}`}>
          <span className="sc-name">{rule.name}</span>
          <span className="sc-code">{rule.code}</span>
        </Link>
      </td>
      <td className="fs-12">{rule.ruleTypeName}</td>
      <td className="num mono">
        {rule.threshold === null ? '—' : `${rule.threshold}${rule.thresholdUnit ?? ''}`}
      </td>
      <td className="fs-11 t-2">{rule.note ?? '—'}</td>
      <td>
        <span className={`tag ${rule.enabled ? 'tag-up' : 'tag-outline'}`}>{rule.enabled ? '启用中' : '已停用'}</span>
      </td>
      <td className="fs-11 t-3">{rule.lastTriggeredAt ?? '未触发'}</td>
      <td className="num">{rule.triggerCount}</td>
      <td className="col-actions">
        <span className="row gap-2">
          <button type="button" className="btn btn-sm btn-outline" onClick={onToggle}>
            {rule.enabled ? '停用' : '启用'}
          </button>
          <button type="button" className="btn btn-sm btn-ghost" onClick={onRemove}>
            删除
          </button>
        </span>
      </td>
    </tr>
  );
}
