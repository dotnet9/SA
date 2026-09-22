import { Link, useParams } from 'react-router';
import { spark, useChart } from '@/components/charts';
import { EmptyState, ErrorState } from '@/components/ui/States';
import { useStockOverview } from './hooks';
import type { ModuleCard, StockOverview } from './api';

/**
 * 个股总览：8 个模块的摘要卡矩阵。
 *
 * 结构与 `design/web/stock.html` 对应：行情条 → 模块跳转标签 → 8 张摘要卡 → 口径说明。
 * 本批只有「趋势与价格结构」有真实数据，其余卡片由接口明确标注 `collecting`，
 * 这里渲染成显式的「采集中」空态——不显示空卡，也不显示任何占位数字。
 */
export function StockOverviewPage() {
  const { code = '' } = useParams();
  const { data, error, isPending, refetch, failureCount } = useStockOverview(code);

  if (isPending) {
    return <div className="sa-boot">正在载入 {code} 的总览…</div>;
  }

  if (!data) {
    return (
      <>
        <div className="card">
          <div className="card-body">
            <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />
          </div>
        </div>
      </>
    );
  }

  return <Content data={data} />;
}

function Content({ data }: { data: StockOverview }) {
  const { profile, modules, summary } = data;

  return (
    <>

      {/* 行情条 */}
      <div className="card is-accent">
        <div className="card-body is-tight">
          <div className="row gap-6 wrap" style={{ alignItems: 'flex-end' }}>
            <div>
              <div className={`mono fs-22 fw-700 ${tone(profile.pct)}`}>{fmt(profile.price)}</div>
              <div className={`chg ${tone(profile.pct)} fs-12`}>
                {signed(profile.chg)} <span>{signed(profile.pct, '%')}</span>
              </div>
            </div>
            <QuoteCell label="今开" value={fmt(profile.open)} />
            <QuoteCell label="最高" value={fmt(profile.high)} />
            <QuoteCell label="最低" value={fmt(profile.low)} />
            <QuoteCell label="昨收" value={fmt(profile.prevClose)} />
            <QuoteCell label="成交额" value={profile.amount === null ? '—' : `${fmt(profile.amount)} 亿`} />
            <QuoteCell label="换手率" value={profile.turnover === null ? '—' : `${fmt(profile.turnover)}%`} />
            <QuoteCell label="量比" value={fmt(profile.volRatio)} />
            <QuoteCell label="总市值" value={profile.cap === null ? '—' : `${fmt(profile.cap)} 亿`} />
            <QuoteCell label="PE(TTM)" value={fmt(profile.peTtm)} />
            <QuoteCell label="PB" value={fmt(profile.pb)} />
          </div>
        </div>
      </div>

      {/* 结论摘要 */}
      {summary.length > 0 ? (
        <div className="row gap-2 wrap mt-3">
          {summary.map((tag, index) => (
            <span key={`${tag.text}-${index}`} className={`tag ${tagClass(tag.tone)}`}>
              {tag.text}
            </span>
          ))}
        </div>
      ) : null}

      {/* 8 张模块摘要卡 */}
      <div className="col gap-3 mt-4">
        {modules.map((module) => (
          <ModuleCardView key={module.key} module={module} />
        ))}
      </div>

    </>
  );
}

function QuoteCell({ label, value }: { label: string; value: string }) {
  return (
    <div className="kpi">
      <span className="kpi-label">{label}</span>
      <span className="kpi-value is-sm">{value}</span>
    </div>
  );
}

/** 一张模块摘要卡。 */
function ModuleCardView({ module }: { module: ModuleCard }) {
  const { ref } = useChart(spark, module.thumb, { up: (module.tags[0]?.tone ?? 'neutral') === 'up' });
  const ready = module.status === 'ready';

  return (
    <div className="card">
      <div className="card-head">
        <span className="card-title">{module.name}</span>
        <div className="card-tools">
          {module.tags.map((tag, index) => (
            <span key={`${tag.text}-${index}`} className={`tag ${tagClass(tag.tone)}`}>
              {tag.text}
            </span>
          ))}
        </div>
      </div>
      <div className="card-body">
        <div className="modcard">
          <div>
            {ready && module.kpis.length > 0 ? (
              <div className="mod-kpis">
                {module.kpis.map((kpi) => (
                  <div key={kpi.label} className="kpi">
                    <span className="kpi-label">{kpi.label}</span>
                    <span className={`kpi-value is-sm ${toneText(kpi.tone)}`}>{kpi.value}</span>
                  </div>
                ))}
              </div>
            ) : (
              <EmptyState
                title={module.status === 'failed' ? '该模块数据不可用' : '该模块数据采集中'}
                hint={module.summary ?? '数据接口尚未接入，界面不展示任何数值。'}
              />
            )}
          </div>
          <div className="mod-thumb">
            {ready && module.thumb.length >= 2 ? (
              <>
                <div className="chart chart-sm" ref={ref} />
                <div className="chart-note">近 {module.thumb.length} 个交易日收盘</div>
              </>
            ) : (
              <div className="chart-note">缩略图待数据到位</div>
            )}
            {ready && module.link ? (
              <Link className="btn btn-sm btn-outline btn-block mt-3" to={module.link}>
                进入{module.name}
              </Link>
            ) : null}
          </div>
        </div>
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------
   展示工具
   ------------------------------------------------------------------ */

function fmt(value: number | null | undefined, digits = 2): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '—';
  }

  return value.toLocaleString('zh-CN', { minimumFractionDigits: digits, maximumFractionDigits: digits });
}

function signed(value: number | null | undefined, suffix = '', digits = 2): string {
  if (value === null || value === undefined) {
    return '—';
  }

  return `${value >= 0 ? '+' : ''}${fmt(value, digits)}${suffix}`;
}

function tone(value: number | null | undefined): string {
  if (value === null || value === undefined) return 'is-flat';
  return value > 0 ? 'is-up' : value < 0 ? 'is-down' : 'is-flat';
}

function toneText(toneValue: string): string {
  return toneValue === 'up' ? 't-up' : toneValue === 'down' ? 't-down' : 't-2';
}

function tagClass(toneValue: string): string {
  switch (toneValue) {
    case 'up':
      return 'tag-up';
    case 'down':
      return 'tag-down';
    case 'warn':
      return 'tag-warn';
    default:
      return 'tag-outline';
  }
}
