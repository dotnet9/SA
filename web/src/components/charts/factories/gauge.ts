import type { ChartFactory } from '../useChart';

/** 仪表盘选项。 */
export interface GaugeOptions {
  /** 当前值。 */
  value: number;
  /** 量程上限，默认 100。 */
  max?: number;
  /** 指标名（显示在中心下方）。 */
  name?: string;
  /** 单位后缀。 */
  unit?: string;
  /** 达到该值起转为警示色。 */
  warnAt?: number;
  /** 达到该值起转为危险色。 */
  dangerAt?: number;
  /** 小数位，默认 0。 */
  decimals?: number;
}

/**
 * 仪表盘。移植自原型 `charts.js` 的 `F.gauge`。
 *
 * 用于「0–100 的评分」「1–5 的风险等级」这类单值强度展示。
 * 颜色靠阈值表达语义（正常 / 警示 / 危险），而不是靠数值大小本身。
 */
export const gauge: ChartFactory<GaugeOptions> = (h, cfg) => {
  if (!cfg || !Number.isFinite(cfg.value)) {
    return {};
  }

  const max = cfg.max ?? 100;
  const decimals = cfg.decimals ?? 0;
  const warnAt = cfg.warnAt ?? max * 0.6;
  const dangerAt = cfg.dangerAt ?? max * 0.85;

  // 阈值从高到低排列：ECharts 取第一个满足 value <= 阈值的区间
  const stops: { value: number; color: string }[] = [];
  if (dangerAt < max) {
    stops.push({ value: max, color: h.down() });
  }
  if (warnAt < dangerAt) {
    stops.push({ value: dangerAt, color: h.flat() });
  }
  stops.push({ value: warnAt, color: h.palette()[0] });

  return {
    series: [
      {
        type: 'gauge',
        min: 0,
        max,
        startAngle: 210,
        endAngle: -30,
        radius: '92%',
        center: ['50%', '58%'],
        progress: { show: false },
        axisLine: { lineStyle: { width: 10, color: stops.map((s) => [s.value / max, s.color] as [number, string]) } },
        pointer: { width: 4, length: '58%', itemStyle: { color: h.cv('--text-2') } },
        axisTick: { show: false },
        splitLine: { length: 8, lineStyle: { color: h.cv('--chart-split'), width: 1 } },
        axisLabel: { show: false },
        anchor: { show: false },
        title: {
          offsetCenter: [0, '32%'],
          fontSize: 11,
          color: h.cv('--text-3')
        },
        detail: {
          offsetCenter: [0, '4%'],
          fontSize: 22,
          fontWeight: 700,
          color: h.cv('--text-1'),
          formatter: (value: number) => `${value.toFixed(decimals)}${cfg.unit ?? ''}`
        },
        data: [{ value: cfg.value, name: cfg.name ?? '' }]
      }
    ]
  };
};
