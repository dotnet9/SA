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
  series: readonly AreaSeries[];
  unit?: string;
  /** 纵轴是否从 0 起（资金净额等含负值时保持 false 以放大波动）。 */
  startAtZero?: boolean;
}

/**
 * 面积图（可多序列）。移植自原型 `charts.js` 的 `F.area`。
 *
 * 用于资金流累计、成交量趋势这类「看形状与拐点」的序列。
 */
export const area: ChartFactory<AreaOptions> = (h, cfg) => {
  if (!cfg || !cfg.labels || cfg.labels.length === 0 || !cfg.series || cfg.series.length === 0) {
    return {};
  }

  const pal = h.palette();
  const unit = cfg.unit ?? '';

  return {
    legend: h.legend({ data: cfg.series.map((s) => s.name) }),
    tooltip: h.tooltip({
      trigger: 'axis',
      axisPointer: h.axisPointer(),
      valueFormatter: (value: unknown) => (typeof value === 'number' ? `${value}${unit}` : String(value))
    }),
    grid: h.grid({ top: 30 }),
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
    series: cfg.series.map((series, index) => {
      const color = series.color ?? pal[index % pal.length];
      return {
        name: series.name,
        type: 'line',
        data: [...series.data],
        smooth: !series.step,
        step: series.step ? 'start' : undefined,
        symbol: 'none',
        lineStyle: { width: 1.6, color },
        itemStyle: { color },
        areaStyle:
          series.fill === false
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
