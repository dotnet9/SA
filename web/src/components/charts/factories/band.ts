import type { ChartFactory } from '../useChart';

/** 区间带图选项。 */
export interface BandOptions {
  labels: readonly string[];
  /** 主线（通常是价格或指标值）。 */
  line: readonly number[];
  /** 区间上沿。 */
  upper: readonly number[];
  /** 区间下沿。 */
  lower: readonly number[];
  unit?: string;
  lineName?: string;
  /** 区间名称（提示文案用），如「目标价区间」「布林带」。 */
  bandName?: string;
  lineColor?: string;
}

/**
 * 区间带图：主线 + 上下沿填充。移植自原型 `charts.js` 的 `F.band`。
 *
 * 用于「价格 vs 布林带」「现价 vs 目标价区间」这类需要同时看到中枢与波动范围的场景。
 * 上下沿用同一色系的半透明填充，中间留白，避免与主线混淆。
 */
export const band: ChartFactory<BandOptions> = (h, cfg) => {
  if (!cfg || !cfg.labels || cfg.labels.length === 0) {
    return {};
  }

  const unit = cfg.unit ?? '';
  const lineColor = h.resolveColor(cfg.lineColor, h.palette()[0]);

  return {
    tooltip: h.tooltip({
      trigger: 'axis',
      axisPointer: h.axisPointer(),
      formatter: (params: { dataIndex: number }[]) => {
        const index = params[0]?.dataIndex ?? 0;
        const line = cfg.line[index];
        const upper = cfg.upper[index];
        const lower = cfg.lower[index];
        const rows = [
          `<b>${cfg.labels[index]}</b>`,
          `${cfg.lineName ?? '数值'}：${line}${unit}`
        ];
        if (Number.isFinite(upper) && Number.isFinite(lower)) {
          rows.push(`${cfg.bandName ?? '区间'}：${lower} ~ ${upper}${unit}`);
        }
        return rows.join('<br>');
      }
    }),
    grid: h.grid({ top: 12, right: 52 }),
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
      scale: true,
      axisLine: { show: false },
      axisLabel: h.axisLabel({ fontSize: 10 }),
      splitLine: h.splitLine()
    },
    series: [
      // 下沿先画（不填充），再用上沿填充到它上面，形成一条带
      {
        name: `${cfg.bandName ?? '区间'}下沿`,
        type: 'line',
        data: [...cfg.lower],
        symbol: 'none',
        lineStyle: { width: 0.8, color: h.withAlpha(lineColor, 0.5), type: 'dashed' },
        stack: 'band',
        silent: true
      },
      {
        name: `${cfg.bandName ?? '区间'}上沿`,
        type: 'line',
        data: cfg.upper.map((value, index) => value - (cfg.lower[index] ?? 0)),
        symbol: 'none',
        lineStyle: { width: 0.8, color: h.withAlpha(lineColor, 0.5), type: 'dashed' },
        areaStyle: { color: h.withAlpha(lineColor, 0.12) },
        stack: 'band',
        silent: true
      },
      {
        name: cfg.lineName ?? '数值',
        type: 'line',
        data: [...cfg.line],
        smooth: true,
        symbol: 'none',
        lineStyle: { width: 1.8, color: lineColor },
        itemStyle: { color: lineColor }
      }
    ]
  };
};
