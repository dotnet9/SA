import type { ChartFactory } from '../useChart';

/** 热力图数据点：`[xIndex, yIndex, value]`。 */
export type HeatmapCell = [number, number, number];

/** 热力图选项。 */
export interface HeatmapOptions {
  /** 横轴类目。 */
  xLabels: readonly string[];
  /** 纵轴类目。 */
  yLabels: readonly string[];
  cells: readonly HeatmapCell[];
  /** 值单位后缀（提示文案用）。 */
  unit?: string;
  /** 数值格式化（保留位数等）。 */
  decimals?: number;
}

/**
 * 热力图。移植自原型 `charts.js` 的 `F.heatmap`。
 *
 * 颜色用 VisualMap 从「跌色 → 中性 → 涨色」连续映射，因此正负对称、
 * 且随涨跌色切换整体翻转。用于风险矩阵与多标的×多指标对照。
 */
export const heatmap: ChartFactory<HeatmapOptions> = (h, cfg) => {
  if (!cfg || cfg.cells.length === 0) {
    return {};
  }

  const values = cfg.cells.map((cell) => cell[2]);
  const bound = Math.max(Math.abs(Math.min(...values)), Math.abs(Math.max(...values))) || 1;
  const decimals = cfg.decimals ?? 1;
  const unit = cfg.unit ?? '';

  return {
    tooltip: h.tooltip({
      position: 'top',
      formatter: (params: { data: HeatmapCell }) => {
        const [x, y, value] = params.data;
        return `<b>${cfg.yLabels[y]} · ${cfg.xLabels[x]}</b><br>${value.toFixed(decimals)}${unit}`;
      }
    }),
    grid: h.grid({ top: 8, bottom: 48, left: 8, right: 8 }),
    xAxis: {
      type: 'category',
      data: [...cfg.xLabels],
      splitArea: { show: true },
      axisLine: h.axisLine(),
      axisLabel: h.axisLabel({ fontSize: 10 })
    },
    yAxis: {
      type: 'category',
      data: [...cfg.yLabels],
      splitArea: { show: true },
      axisLine: h.axisLine(),
      axisLabel: h.axisLabel({ fontSize: 10 })
    },
    visualMap: {
      min: -bound,
      max: bound,
      calculable: false,
      orient: 'horizontal',
      left: 'center',
      bottom: 0,
      itemWidth: 10,
      itemHeight: 90,
      textStyle: { color: h.cv('--chart-label'), fontSize: 10 },
      inRange: { color: [h.down(), h.cv('--bg-elevated'), h.up()] }
    },
    series: [
      {
        type: 'heatmap',
        data: cfg.cells.map((cell) => [...cell]),
        label: { show: cfg.xLabels.length <= 12 && cfg.yLabels.length <= 10, fontSize: 10 },
        itemStyle: { borderColor: h.cv('--bg-root'), borderWidth: 1 },
        emphasis: { itemStyle: { shadowBlur: 8, shadowColor: 'rgba(0,0,0,.35)' } }
      }
    ]
  };
};
