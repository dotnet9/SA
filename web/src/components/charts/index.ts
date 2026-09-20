/**
 * 图表工厂出口。
 *
 * 命名与原型 `charts.js` 的 `F.*` 一一对应（详细设计 §9.2 的 20 个工厂），
 * 本批实现市场概览需要的 7 个，其余随各域批次补齐。
 */
export { spark } from './factories/spark';
export { donut, type DonutSlice, type DonutOptions } from './factories/donut';
export { combo, type ComboBarSeries, type ComboLineSeries, type ComboOptions } from './factories/combo';
export { treemap, type TreemapItem } from './factories/treemap';
export { hbar, type HBarItem, type HBarOptions } from './factories/hbar';
export { area, type AreaSeries, type AreaOptions } from './factories/area';
export { heatmap, type HeatmapCell, type HeatmapOptions } from './factories/heatmap';

export { useChart, type ChartFactory, type ChartOptions, type UseChartResult } from './useChart';
export { default as echarts } from './echarts';
export { cv, palette, upColor, downColor, flatColor, withAlpha, toneColor } from './theme';
