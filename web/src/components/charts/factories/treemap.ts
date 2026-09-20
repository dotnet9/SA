import type { ChartFactory } from '../useChart';

/** 热力图条目（行业/概念板块）。 */
export interface TreemapItem {
  name: string;
  /** 涨跌幅（百分数）。 */
  pct: number;
  /** 附加展示信息：主力净流入（亿元）。 */
  flow?: number;
  /** 领涨股。 */
  leader?: string;
  /** 代码（板块码或证券代码）。 */
  code?: string;
}

/**
 * 树图（行业热力）。移植自原型 `charts.js` 的 `F.treemap`。
 *
 * 面积 = |涨跌幅| 强度，颜色 = 涨跌方向，透明度随强度加深：
 * 一眼看出「哪个行业今天最猛」，而不是只比色块大小。
 */
export const treemap: ChartFactory<readonly TreemapItem[]> = (h, data) => {
  if (!data || data.length === 0) {
    return {};
  }

  return {
    tooltip: h.tooltip({
      formatter: (params: { data?: { meta?: TreemapItem } }) => {
        const item = params.data?.meta;
        if (!item) {
          return '';
        }

        const lines = [`<b>${item.name}</b>`, `涨跌幅：${item.pct >= 0 ? '+' : ''}${item.pct}%`];
        if (item.flow !== undefined) {
          lines.push(`主力净流入：${item.flow >= 0 ? '+' : ''}${item.flow} 亿`);
        }
        if (item.leader) {
          lines.push(`领涨：${item.leader}`);
        }
        return lines.join('<br>');
      }
    }),
    series: [
      {
        type: 'treemap',
        roam: false,
        nodeClick: false,
        breadcrumb: { show: false },
        left: 0,
        right: 0,
        top: 0,
        bottom: 0,
        itemStyle: { borderColor: h.cv('--bg-root'), borderWidth: 2, gapWidth: 2 },
        label: { show: true, fontSize: 11, color: '#fff', formatter: '{b}' },
        upperLabel: { show: false },
        levels: [{ itemStyle: { borderWidth: 0, gapWidth: 2 } }],
        data: data.map((item) => {
          const intensity = Math.min(1, Math.abs(item.pct) / 4);
          return {
            name: item.name,
            // 面积下限保证小涨跌幅的板块仍可点击与辨认
            value: Math.max(4, Math.abs(item.pct) * 20 + 12),
            meta: item,
            itemStyle: {
              color: item.pct >= 0 ? h.up() : h.down(),
              opacity: 0.28 + intensity * 0.62
            }
          };
        })
      }
    ]
  };
};
