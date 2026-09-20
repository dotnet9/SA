import type { ChartFactory } from '../useChart';

/** 一根柱序列。 */
export interface ComboBarSeries {
  name: string;
  data: readonly number[];
  /** 固定颜色；不传则用「涨跌 + 数值符号」着色（正红负绿，随 data-updown 互换）。 */
  color?: string;
  /** 统一用一种颜色（如截面资金流）。 */
  colorBySign?: boolean;
  /** 堆叠组名。 */
  stack?: string;
}

/** 一条折线序列。 */
export interface ComboLineSeries {
  name: string;
  data: readonly number[];
  color?: string;
  area?: boolean;
  /** 折线默认落在左轴；为 true 时使用右轴。 */
  onRightAxis?: boolean;
  smooth?: boolean;
}

/** 组合图选项。 */
export interface ComboOptions {
  labels: readonly string[];
  bars?: readonly ComboBarSeries[];
  lines?: readonly ComboLineSeries[];
  unit?: string;
  /** 右轴名称（存在右轴折线时展示）。 */
  rightAxisName?: string;
  /** 左轴名称。 */
  leftAxisName?: string;
  /** 是否堆叠所有柱（默认按各自 stack 分组）。 */
  stackBars?: boolean;
}

/**
 * 柱 + 折线组合图。移植自原型 `charts.js` 的 `F.combo`。
 *
 * 用于「北向资金（柱 + 累计折线）」「营收与增速」这类量价/量率同图场景。
 */
export const combo: ChartFactory<ComboOptions> = (h, cfg) => {
  if (!cfg || !cfg.labels || cfg.labels.length === 0) {
    return {};
  }

  const pal = h.palette();
  const bars = cfg.bars ?? [];
  const lines = cfg.lines ?? [];
  const hasRightAxis = lines.some((line) => line.onRightAxis);
  const unit = cfg.unit ?? '';

  const series: Record<string, unknown>[] = bars.map((bar, index) => ({
    name: bar.name,
    type: 'bar',
    stack: cfg.stackBars ? 'total' : bar.stack,
    barMaxWidth: 18,
    data: bar.color
      ? [...bar.data]
      : bar.data.map((value) => ({ value, itemStyle: { color: value >= 0 ? h.up() : h.down(), opacity: 0.85 } })),
    itemStyle: bar.color ? { color: h.resolveColor(bar.color), opacity: 0.85 } : undefined,
    ...(index === 0 && !bar.color ? {} : {})
  }));

  series.push(
    ...lines.map((line, index) => {
      const color = h.resolveColor(line.color, pal[index % pal.length]);
      return {
        name: line.name,
        type: 'line',
        yAxisIndex: line.onRightAxis ? 1 : 0,
        data: [...line.data],
        smooth: line.smooth ?? true,
        symbol: 'none',
        lineStyle: { width: 1.6, color },
        itemStyle: { color },
        areaStyle: line.area
          ? {
              color: {
                type: 'linear',
                x: 0,
                y: 0,
                x2: 0,
                y2: 1,
                colorStops: [
                  { offset: 0, color: h.withAlpha(color, 0.28) },
                  { offset: 1, color: h.withAlpha(color, 0) }
                ]
              }
            }
          : undefined
      };
    })
  );

  return {
    legend: h.legend({ data: [...bars.map((b) => b.name), ...lines.map((l) => l.name)] }),
    tooltip: h.tooltip({
      trigger: 'axis',
      axisPointer: { type: 'shadow' },
      valueFormatter: (value: unknown) => (typeof value === 'number' ? `${value}${unit}` : String(value))
    }),
    grid: h.grid({ top: 30, right: hasRightAxis ? 52 : 12 }),
    xAxis: {
      type: 'category',
      data: [...cfg.labels],
      axisLine: h.axisLine(),
      axisLabel: h.axisLabel({ fontSize: 10 }),
      splitLine: { show: false }
    },
    yAxis: hasRightAxis
      ? [
          {
            type: 'value',
            name: cfg.leftAxisName ?? '',
            nameTextStyle: { color: h.cv('--text-3'), fontSize: 10 },
            axisLine: { show: false },
            axisLabel: h.axisLabel({ fontSize: 10 }),
            splitLine: h.splitLine()
          },
          {
            type: 'value',
            name: cfg.rightAxisName ?? '',
            nameTextStyle: { color: h.cv('--text-3'), fontSize: 10 },
            position: 'right',
            scale: true,
            axisLine: { show: false },
            axisLabel: h.axisLabel({ fontSize: 10 }),
            splitLine: { show: false }
          }
        ]
      : {
          type: 'value',
          axisLine: { show: false },
          axisLabel: h.axisLabel({ fontSize: 10 }),
          splitLine: h.splitLine()
        },
    series
  };
};
