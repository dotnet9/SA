import { useMemo, useState } from 'react';
import { useParams } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { combo, graph, useChart, type GraphEdge, type GraphNode } from '@/components/charts';
import { EmptyState, ErrorState, FreshnessNote } from '@/components/ui/States';
import { errorText } from '@/lib/errorText';
import { useToast } from '@/providers/ToastProvider';
import { annotateEvent, fetchEvents, removeAnnotation, type EventItem, type EventTimeline, type Topology } from './api';

/**
 * 事件与影响。
 *
 * 结构与 `design/web/stock-events.html` 对应：结论 → 事件强度趋势 → 时间线 → 四种拓扑图 → 口径说明。
 *
 * 时间线与拓扑图都由本地已落地的结构化数据派生，因此点开一条事件能看到它与各模块页的数字同源。
 * 供应链拓扑需要专门的产业数据源，暂无公开源，页面显式说明「不提供」而不是画一张推测图。
 */
export function StockEventsPage() {
  const { code = '' } = useParams();

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['stock', code, 'events'],
    queryFn: () => fetchEvents(code),
    enabled: code.length > 0,
    // 事件派生于财务/股权/资金面：首次打开时这些数据可能还在采集中，
    // 因此服务端会带 collecting 标记，界面据此每 5 秒自动刷新一次（最多持续 2 分钟）
    refetchInterval: (query) => {
      const current = query.state.data;
      if (!current?.collecting || !current) {
        return false;
      }

      const attempts = query.state.dataUpdateCount;
      return attempts <= 24 ? 5000 : false;
    }
  });

  if (isPending) {
    return <div className="sa-boot">正在载入 {code} 的事件…</div>;
  }

  if (!data) {
    return (
      <>
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

function Content({ data }: { data: EventTimeline }) {
  const [typeFilter, setTypeFilter] = useState<string>('all');

  const filtered = useMemo(
    () => (typeFilter === 'all' ? data.events : data.events.filter((item) => item.type === typeFilter)),
    [data.events, typeFilter]
  );

  return (
    <>

      {data.insights.length > 0 ? (
        <div className="row gap-2 wrap">
          {data.insights.map((text) => (
            <span key={text} className="tag tag-outline">
              {text}
            </span>
          ))}
        </div>
      ) : null}

      {/* 事件强度趋势 */}
      <div className="card mt-3">
        <div className="card-head">
          <span className="card-title">事件强度趋势</span>
          <span className="card-sub">柱 = 影响强度（正为积极、负为消极）· 线 = 影响条数</span>
        </div>
        <div className="card-body">
          {data.events.length === 0 ? (
            <EmptyState title="暂无事件" hint="事件派生自财务、股权与资金面数据，采集完成后自动出现。" />
          ) : (
            <EventTrendChart data={data} />
          )}
        </div>
      </div>

      {/* 类型筛选 */}
      <div className="row gap-2 wrap mt-4">
        <span className="segmented">
          <span className={typeFilter === 'all' ? 'is-active' : undefined} onClick={() => setTypeFilter('all')}>
            全部 {data.events.length}
          </span>
          {data.summary.map((item) => (
            <span
              key={item.type}
              className={typeFilter === item.type ? 'is-active' : undefined}
              onClick={() => setTypeFilter(item.type)}
            >
              {item.typeName} {item.count}
            </span>
          ))}
        </span>
      </div>

      {/* 时间线 */}
      <div className="card mt-3">
        <div className="card-head">
          <span className="card-title">事件时间线</span>
          <span className="card-sub">按日期倒序 · 显示条数 {filtered.length}</span>
        </div>
        <div className="card-body">
          {filtered.length === 0 ? (
            <EmptyState title="该类型暂无事件" hint="切换其他类型标签，或等待数据采集完成。" />
          ) : (
            <div className="col gap-3">
              {filtered.map((item) => (
                <EventRow key={item.key} item={item} code={data.code} canAnnotate={data.canAnnotate} />
              ))}
            </div>
          )}
        </div>
      </div>

      {/* 四种拓扑图 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">四种拓扑图</span>
          <span className="card-sub">谁在控制 · 行业位置 · 事件影响 · 交易对手</span>
        </div>
        <div className="card-body">
          <TopologyTabs topologies={data.topologies} />
        </div>
      </div>
      <FreshnessNote asOf={data.asOf} source="由本模块已落地的结构化数据派生（不新增采集源）" />

    </>
  );
}

/** 单条事件（含人工标注入口）。 */
function EventRow({ item, code, canAnnotate }: { item: EventItem; code: string; canAnnotate: boolean }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const [editing, setEditing] = useState(false);
  const [tone, setTone] = useState(item.tone);
  const [impact, setImpact] = useState(item.impact);
  const [note, setNote] = useState(item.annotationNote ?? '');

  const saveMutation = useMutation({
    mutationFn: () => annotateEvent(code, item.key, tone, impact, note),
    onSuccess: async () => {
      toast.toast('已保存标注', 'ok');
      setEditing(false);
      await queryClient.invalidateQueries({ queryKey: ['stock', code, 'events'] });
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const clearMutation = useMutation({
    mutationFn: () => removeAnnotation(code, item.key),
    onSuccess: async () => {
      toast.toast('已恢复为规则判读', 'ok');
      setEditing(false);
      await queryClient.invalidateQueries({ queryKey: ['stock', code, 'events'] });
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  return (
    <div className="col gap-1">
      <div className="row-between wrap gap-2">
        <span className="row gap-2 wrap" style={{ alignItems: 'center' }}>
          <span className="mono fs-11 t-3">{item.date}</span>
          <span className="tag tag-outline">{item.typeName}</span>
          <span className={`fw-600 fs-12 ${toneText(item.tone)}`}>{item.title}</span>
          {item.annotated ? <span className="tag tag-warn">已人工标注</span> : null}
        </span>
        <span className="row gap-2" style={{ alignItems: 'center' }}>
          {/* 影响强度用点阵表示，一眼看出强弱 */}
          <span className="fs-11 t-3" title={`影响强度 ${item.impact}/5`}>
            {'●'.repeat(item.impact)}
            {'○'.repeat(Math.max(0, 5 - item.impact))}
          </span>
          <span className="fs-11 t-3">{item.source}</span>
          {canAnnotate ? (
            <button type="button" className="btn btn-sm btn-ghost" onClick={() => setEditing((value) => !value)}>
              {editing ? '收起' : '标注'}
            </button>
          ) : null}
        </span>
      </div>

      {item.detail ? <div className="fs-11 t-2">{item.detail}</div> : null}
      {item.annotated && item.annotationNote ? (
        <div className="fs-11 t-3">人工备注：{item.annotationNote}</div>
      ) : null}

      {editing && canAnnotate ? (
        <div className="row gap-2 wrap mt-2" style={{ alignItems: 'center' }}>
          <span className="segmented">
            {(['up', 'neutral', 'down'] as const).map((value) => (
              <span key={value} className={tone === value ? 'is-active' : undefined} onClick={() => setTone(value)}>
                {value === 'up' ? '积极' : value === 'down' ? '消极' : '中性'}
              </span>
            ))}
          </span>
          <span className="segmented">
            {[1, 2, 3, 4, 5].map((value) => (
              <span key={value} className={impact === value ? 'is-active' : undefined} onClick={() => setImpact(value)}>
                {value}
              </span>
            ))}
          </span>
          <input
            className="input input-sm"
            style={{ width: 220 }}
            placeholder="备注（为什么这样判读）"
            value={note}
            onChange={(event) => setNote(event.target.value)}
          />
          <button
            type="button"
            className="btn btn-sm btn-primary"
            disabled={saveMutation.isPending}
            onClick={() => saveMutation.mutate()}
          >
            保存
          </button>
          {item.annotated ? (
            <button
              type="button"
              className="btn btn-sm btn-ghost"
              disabled={clearMutation.isPending}
              onClick={() => clearMutation.mutate()}
            >
              恢复规则判读
            </button>
          ) : null}
        </div>
      ) : null}

      <div className="divider" />
    </div>
  );
}

/** 四种拓扑图切换。 */
function TopologyTabs({ topologies }: { topologies: Topology[] }) {
  const [active, setActive] = useState(topologies[0]?.key ?? 'shareholder');
  const current = topologies.find((item) => item.key === active) ?? topologies[0];

  return (
    <>
      <div className="row gap-2 wrap">
        <span className="segmented">
          {topologies.map((item) => (
            <span key={item.key} className={active === item.key ? 'is-active' : undefined} onClick={() => setActive(item.key)}>
              {item.name}
            </span>
          ))}
        </span>
      </div>

      {current ? (
        <>
          <div className="chart-note mt-3">{current.description}</div>
          {!current.available ? (
            <EmptyState title="该拓扑图暂不可用" hint={current.unavailableReason ?? '数据不足。'} />
          ) : (
            <TopologyChart topology={current} />
          )}
        </>
      ) : null}
    </>
  );
}

function TopologyChart({ topology }: { topology: Topology }) {
  const nodes: GraphNode[] = useMemo(
    () =>
      topology.nodes.map((node) => ({
        id: node.id,
        name: node.name,
        category: node.category,
        value: Math.abs(node.value),
        highlight: node.highlight,
        note: node.note ?? undefined
      })),
    [topology.nodes]
  );

  const edges: GraphEdge[] = useMemo(
    () =>
      topology.edges.map((edge) => ({
        source: edge.source,
        target: edge.target,
        label: edge.label ?? undefined,
        tone: edge.tone === 'up' ? 'up' : edge.tone === 'down' ? 'down' : 'neutral'
      })),
    [topology.edges]
  );

  // 布局在数据里给出（工厂负责渲染），不通过 options 传，避免与工厂的选项类型混淆
  const { ref } = useChart(graph, {
    nodes,
    edges,
    categories: topology.categories,
    showEdgeLabel: true,
    layout: nodes.length > 24 ? 'circular' : 'force'
  });

  return (
    <>
      <div className="chart chart-2xl" ref={ref} />
      <div className="chart-note">
        共 {nodes.length} 个节点、{edges.length} 条边；可拖动节点、滚轮缩放。节点较多时自动切换为环形布局以便阅读。
      </div>
    </>
  );
}

/** 事件强度趋势。 */
function EventTrendChart({ data }: { data: EventTimeline }) {
  // 按月份聚合：逐条事件画柱会因日期疏密不均而难以比较
  const monthly = useMemo(() => {
    const buckets = new Map<string, { intensity: number; count: number }>();

    for (const item of [...data.events].reverse()) {
      const month = item.date.slice(0, 7);
      const bucket = buckets.get(month) ?? { intensity: 0, count: 0 };
      bucket.intensity += item.tone === 'down' ? -item.impact : item.tone === 'up' ? item.impact : 0;
      bucket.count += 1;
      buckets.set(month, bucket);
    }

    return [...buckets.entries()].sort((a, b) => a[0].localeCompare(b[0]));
  }, [data.events]);

  const { ref } = useChart(
    combo,
    {
      labels: monthly.map(([month]) => month.slice(2)),
      bars: [{ name: '净影响强度', data: monthly.map(([, bucket]) => bucket.intensity) }],
      lines: [{ name: '事件条数', data: monthly.map(([, bucket]) => bucket.count), onRightAxis: true }],
      leftAxisName: '强度',
      rightAxisName: '条数'
    },
    {}
  );

  return (
    <>
      <div className="chart chart-md" ref={ref} />
      <div className="chart-note">按月聚合：净影响强度为正说明该月积极事件占优，为负说明消极事件占优。</div>
    </>
  );
}

function toneText(tone: string): string {
  return tone === 'up' ? 't-up' : tone === 'down' ? 't-down' : 't-2';
}
