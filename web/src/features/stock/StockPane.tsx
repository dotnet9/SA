import { lazy, Suspense, type LazyExoticComponent } from 'react';
import { Link, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { StockTabKeys, StockTabNames, tabOfModule } from '@/app/nav';
import { useCurrentStock } from '@/app/useCurrentStock';
import { ErrorState } from '@/components/ui/States';
import { fetchStockOverview } from '@/features/stock/api';

/**
 * 个股区：9 个 Tab 同页切换。
 *
 * 三个入口共用这一份实现（大盘概况、自选股、个股独立页），不存在两套：
 *   `/market/:code/:module?`  `/watchlist/:code/:module?`  `/stock/:code/:module?`
 *
 * **Tab 是路由的一部分**（`:module` 段），因此：
 *   - 浏览器前进 / 后退可用；
 *   - 分享链接带 Tab；
 *   - 从大盘或自选切到另一只股票时，Tab 保持不变（链接由列表页拼出）。
 *
 * 个股条只放 4 项：身份、现价 / 涨跌、成交 / 换手 / PE。原来堆 10 个指标
 * （今开 / 最高 / 最低 / 昨收 / 量比 / 市值 …）会让页面「看不出重点」。
 */

const Modules: Record<string, LazyExoticComponent<() => React.JSX.Element>> = {
  overview: lazy(() => import('@/features/stock/StockOverviewPage').then((m) => ({ default: m.StockOverviewPage }))),
  trend: lazy(() => import('@/features/stock/StockTrendPage').then((m) => ({ default: m.StockTrendPage }))),
  finance: lazy(() => import('@/features/finance/StockFinancePage').then((m) => ({ default: m.StockFinancePage }))),
  equity: lazy(() => import('@/features/equity/StockEquityPage').then((m) => ({ default: m.StockEquityPage }))),
  capital: lazy(() => import('@/features/capital/StockCapitalPage').then((m) => ({ default: m.StockCapitalPage }))),
  industry: lazy(() => import('@/features/industry/StockIndustryPage').then((m) => ({ default: m.StockIndustryPage }))),
  events: lazy(() => import('@/features/events/StockEventsPage').then((m) => ({ default: m.StockEventsPage }))),
  risk: lazy(() => import('@/features/risk/StockRiskPage').then((m) => ({ default: m.StockRiskPage }))),
  rating: lazy(() => import('@/features/rating/StockRatingPage').then((m) => ({ default: m.StockRatingPage })))
};

/** 数字格式化：缺失显示「—」，不显示 0（0 会被误读为真实值）。 */
function fmt(value: number | null | undefined, digits = 2): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '—';
  }

  return value.toLocaleString('zh-CN', { minimumFractionDigits: digits, maximumFractionDigits: digits });
}

function tone(value: number | null | undefined): string {
  if (value === null || value === undefined || value === 0) return 'is-flat';
  return value > 0 ? 'is-up' : 'is-down';
}

/**
 * 个股区。
 *
 * @param listPath 返回列表的路径（独立页传 null，此时不显示「返回列表」）。
 */
export function StockPane({ listPath }: { listPath: string | null }) {
  const { code: urlCode, module } = useParams();
  const remembered = useCurrentStock();
  const code = urlCode ?? remembered;
  const tab = tabOfModule(module);

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['stock', code, 'overview'],
    queryFn: () => fetchStockOverview(code),
    enabled: code.length > 0
  });

  const profile = data?.profile;

  return (
    <>
      <div className="stock-pane-head">
        {listPath ? (
          <Link className="stock-back" to={listPath}>
            ← 返回列表
          </Link>
        ) : null}

        <div className="row gap-2 wrap" style={{ minWidth: 0 }}>
          <span className="fs-16 fw-700">{profile?.name ?? code}</span>
          <span className="mono fs-11 t-3">{code}</span>
          {profile?.board ? <span className="tag tag-outline">{profile.board}</span> : null}
          {profile?.industry ? <span className="tag tag-brand">{profile.industry}</span> : null}
        </div>
        <div className="row gap-3" style={{ marginLeft: 'auto', alignItems: 'baseline' }}>
          <span className={`mono fs-20 fw-700 ${tone(profile?.pct)}`}>{fmt(profile?.price)}</span>
          <span className={`mono fs-12 ${tone(profile?.pct)}`}>
            {profile?.pct === null || profile?.pct === undefined
              ? '—'
              : `${profile.pct >= 0 ? '+' : ''}${fmt(profile.pct)}%`}
          </span>
        </div>
        <div className="row gap-4 fs-11 t-3">
          <span>
            成交 <b className="mono t-1">{profile?.amount === null || profile?.amount === undefined ? '—' : `${fmt(profile.amount)} 亿`}</b>
          </span>
          <span>
            换手 <b className="mono t-1">{profile?.turnover === null || profile?.turnover === undefined ? '—' : `${fmt(profile.turnover)}%`}</b>
          </span>
          <span>
            PE <b className="mono t-1">{fmt(profile?.peTtm)}</b>
          </span>
        </div>
      </div>
      <div className="stock-tabs">
        <div className="tabbar is-pill">
          {StockTabKeys.map((key) => (
            <Link
              key={key}
              className={`tab${key === tab ? ' is-active' : ''}`}
              to={`${listPath ?? `/stock/${code}`}${key === 'overview' ? '' : `/${key}`}`.replace('//', '/')}
            >
              {StockTabNames[key]}
            </Link>
          ))}
        </div>
      </div>

      {code.length === 0 ? (
        <div className="card">
          <div className="card-body fs-12 t-3">请从大盘概况或自选股里选择一只股票。</div>
        </div>
      ) : isPending && !data ? (
        <div className="sa-boot">正在载入 {code} …</div>
      ) : !data ? (
        <div className="card">
          <div className="card-body">
            <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />
          </div>
        </div>
      ) : (
        <Suspense fallback={<div className="sa-boot">正在载入…</div>}>
          <ModuleOf tab={tab} />
        </Suspense>
      )}
    </>
  );
}

/** 按 Tab 渲染对应模块页。模块页自身从 URL 取代码，因此这里不需要传参。 */
function ModuleOf({ tab }: { tab: string }) {
  const Component = Modules[tab] ?? Modules.overview;
  return <Component />;
}
