/**
 * 图表工厂出口。
 *
 * 命名与原型 `charts.js` 的 `F.*` 一一对应（详细设计 §9.2 的 20 个工厂）。
 * 已完成：市场概览 7 个 + 趋势模块 4 个；其余（graph / sankey / radar / bubble /
 * chips / lanes / stacked / treemap 变体 / targetBand / ratingHistory）随各域批次补齐。
 */
export { spark } from './factories/spark';
export { donut, type DonutSlice, type DonutOptions } from './factories/donut';
export { combo, type ComboBarSeries, type ComboLineSeries, type ComboOptions } from './factories/combo';
export { treemap, type TreemapItem } from './factories/treemap';
export { hbar, type HBarItem, type HBarOptions } from './factories/hbar';
export { area, type AreaSeries, type AreaOptions } from './factories/area';
export { heatmap, type HeatmapCell, type HeatmapOptions } from './factories/heatmap';
export { kline, type Candle, type KlineData, type KlineOptions } from './factories/kline';
export { posneg, type PosNegOptions } from './factories/posneg';
export { band, type BandOptions } from './factories/band';
export { gauge, type GaugeOptions } from './factories/gauge';

export { useChart, type ChartFactory, type ChartOptions, type UseChartResult } from './useChart';
export { default as echarts } from './echarts';
export { cv, palette, upColor, downColor, flatColor, withAlpha, toneColor } from './theme';
