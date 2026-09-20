import { useCallback, useEffect, useRef, useState } from 'react';
import echarts, { type EChartsOption } from './echarts';
import { helpers, type ChartHelpers } from './theme';

/**
 * 图表工厂：给出令牌与数据，返回一份 ECharts 配置。
 *
 * 与原型 `charts.js` 的工厂一一对应（实施计划 §5.9）：
 * 逻辑全部移植自原型，保留「从 CSS 变量取色」的做法，因此主题与涨跌色切换即时生效。
 *
 * @typeParam TData 数据形态（多数工厂把整份配置都放在这里）。
 * @typeParam TOptions 少量绘制选项（如强制涨跌方向）。
 */
export type ChartFactory<TData, TOptions = ChartOptions> = (
  h: ChartHelpers,
  data: TData,
  options?: TOptions
) => EChartsOption;

/** 工厂的通用选项，各工厂按需声明自己的选项类型。 */
export interface ChartOptions {
  /** 覆盖涨跌方向（默认按数据首尾比较推断，与原型一致）。 */
  up?: boolean;
}

/** `useChart` 的返回值。 */
export interface UseChartResult {
  /** 绑定到容器 `<div>` 的 ref。 */
  ref: (node: HTMLDivElement | null) => void;
  /** 当前实例；未初始化完成时为 null。 */
  instance: () => echarts.ECharts | null;
}

/**
 * 在容器上绘制图表，并负责生命周期。
 *
 * 与原型 `make()` / `rebuildAll()` 等价：
 * - 同一容器重复绘制前先 `dispose`，避免实例泄漏与控制台告警；
 * - 主题/涨跌色变更（`sa:themechange`）后重建实例，让新配色生效；
 * - 容器尺寸变化用 `ResizeObserver` 触发 `resize()`；
 * - 卸载时释放实例。
 */
export function useChart<TData, TOptions = ChartOptions>(
  factory: ChartFactory<TData, TOptions>,
  data: TData,
  options?: TOptions
): UseChartResult {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const instanceRef = useRef<echarts.ECharts | null>(null);

  // 工厂与数据放进 ref：重建时读最新值，避免把它们写进 effect 依赖导致频繁重建
  const factoryRef = useRef(factory);
  const dataRef = useRef(data);
  const optionsRef = useRef(options);
  factoryRef.current = factory;
  dataRef.current = data;
  optionsRef.current = options;

  // 调用方通常内联传入 options（对象字面量每次渲染都是新引用），
  // 直接把它写进依赖会让每次渲染都重建实例；这里用序列化值做等价比较。
  const optionsKey = options === undefined ? '' : JSON.stringify(options);

  const [ready, setReady] = useState(false);

  const draw = useCallback(() => {
    const node = containerRef.current;
    if (!node) {
      return;
    }

    // 重复绘制前必须释放旧实例（原型 release() 的等价物）
    const existing = echarts.getInstanceByDom(node);
    if (existing) {
      existing.dispose();
      instanceRef.current = null;
    }

    const instance = echarts.init(node, null, { renderer: 'canvas' });
    const option = factoryRef.current(helpers(), dataRef.current, optionsRef.current);
    instance.setOption(option);
    instanceRef.current = instance;
  }, []);

  const attach = useCallback(
    (node: HTMLDivElement | null) => {
      containerRef.current = node;
      if (node) {
        draw();
        setReady(true);
      } else {
        instanceRef.current?.dispose();
        instanceRef.current = null;
        setReady(false);
      }
    },
    [draw]
  );

  // 数据或绘制选项变化后重绘（按值比较选项，见上面的 optionsKey）
  useEffect(() => {
    if (ready) {
      draw();
    }
  }, [data, optionsKey, ready, draw]);

  // 主题与涨跌色变化后重建（原型 rebuildAll 的等价物）
  useEffect(() => {
    const onThemeChange = () => {
      // 等一帧：CSS 变量刚写入，需要浏览器完成样式计算后再读色
      requestAnimationFrame(() => draw());
    };

    document.addEventListener('sa:themechange', onThemeChange);
    return () => document.removeEventListener('sa:themechange', onThemeChange);
  }, [draw]);

  // 容器尺寸变化
  useEffect(() => {
    const node = containerRef.current;
    if (!node || typeof ResizeObserver === 'undefined') {
      return;
    }

    const observer = new ResizeObserver(() => {
      instanceRef.current?.resize();
    });
    observer.observe(node);
    return () => observer.disconnect();
  }, [ready]);

  // 卸载释放
  useEffect(
    () => () => {
      instanceRef.current?.dispose();
      instanceRef.current = null;
    },
    []
  );

  const instance = useCallback(() => instanceRef.current, []);

  return { ref: attach, instance };
}
