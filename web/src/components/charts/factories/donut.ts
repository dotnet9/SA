import type { ChartFactory } from '../useChart';

/** 环形图的一个扇区。 */
export interface DonutSlice {
  name: string;
  value: number;
  /** 显式颜色；不传则按色调自动取值。 */
  color?: string;
  /** 语义色调，用于自动取色（up / down / flat）。 */
  tone?: 'up' | 'down' | 'flat';
}

/** 环形图选项。 */
export interface DonutOptions {
  /** 圆心位置，如 `['34%', '50%']`。 */
  center?: [string, string];
  /** 内外半径，如 `['50%', '72%']`。 */
  radius?: [string, string];
  /** 图例方向。 */
  legendOrient?: 'horizontal' | 'vertical';
  /** 中心显示的总计文案。 */
  centerLabel?: string;
  /** 中心显示的数值文案。 */
  centerValue?: string;
}

/**
 * 环形图。移植自原型 `charts.js` 的 `F.donut`。
 *
 * 用于「涨跌家数」这类构成占比；中心可放总计，避免额外再画一个数字块。
 */
export const donut: ChartFactory<readonly DonutSlice[], DonutOptions> = (h, data, options) => {
  if (!data || data.length === 0) {
    return {};
  }

  const pal = h.palette();
  const center = options?.center ?? ['50%', '50%'];
  const radius = options?.radius ?? ['52%', '74%'];

  return {
    tooltip: h.tooltip({ trigger: 'item', formatter: '{b}：{c}（{d}%）' }),
    legend: h.legend({
      orient: options?.legendOrient ?? 'horizontal',
      left: options?.legendOrient === 'vertical' ? '58%' : 'center',
      top: options?.legendOrient === 'vertical' ? 'middle' : 'auto',
      bottom: options?.legendOrient === 'vertical' ? 'auto' : 0
    }),
    graphic:
      options?.centerValue === undefined
        ? undefined
        : [
            {
              type: 'text',
              left: center[0],
              top: center[1],
              style: {
                text: options.centerValue,
                fill: h.cv('--text-1'),
                fontSize: 20,
                fontWeight: 700,
                align: 'center',
                textVerticalAlign: 'middle'
              }
            },
            {
              type: 'text',
              left: center[0],
              top: `calc(${center[1]} + 18px)`,
              style: {
                text: options.centerLabel ?? '',
                fill: h.cv('--text-3'),
                fontSize: 11,
                align: 'center',
                textVerticalAlign: 'middle'
              }
            }
          ],
    series: [
      {
        type: 'pie',
        radius,
        center,
        avoidLabelOverlap: true,
        label: { show: false },
        labelLine: { show: false },
        itemStyle: { borderColor: h.cv('--bg-root'), borderWidth: 2 },
        data: data.map((slice, index) => ({
          name: slice.name,
          value: slice.value,
          itemStyle: {
            color: slice.color ?? (slice.tone ? h.tone(slice.tone) : pal[index % pal.length])
          }
        }))
      }
    ]
  };
};
