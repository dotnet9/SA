import type { ChartFactory } from '../useChart';

/** 图节点。 */
export interface GraphNode {
  /** 节点 Id（必须唯一）。 */
  id: string;
  /** 显示名。 */
  name: string;
  /** 分类下标（对应 categories）。 */
  category?: number;
  /** 节点相对大小（会映射成符号尺寸）。 */
  value?: number;
  /** 是否高亮（如当前查看的标的）。 */
  highlight?: boolean;
  /** 悬浮说明。 */
  note?: string;
}

/** 图的边。 */
export interface GraphEdge {
  source: string;
  target: string;
  /** 边上的标注文案（如持股比例、影响方向）。 */
  label?: string;
  /** 边的语义：影响方向用 up / down 着色，neutral 用中性色。 */
  tone?: 'up' | 'down' | 'neutral';
}

/** 图选项。 */
export interface GraphOptions {
  nodes: readonly GraphNode[];
  edges: readonly GraphEdge[];
  /** 分类名（图例）。 */
  categories?: readonly string[];
  /** 是否显示边上的标注文案。 */
  showEdgeLabel?: boolean;
  /** 布局：force（力导向）或 circular（环形，节点多时更清晰）。 */
  layout?: 'force' | 'circular';
}

/**
 * 关系图。移植自原型 `charts.js` 的 `F.graph`。
 *
 * 四种拓扑图（股东 / 行业 / 事件 / 交易对手）共用这一个工厂，靠节点分类与边着色区分语义：
 * 关系图的形状本身不承载含义，承载含义的是<b>分类图例与边上的标注</b>，
 * 因此这里把两者都做成显式的配置而不是靠颜色猜测。
 */
export const graph: ChartFactory<GraphOptions> = (h, cfg) => {
  if (!cfg || cfg.nodes.length === 0) {
    return {};
  }

  const pal = h.palette();
  const layout = cfg.layout ?? 'force';
  const categories = (cfg.categories ?? []).map((name, index) => ({
    name,
    itemStyle: { color: pal[index % pal.length] }
  }));

  return {
    tooltip: h.tooltip({
      formatter: (params: { dataType?: string; data?: GraphNode & { label?: string } }) => {
        const data = params.data;
        if (!data) {
          return '';
        }

        if (params.dataType === 'edge') {
          return `<b>${data.label ?? '关联'}</b>`;
        }

        return `<b>${data.name}</b>${data.note ? `<br>${data.note}` : ''}`;
      }
    }),
    legend:
      categories.length > 0
        ? h.legend({
            data: categories.map((category) => category.name),
            top: 0,
            left: 'center',
            right: 'auto'
          })
        : { show: false },
    series: [
      {
        type: 'graph',
        layout,
        roam: true,
        draggable: true,
        categories,
        // 节点名本身就是信息（股东名、行业名），必须显示
        label: { show: true, fontSize: 11, color: h.cv('--text-1') },
        edgeSymbol: ['none', 'arrow'],
        edgeSymbolSize: 7,
        edgeLabel: {
          show: cfg.showEdgeLabel === true,
          fontSize: 10,
          color: h.cv('--text-3'),
          formatter: (params: { data?: { label?: string } }) => params.data?.label ?? ''
        },
        force: {
          // 斥力与边长按节点数收敛：节点多（行业/事件拓扑）时缩短边长，避免图溢出画布
          repulsion: Math.max(80, 400 - cfg.nodes.length * 6),
          edgeLength: Math.max(50, 160 - cfg.nodes.length * 2),
          gravity: 0.08,
          layoutAnimation: cfg.nodes.length <= 40
        },
        circular: { rotateLabel: true },
        lineStyle: { color: h.cv('--chart-axis'), width: 1.2, curveness: 0.08, opacity: 0.9 },
        data: cfg.nodes.map((node) => ({
          id: node.id,
          name: node.name,
          category: node.category,
          value: node.value ?? 1,
          note: node.note,
          symbolSize: node.highlight ? 30 : 16 + Math.min(14, (node.value ?? 1) / 4),
          itemStyle: node.highlight
            ? { borderColor: h.cv('--text-1'), borderWidth: 2 }
            : undefined
        })),
        links: cfg.edges.map((edge) => ({
          source: edge.source,
          target: edge.target,
          label: edge.label,
          lineStyle:
            edge.tone === 'up'
              ? { color: h.up(), width: 1.6 }
              : edge.tone === 'down'
                ? { color: h.down(), width: 1.6 }
                : undefined
        }))
      }
    ]
  };
};
