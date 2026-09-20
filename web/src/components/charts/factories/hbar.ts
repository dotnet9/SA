import type { ChartFactory } from '../useChart';

/** 横条条目。 */
export interface HBarItem {
  name: string;
  /** 条长取值（通常是涨跌幅百分数）。 */
  pct: number;
  /** 附加展示信息：主力净流入（亿元）。 */
  flow?: number;
  /** 领涨股等补充说明。 */
  note?: string;
}

/** 横条图选项。 */
export interface HBarOptions {
  unit?: string;
  max?: number;
  valueKey?: 'pct' | 'flow';
}

/**
 * 横向条形排行。移植自原型 `charts.js` 的 `F.hbar`。
 *
 * 条长按涨跌幅，颜色按涨跌方向，条端标注数值；适合行业/榜单这类「名称较长、
 * 只需要看排序」的场景（名称放纵轴不需要旋转）。
 */
export const hbar: ChartFactory<readonly HBarItem[], HBarOptions> = (h, data, options) => {
  if (!data || data.length === 0) {
    return {};
  }

  const valueKey = options?.valueKey ?? 'pct';
  const unit = options?.unit ?? '%';
  const values = data.map((item) => (valueKey === 'flow' ? (item.flow ?? 0) : item.pct));

  return {
    tooltip: h.tooltip({
      formatter: (params: { dataIndex: number }) => {
        const item = data[params.dataIndex];
        const lines = [`<b>${item.name}</b>`, `涨跌幅：${item.pct >= 0 ? '+' : ''}${item.pct}%`];
        if (item.flow !== undefined) {
          lines.push(`主力净流入：${item.flow >= 0 ? '+' : ''}${item.flow} 亿`);
        }
        if (item.note) {
          lines.push(item.note);
        }
        return lines.join('<br>');
      }
    }),
    grid: h.grid({ left: 8, right: 64, top: 8, bottom: 8 }),
    xAxis: {
      type: 'value',
      axisLine: { show: false },
      axisLabel: h.axisLabel({ fontSize: 10 }),
      splitLine: h.splitLine()
    },
    yAxis: {
      type: 'category',
      data: data.map((item) => item.name),
      inverse: true,
      axisLine: h.axisLine(),
      axisLabel: h.axisLabel({ fontSize: 11 }),
      splitLine: { show: false }
    },
    series: [
      {
        type: 'bar',
        barMaxWidth: 12,
        data: data.map((_, index) => {
          const value = values[index];
          return {
            value,
            itemStyle: {
              color: value >= 0 ? h.up() : h.down(),
              opacity: 0.82,
              borderRadius: value >= 0 ? [0, 3, 3, 0] : [3, 0, 0, 3]
            }
          };
        }),
        label: {
          show: true,
          position: 'right',
          fontSize: 10,
          color: h.cv('--text-2'),
          formatter: (params: { value: number }) =>
            `${params.value >= 0 ? '+' : ''}${params.value}${unit}`
        }
      }
    ]
  };
};
