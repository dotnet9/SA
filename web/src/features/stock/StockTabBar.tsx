import { useEffect } from 'react';
import { Link, useParams } from 'react-router';
import { StockTabKeys, StockTabModules, StockTabNames, tabOfModule } from '@/app/nav';
import { rememberStockTab } from '@/app/useCurrentStock';

/**
 * 个股页的 5 个 Tab 条（实施计划 §6.2）。
 *
 * Tab 只是**路由的可视化分组**，不是新的状态：每个 Tab 落到它第一个模块的路径上，
 * 因此分享链接、浏览器前进后退、以及「自选股里切标的保持当前 Tab」都不需要额外机制。
 *
 * 未知模块不报错：`tabOfModule` 会回退到第一个 Tab（旧链接不会 404）。
 */
export function StockTabBar({ code, module }: { code: string; module: string }) {
  const active = tabOfModule(module);

  // 记住当前 Tab：自选股等列表里切换标的时用它保持维度不跳回第一个
  useEffect(() => {
    rememberStockTab(module);
  }, [module]);

  return (
    <div className="row gap-1 wrap" style={{ marginBottom: 12, borderBottom: '1px solid var(--border-soft)' }}>
      {StockTabKeys.map((key) => {
        // 点 Tab 落到该 Tab 的默认模块；当前模块属于该 Tab 时保持当前模块不变，
        // 避免「在趋势页点『价值研究』Tab」这类操作把用户弹回默认页
        const target = StockTabModules[key].includes(module) ? module : StockTabModules[key][0];
        const isActive = key === active;

        return (
          <Link
            key={key}
            to={`/stock/${code}/${target}`}
            className="fs-13"
            style={{
              padding: '6px 12px',
              textDecoration: 'none',
              color: isActive ? 'var(--brand)' : 'var(--text-2)',
              borderBottom: isActive ? '2px solid var(--brand)' : '2px solid transparent',
              fontWeight: isActive ? 600 : 400
            }}
          >
            {StockTabNames[key]}
          </Link>
        );
      })}
    </div>
  );
}

/**
 * 个股页的 Tab 包装：Tab 条 + 具体模块内容。
 *
 * 单独抽出来是为了让桌面与移动端共用同一份 Tab 条（移动端不重写布局，
 * 与「移动端不做拓扑页」的取舍一致——这里 Tab 条本身在窄屏也放得下）。
 */
export function StockTabbed({ module, children }: { module: string; children: React.ReactNode }) {
  const { code = '' } = useParams();
  return (
    <>
      <StockTabBar code={code} module={module} />
      {children}
    </>
  );
}
