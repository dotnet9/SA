import type { ChartFactory } from '../useChart';

/** 一根 K 线（与后端 `CandleDto` 一致）。 */
export interface Candle {
  d: string;
  o: number;
  h: number;
  l: number;
  c: number;
  v: number;
}

/** K 线图数据：K 线 + 服务端算好的指标。 */
export interface KlineData {
  bars: readonly Candle[];
  ma?: {
    ma5?: readonly (number | null)[];
    ma10?: readonly (number | null)[];
    ma20?: readonly (number | null)[];
    ma60?: readonly (number | null)[];
  };
  macd?: {
    dif?: readonly (number | null)[];
    dea?: readonly (number | null)[];
    macd?: readonly (number | null)[];
  };
  kdj?: {
    k?: readonly (number | null)[];
    d?: readonly (number | null)[];
    j?: readonly (number | null)[];
  };
  boll?: {
    up?: readonly (number | null)[];
    mid?: readonly (number | null)[];
    low?: readonly (number | null)[];
  };
}

/** K 线图选项。 */
export interface KlineOptions {
  /** 展示成交量副图（默认 true）。 */
  volume?: boolean;
  /** 展示 MACD 副图（默认 true）。 */
  macd?: boolean;
  /** 展示 KDJ 副图（默认 false，与原型一致：MACD 与 KDJ 二选一）。 */
  kdj?: boolean;
  /** 叠加布林带（默认 false）。 */
  boll?: boolean;
  /** 默认展示的最近根数（原型 120 根）。 */
  windowSize?: number;
}

/**
 * K 线与副图。移植自原型 `charts.js` 的 `F.kline`。
 *
 * <b>与原型的一处差异</b>：原型在浏览器里用 `charts.js` 自己算 MA/MACD/KDJ，
 * 本实现改为直接消费服务端算好的序列（实施计划 §5.5：指标由后端批量计算并落库），
 * 因此图上看到的值与列表、接口完全一致，不存在两套算法。
 *
 * 主图与副图共用 4 个 grid；数值序列里 <c>null</c> 表示样本不足，ECharts 会自然断开，
 * 与「不足周期不画线」的约定一致。
 */
