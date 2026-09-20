import { useMemo, useState } from 'react';
import { Link, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { graph, useChart, type GraphEdge, type GraphNode } from '@/components/charts';
import { EmptyState, ErrorState } from '@/components/ui/States';
import { fetchEvents, type Topology } from '@/features/events/api';

/**
 * 四种拓扑图总览。
 *
 * 结构与 `design/web/topology.html` 对应：四图切换标签 → 单图放大 / 四图并排 → 每图带口径与节点边数。
 *
 * <b>与原型的一处偏差（已在页面上写明）</b>：原型的四图是
 * 「事件因果网络 / 公司股权关系 / 产业链传导 / 自上而下影响传导」，
 * 而本实现提供的是「股东拓扑 / 行业拓扑 / 事件拓扑 / 交易对手拓扑」。
 * 差异来自数据可得性：产业链传导需要专门的产业数据源（上游成本占比、供货占比），
 * 本轮没有可用的公开源，因此不画这张图——不画比画一张推测出来的关系图更负责。
 * 「自上而下影响传导」同理属于宏观到个股的传导模型，本轮未建模。
 */
export function TopologyPage() {
  const { code = '' } = useParams();

  const [view, setView] = useState<'one' | 'all'>('one');
  const [active, setActive] = useState<string | null>(null);

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['stock', code, 'events'],
    queryFn: () => fetchEvents(code),
    enabled: code.length > 0
  });

  if (!code) {
    return (
      <>
        <Head name="" />
        <EmptyState title="缺少证券代码" hint="请从个股页进入，或在地址栏补上代码，例如 /topology/300750。" />
      </>
    );
  }

  if (isPending) {
    return <div className="sa-boot">正在载入 {code} 的拓扑数据…</div>;
  }

  if (!data) {
    return (
      <>
        <Head name={code} />
        <div className="card">
          <div className="card-body">
            <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />
          </div>
        </div>
      </>
    );
  }

  const available = data.topologies.filter((item) => item.available);
  const current = data.topologies.find((item) => item.key === active) ?? data.topologies[0];

  return (
    <>
      <Head name={data.name} />

      {/* 四图切换标签 */}
      <div className="topo-tabs">
        {data.topologies.map((topology, index) => (
          <div
            key={topology.key}
            className={`topo-tab ${current?.key === topology.key && view === 'one' ? 'is-active' : ''}`}
            onClick={() => {
              setActive(topology.key);
              setView('one');
            }}
          >
            <div className="row-between">
              <span className="tt-num">{index + 1}</span>
              <span className={`tag ${topology.available ? 'tag-up' : 'tag-outline'}`}>
                {topology.available ? '可用' : '数据不足'}
              </span>
            </div>
            <div className="tt-name">{topology.name}</div>
            <div className="tt-desc">{topology.description}</div>
            <div className="tt-meta">
              {topology.available ? `${topology.nodes.length} 节点 · ${topology.edges.length} 边` : topology.unavailableReason}
            </div>
          </div>
        ))}
      </div>

      <div className="row gap-2 wrap mt-3" style={{ alignItems: 'center' }}>
        <span className="segmented">
          <span className={view === 'one' ? 'is-active' : undefined} onClick={() => setView('one')}>
            单图放大
          </span>
          <span className={view === 'all' ? 'is-active' : undefined} onClick={() => setView('all')}>
            四图并排（{available.length}/{data.topologies.length} 可用）
          </span>
        </span>
        <Link className="btn btn-sm btn-ghost" to={`/stock/${code}/events`} style={{ marginLeft: 'auto' }}>
          进入事件与影响模块 →
        </Link>
      </div>

      {view === 'one' ? (
        current ? (
          <div className="card is-accent mt-3">
            <div className="card-head">
              <span className="card-title">{current.name}</span>
              <span className="card-sub">{current.description}</span>
              <div className="card-tools">
                <span className="tag tag-outline">
                  {current.available
                    ? `${current.nodes.length} 节点 · ${current.edges.length} 边`
                    : '数据不足'}
                </span>
              </div>
            </div>
            <div className="card-body">
              {!current.available ? (
                <EmptyState title="该拓扑图暂不可用" hint={current.unavailableReason ?? '数据不足。'} />
              ) : (
                <TopologyCanvas topology={current} height={560} />
              )}
            </div>
          </div>
        ) : null
      ) : (
        <div className="compare-grid mt-3">
          {data.topologies.map((topology) => (
            <div key={topology.key} className="card">
              <div className="card-head">
                <span className="card-title">{topology.name}</span>
                <span className="card-sub">
                  {topology.available ? `${topology.nodes.length} 节点 · ${topology.edges.length} 边` : '数据不足'}
                </span>
              </div>
              <div className="card-body">
                {!topology.available ? (
                  <EmptyState title="暂不可用" hint={topology.unavailableReason ?? '数据不足。'} />
                ) : (
                  <TopologyCanvas topology={topology} height={360} />
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      <div className="legend-block mt-4">
        <b>四张图各自回答什么</b>
        <br />
        <b>股东拓扑</b>：谁在控制这家公司 —— 前十大股东与持股比例（边上的数字是占总股本比例）。
        <br />
        <b>行业拓扑</b>：它在行业里的位置 —— 所属行业、同业与各自涨跌（边的颜色表示同业当日涨跌方向）。
        <br />
        <b>事件拓扑</b>：哪些事件在影响它 —— 按类型聚合，边的颜色表示该类型的整体倾向。
        <br />
        <b>交易对手拓扑</b>：谁在跟它做大宗交易 —— 按对手方聚合成交金额（同一营业部只出现一个节点）。
        <br />
        <b>未提供的两张图</b>：原型的「产业链传导」需要上游成本占比与供货占比数据、「自上而下影响传导」
        需要宏观到个股的传导模型，本轮都没有可用的公开数据源，因此不画——不画比画一张推测图更负责。
        <br />
        节点可拖动、画布可滚轮缩放；节点较多（&gt; 24）时自动切换为环形布局以便阅读。
      </div>
    </>
  );
}

/** 拓扑画布：把接口数据映射成图工厂的输入。 */
function TopologyCanvas({ topology, height }: { topology: Topology; height: number }) {
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

  const { ref } = useChart(graph, {
    nodes,
    edges,
    categories: topology.categories,
    showEdgeLabel: true,
    layout: nodes.length > 24 ? 'circular' : 'force'
  });

  return <div className="chart" style={{ height }} ref={ref} />;
}

function Head({ name }: { name: string }) {
  return (
    <div className="sa-pagehead">
      <div>
        <div className="breadcrumb">
          <Link to="/market">市场概览</Link>
          <span className="sep">/</span>
          <span>四种拓扑图总览</span>
        </div>
        <h1>四种拓扑图总览</h1>
        <div className="sub">
          {name ? `${name} · ` : ''}同一支股票下，「谁影响谁」的四种表达方式对照 · 节点可拖拽 · 画布可缩放 ·
          每张图都标注口径
        </div>
      </div>
    </div>
  );
}
