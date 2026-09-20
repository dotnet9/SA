/**
 * ECharts 按需引入。
 *
 * 详细设计 §12 要求「ECharts 按需引入图表类型与组件」：只注册本项目实际用到的图表与组件，
 * 避免把整个 echarts 打进首屏。新增图表类型时必须在这里登记，否则运行时不会有任何渲染（也不报错）。
 */
import * as echarts from 'echarts/core';
import {
  BarChart,
  CandlestickChart,
  GraphChart,
  HeatmapChart,
  LineChart,
  PieChart,
  TreemapChart
} from 'echarts/charts';
import {
  AxisPointerComponent,
  DataZoomComponent,
  GridComponent,
  LegendComponent,
  MarkLineComponent,
  TitleComponent,
  TooltipComponent,
  VisualMapComponent
} from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';

echarts.use([
  LineChart,
  BarChart,
  PieChart,
  TreemapChart,
  HeatmapChart,
  CandlestickChart,
  GraphChart,
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  VisualMapComponent,
  MarkLineComponent,
  DataZoomComponent,
  AxisPointerComponent,
  CanvasRenderer
]);

export default echarts;

/** ECharts 配置对象类型（按需引入后仍可用组合类型）。 */
export type EChartsOption = Parameters<echarts.ECharts['setOption']>[0];
