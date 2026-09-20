import type { ChartFactory } from '../useChart';

/** 正负柱状图选项。 */
export interface PosNegOptions {
  labels: readonly string[];
  data: readonly number[];
  unit?: string;
  /** 序列名（提示文案用）。 */
  name?: string;
  /** 是否让正负柱使用统一色（false 时按符号取涨跌色，默认 true）。 */
  colorBySign?: boolean;
}

/**
 * 正负柱状图。移植自原型 `charts.js` 的 `F.posneg`。
 *
 * 用于主力净流入、涨跌额这类「正负方向本身就是信息」的序列：
 * 正值用涨色、负值用跌色，零轴用中性色标出。
 */
export const posneg: ChartFactory<PosNegOptions> = (h, cfg) => {
  if (!cfg || !cfg.labels || cfg.labels.length === 0) {
    return {};
  }

  const unit = cfg.unit ?? '';
  const colorBySign = cfg.colorBySign !== false;

  return {
    tooltip: h.tooltip({
      trigger: 'axis',
      axisPointer: { type: 'shadow' },
      formatter: (params: { dataIndex: number }[]) => {
        const index = params[0]?.dataIndex ?? 0;
        const value = cfg.data[index];
        const sign = value >= 0 ? '+' : '';
        return `<b>${cfg.labels[index]}</b><br>${cfg.name ?? '数值'}：${sign}${value}${unit}`;
      }
    }),
    grid: h.grid({ top: 12, bottom: 24 }),
    xAxis: {
      type: 'category',
      data: [...cfg.labels],
      axisLine: h.axisLine(),
      axisLabel: h.axisLabel({ fontSize: 10 }),
      splitLine: { show: false }
    },
    yAxis: {
      type: 'value',
      axisLine: { show: false },
      axisLabel: h.axisLabel({ fontSize: 10 }),
      splitLine: h.splitLine()
    },
    series: [
      {
        name: cfg.name ?? '数值',
        type: 'bar',
        barMaxWidth: 18,
        data: cfg.data.map((value) => ({
          value,
          itemStyle: {
            color: colorBySign ? (value >= 0 ? h.up() : h.down()) : h.palette()[0],
            opacity: 0.85,
            borderRadius: value >= 0 ? [3, 3, 0, 0] : [0, 0, 3, 3]
          }
        })),
        // 零轴：不画的话很难判断哪一侧是净流入
        markLine: {
          silent: true,
          symbol: 'none',
          lineStyle: { color: h.cv('--chart-axis'), type: 'solid' },
          label: { show: false },
          data: [{ yAxis: 0 }]
        }
      }
    ]
  };
};
