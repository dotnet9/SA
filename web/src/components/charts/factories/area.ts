import type { ChartFactory } from '../useChart';

/** 面积图的一条序列。 */
export interface AreaSeries {
  name: string;
  data: readonly number[];
  color?: string;
  /** 是否填充（默认填充）。 */
  fill?: boolean;
  /** 是否画成阶梯线（资金流累计等场景更贴切）。 */
  step?: boolean;
}

/** 面积图选项。 */
export interface AreaOptions {
  labels: readonly string[];
  /** 单序列形态：与原型 `charts.js` 的 `F.area(el, { labels, data, unit })` 调用一致。 */
  data?: readonly number[];
  /** 多序列形态：需要对比两条以上时使用。 */
  series?: readonly AreaSeries[];
  unit?: string;
  /** 纵轴是否从 0 起（资金净额等含负值时保持 false 以放大波动）。 */
  startAtZero?: boolean;
  /** 单序列形态下的序列名。 */
  name?: string;
}

/**
 * 面积图。移植自原型 `charts.js` 的 `F.area`，同时支持原型的单序列形态
 * （`{ labels, data, unit }`）与本项目扩展的多序列形态（`{ labels, series }`）。
 */
export const area: ChartFactory<AreaOptions> = (h, cfg) => {
  if (!cfg || !cfg.labels || cfg.labels.length === 0) {
    return {};
  }

  const series: AreaSeries[] = cfg.series && cfg.series.length > 0
    ? [...cfg.series]
    : cfg.data
      ? [{ name: cfg.name ?? '数值', data: cfg.data }]
      : [];

  if (series.length === 0) {
    return {};
  }

  const pal = h.palette();
  const unit = cfg.unit ?? '';

  return {
    legend: series.length > 1 ? h.legend({ data: series.map((s) => s.name) }) : undefined,
    tooltip: h.tooltip({
      trigger: 'axis',
      axisPointer: h.axisPointer(),
      valueFormatter: (value: unknown) => (typeof value === 'number' ? `${value}${unit}` : String(value))
    }),
    grid: h.grid({ top: series.length > 1 ? 30 : 12 }),
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: [...cfg.labels],
      axisLine: h.axisLine(),
      axisLabel: h.axisLabel({ fontSize: 10 }),
      splitLine: { show: false }
    },
    yAxis: {
      type: 'value',
      scale: !cfg.startAtZero,
      axisLine: { show: false },
      axisLabel: h.axisLabel({ fontSize: 10 }),
      splitLine: h.splitLine()
    },
    series: series.map((item, index) => {
      const color = h.resolveColor(item.color, pal[index % pal.length]);
      return {
        name: item.name,
        type: 'line',
        data: [...item.data],
        smooth: !item.step,
        step: item.step ? 'start' : undefined,
        symbol: 'none',
        lineStyle: { width: 1.6, color },
        itemStyle: { color },
        areaStyle:
          item.fill === false
            ? undefined
            : {
                color: {
                  type: 'linear',
                  x: 0,
                  y: 0,
                  x2: 0,
                  y2: 1,
                  colorStops: [
                    { offset: 0, color: h.withAlpha(color, 0.3) },
                    { offset: 1, color: h.withAlpha(color, 0) }
                  ]
                }
              }
      };
    })
  };
};
