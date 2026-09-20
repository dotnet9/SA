/**
 * 图表主题令牌与通用片段。
 *
 * 全部颜色都在<b>取用时刻</b>从 CSS 变量解析（`getComputedStyle`），因此
 * 主题与涨跌色切换后重建实例即可整体跟随，不需要在图表里硬编码任何颜色
 * （实施计划 §5.9：不允许在图表里硬编码颜色）。
 */

/** 读取一个 CSS 变量（在 `<html>` 上，与 tokens.css 一致）。 */
export function cv(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim() || '#888';
}

/** 序列调色板。 */
export function palette(): string[] {
  return [
    cv('--chart-1'),
    cv('--chart-2'),
    cv('--chart-3'),
    cv('--chart-4'),
    cv('--chart-5'),
    cv('--chart-6'),
    cv('--chart-7'),
    cv('--chart-8')
  ];
}

/** 涨色（随 `data-updown` 互换）。 */
export const upColor = (): string => cv('--up');

/** 跌色。 */
export const downColor = (): string => cv('--down');

/** 平盘色。 */
export const flatColor = (): string => cv('--flat');

/** 给颜色加透明度，支持 `#rgb` / `#rrggbb` / `rgb()` / `rgba()`。 */
export function withAlpha(color: string, alpha: number): string {
  const value = (color || '').trim();

  const short = /^#([0-9a-f]{3})$/i.exec(value);
  if (short) {
    const s = short[1];
    return `rgba(${parseInt(s[0] + s[0], 16)},${parseInt(s[1] + s[1], 16)},${parseInt(s[2] + s[2], 16)},${alpha})`;
  }

  const long = /^#([0-9a-f]{6})$/i.exec(value);
  if (long) {
    const v = long[1];
    return `rgba(${parseInt(v.slice(0, 2), 16)},${parseInt(v.slice(2, 4), 16)},${parseInt(v.slice(4, 6), 16)},${alpha})`;
  }

  const rgb = /^rgba?\(([^)]+)\)$/i.exec(value);
  if (rgb) {
    const parts = rgb[1]
      .split(',')
      .map((x) => x.trim())
      .slice(0, 3);
    return `rgba(${parts.join(',')},${alpha})`;
  }

  return value;
}

/** 涨跌方向对应的颜色。 */
export function toneColor(tone: 'up' | 'down' | 'flat' | undefined): string {
  if (tone === 'up') return upColor();
  if (tone === 'down') return downColor();
  return flatColor();
}

/* ------------------------------------------------------------------
   通用片段
   ------------------------------------------------------------------ */

/** 悬浮提示样式。 */
export function tooltip(extra: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    backgroundColor: cv('--chart-tooltip-bg'),
    borderColor: cv('--border'),
    borderWidth: 1,
    padding: [8, 12],
    textStyle: { color: cv('--text-1'), fontSize: 12, fontFamily: 'inherit' },
    extraCssText: 'backdrop-filter:blur(12px);border-radius:10px;box-shadow:0 12px 40px rgba(0,0,0,.45);',
    ...extra
  };
}

/** 坐标轴底线。 */
export function axisLine(): Record<string, unknown> {
  return { lineStyle: { color: cv('--chart-axis') } };
}

/** 坐标轴文字。 */
export function axisLabel(extra: Record<string, unknown> = {}): Record<string, unknown> {
  return { color: cv('--chart-label'), fontSize: 11, ...extra };
}

/** 网格分隔线。 */
export function splitLine(show = true): Record<string, unknown> {
  return { show, lineStyle: { color: cv('--chart-split'), type: 'solid' } };
}

/** 绘图区边距。 */
export function grid(extra: Record<string, unknown> = {}): Record<string, unknown> {
  return { left: 8, right: 12, top: 28, bottom: 6, containLabel: true, ...extra };
}

/** 图例。 */
export function legend(extra: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    top: 0,
    right: 4,
    itemWidth: 9,
    itemHeight: 9,
    itemGap: 12,
    textStyle: { color: cv('--chart-label'), fontSize: 11 },
    icon: 'roundRect',
    ...extra
  };
}

/** 标题（图表内标题，仅少数图使用）。 */
export function titleText(text: string, sub = ''): Record<string, unknown> {
  return {
    text,
    subtext: sub,
    left: 0,
    top: 0,
    textStyle: { color: cv('--text-1'), fontSize: 13, fontWeight: 600 },
    subtextStyle: { color: cv('--text-3'), fontSize: 11 }
  };
}

/** 十字准星。 */
export function axisPointer(): Record<string, unknown> {
  return {
    type: 'cross',
    crossStyle: { color: cv('--text-3'), type: 'dashed' },
    lineStyle: { color: cv('--text-3'), type: 'dashed' },
    label: {
      backgroundColor: cv('--bg-elevated'),
      borderColor: cv('--border'),
      borderWidth: 1,
      color: cv('--text-1'),
      fontSize: 11
    }
  };
}

/** 传给图表工厂的令牌与片段集合。 */
export interface ChartHelpers {
  cv: typeof cv;
  palette: typeof palette;
  up: typeof upColor;
  down: typeof downColor;
  flat: typeof flatColor;
  tone: typeof toneColor;
  withAlpha: typeof withAlpha;
  tooltip: typeof tooltip;
  axisLine: typeof axisLine;
  axisLabel: typeof axisLabel;
  splitLine: typeof splitLine;
  grid: typeof grid;
  legend: typeof legend;
  titleText: typeof titleText;
  axisPointer: typeof axisPointer;
}

/** 当前令牌快照。工厂在重建时重新取一次，从而跟随主题切换。 */
export function helpers(): ChartHelpers {
  return {
    cv,
    palette,
    up: upColor,
    down: downColor,
    flat: flatColor,
    tone: toneColor,
    withAlpha,
    tooltip,
    axisLine,
    axisLabel,
    splitLine,
    grid,
    legend,
    titleText,
    axisPointer
  };
}