export const kline: ChartFactory<KlineData, KlineOptions> = (h, data, options) => {
  if (!data || !data.bars || data.bars.length === 0) {
    return {};
  }

  const bars = data.bars;
  const dates = bars.map((bar) => bar.d);
  const showVolume = options?.volume !== false;
  const showMacd = options?.macd !== false;
  const showKdj = options?.kdj === true;
  const showBoll = options?.boll === true;
  const windowSize = options?.windowSize ?? 120;

  // 主图 / 成交量 / 指标副图三段的高度分配（百分比），与原型一致
  const topPct = 6;
  const hasSub = showMacd || showKdj;
  const mainH = hasSub ? (showVolume ? 44 : 62) : showVolume ? 62 : 84;
  const volH = showVolume ? (hasSub ? 12 : 22) : 0;
  const subH = hasSub ? 20 : 0;

  const grids: Record<string, unknown>[] = [];
  const xAxes: Record<string, unknown>[] = [];
  const yAxes: Record<string, unknown>[] = [];
  const series: Record<string, unknown>[] = [];
  const legends: string[] = [];

  let cursor = topPct;

  /* --- 主图：K 线 + 均线 --- */
  grids.push({ left: 8, right: 12, top: `${cursor}%`, height: `${mainH}%`, containLabel: true });
  xAxes.push({
    type: 'category',
    gridIndex: 0,
    data: dates,
    boundaryGap: true,
    axisLine: h.axisLine(),
    axisLabel: { show: false },
    splitLine: { show: false },
    axisPointer: { label: { show: false } }
  });
  yAxes.push({
    type: 'value',
    gridIndex: 0,
    scale: true,
    position: 'right',
    axisLine: { show: false },
    axisLabel: h.axisLabel({ fontSize: 10 }),
    splitLine: h.splitLine()
  });
  cursor += mainH + 4;

  series.push({
    name: 'K线',
    type: 'candlestick',
    xAxisIndex: 0,
    yAxisIndex: 0,
    data: bars.map((bar) => [bar.o, bar.c, bar.l, bar.h]),
    itemStyle: {
      color: h.up(),
      color0: h.down(),
      borderColor: h.up(),
      borderColor0: h.down()
    },
    barMaxWidth: 12
  });

  const pal = h.palette();
  const maDefs: [string, readonly (number | null)[] | undefined][] = [
    ['MA5', data.ma?.ma5],
    ['MA10', data.ma?.ma10],
    ['MA20', data.ma?.ma20],
    ['MA60', data.ma?.ma60]
  ];

  maDefs.forEach(([name, values], index) => {
    if (!values || values.length === 0) {
      return;
    }

    legends.push(name);
    series.push({
      name,
      type: 'line',
      xAxisIndex: 0,
      yAxisIndex: 0,
      data: [...values],
      smooth: true,
      symbol: 'none',
      lineStyle: { width: 1.1, color: pal[index % pal.length], opacity: 0.9 },
      itemStyle: { color: pal[index % pal.length] }
    });
  });

  /* --- 布林带：叠加在主图上 --- */
  if (showBoll && data.boll?.up && data.boll?.low) {
    legends.push('BOLL上轨', 'BOLL下轨');
    series.push(
      {
        name: 'BOLL上轨',
        type: 'line',
        xAxisIndex: 0,
        yAxisIndex: 0,
        data: [...data.boll.up],
        symbol: 'none',
        lineStyle: { width: 1, color: pal[5], type: 'dashed' },
        itemStyle: { color: pal[5] }
      },
      {
        name: 'BOLL下轨',
        type: 'line',
        xAxisIndex: 0,
        yAxisIndex: 0,
        data: [...(data.boll.low ?? [])],
        symbol: 'none',
        lineStyle: { width: 1, color: pal[5], type: 'dashed' },
        itemStyle: { color: pal[5] }
      }
    );
  }

  /* --- 成交量 --- */
  if (showVolume) {
    grids.push({ left: 8, right: 12, top: `${cursor}%`, height: `${volH}%`, containLabel: true });
    xAxes.push({
      type: 'category',
      gridIndex: 1,
      data: dates,
      axisLine: h.axisLine(),
      axisLabel: { show: false },
      splitLine: { show: false }
    });
    yAxes.push({
      type: 'value',
      gridIndex: 1,
      position: 'right',
      axisLine: { show: false },
      axisLabel: h.axisLabel({ fontSize: 10 }),
      splitLine: h.splitLine()
    });
    series.push({
      name: '成交量',
      type: 'bar',
      xAxisIndex: 1,
      yAxisIndex: 1,
      data: bars.map((bar) => ({
        value: bar.v,
        itemStyle: { color: bar.c >= bar.o ? h.up() : h.down(), opacity: 0.62 }
      })),
      barMaxWidth: 12
    });
    cursor += volH + 4;
  }

  /* --- MACD / KDJ --- */
  if (showMacd && data.macd) {
    grids.push({ left: 8, right: 12, top: `${cursor}%`, height: `${subH}%`, containLabel: true });
    xAxes.push({
      type: 'category',
      gridIndex: 2,
      data: dates,
      axisLine: h.axisLine(),
      axisLabel: h.axisLabel({ fontSize: 10 }),
      splitLine: { show: false }
    });
    yAxes.push({
      type: 'value',
      gridIndex: 2,
      position: 'right',
      axisLine: { show: false },
      axisLabel: h.axisLabel({ fontSize: 10 }),
      splitLine: h.splitLine()
    });

    legends.push('DIF', 'DEA', 'MACD');
    series.push(
      {
        name: 'MACD',
        type: 'bar',
        xAxisIndex: 2,
        yAxisIndex: 2,
        data: (data.macd.macd ?? []).map((value) => ({
          value,
          itemStyle: { color: (value ?? 0) >= 0 ? h.up() : h.down(), opacity: 0.7 }
        })),
        barMaxWidth: 8
      },
      {
        name: 'DIF',
        type: 'line',
        xAxisIndex: 2,
        yAxisIndex: 2,
        data: [...(data.macd.dif ?? [])],
        symbol: 'none',
        lineStyle: { width: 1.1, color: pal[0] },
        itemStyle: { color: pal[0] }
      },
      {
        name: 'DEA',
        type: 'line',
        xAxisIndex: 2,
        yAxisIndex: 2,
        data: [...(data.macd.dea ?? [])],
        symbol: 'none',
        lineStyle: { width: 1.1, color: pal[1] },
        itemStyle: { color: pal[1] }
      }
    );
  } else if (showKdj && data.kdj) {
    grids.push({ left: 8, right: 12, top: `${cursor}%`, height: `${subH}%`, containLabel: true });
    xAxes.push({
      type: 'category',
      gridIndex: 2,
      data: dates,
      axisLine: h.axisLine(),
      axisLabel: h.axisLabel({ fontSize: 10 }),
      splitLine: { show: false }
    });
    yAxes.push({
      type: 'value',
      gridIndex: 2,
      position: 'right',
      min: 0,
      max: 100,
      axisLine: { show: false },
      axisLabel: h.axisLabel({ fontSize: 10 }),
      splitLine: h.splitLine()
    });

    legends.push('K', 'D', 'J');
    (['k', 'd', 'j'] as const).forEach((key, index) => {
      series.push({
        name: key.toUpperCase(),
        type: 'line',
        xAxisIndex: 2,
        yAxisIndex: 2,
        data: [...(data.kdj?.[key] ?? [])],
        symbol: 'none',
        lineStyle: { width: 1.1, color: pal[index % pal.length] },
        itemStyle: { color: pal[index % pal.length] }
      });
    });
  }

  return {
    animation: false,
    // 图例由页面自绘（原型把 MA 图例写成 HTML），避免与大图例挤在一起
    legend: { show: false },
    tooltip: h.tooltip({ trigger: 'axis', axisPointer: h.axisPointer() }),
    axisPointer: { link: [{ xAxisIndex: 'all' }] },
    grid: grids,
    xAxis: xAxes,
    yAxis: yAxes,
    series,
    dataZoom: [
      {
        type: 'inside',
        xAxisIndex: grids.map((_, index) => index),
        startValue: Math.max(0, bars.length - windowSize),
        endValue: bars.length - 1,
        // 滚轮缩放只在按住 Shift 时生效：否则图表会吞掉页面滚动，
        // 用户在 K 线区域内无法上下滚动页面（拖动平移不受影响）。
        zoomOnMouseWheel: 'shift',
        moveOnMouseWheel: 'shift',
        moveOnMouseMove: true
      }
    ]
  };
};
