import type { ChartFactory } from '../useChart';

/**
 * 迷你走势 sparkline。移植自原型 `charts.js` 的 `F.spark`。
 *
 * 数据为纯数值序列（收盘价）；颜色按首尾比较自动判定涨跌，可被 `options.up` 覆盖。
 * 无数据时返回空配置，由外层展示空态而不是画一条假线。
 */
export const spark: ChartFactory<readonly number[], { up?: boolean }> = (h, data, options) => {
  if (!data || data.length < 2) {
    return {};
  }

  const up = options?.up ?? data[data.length - 1] >= data[0];
  const color = up ? h.up() : h.down();

  return {
    animation: false,
    grid: { left: 0, right: 0, top: 2, bottom: 2 },
    xAxis: {
      type: 'category',
      show: false,
      boundaryGap: false,
      data: data.map((_, index) => index)
    },
    yAxis: { type: 'value', show: false, scale: true },
    series: [
      {
        type: 'line',
        data: [...data],
        smooth: true,
        symbol: 'none',
        lineStyle: { width: 1.4, color },
        areaStyle: {
          color: {
            type: 'linear',
            x: 0,
            y: 0,
            x2: 0,
            y2: 1,
            colorStops: [
              { offset: 0, color: h.withAlpha(color, 0.32) },
              { offset: 1, color: h.withAlpha(color, 0) }
            ]
          }
        }
      }
    ],
    tooltip: { show: false }
  };
};
