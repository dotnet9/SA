import { useMemo } from 'react';
import { Link, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { area, combo, gauge, useChart } from '@/components/charts';
import { EmptyState, ErrorState, FreshnessNote } from '@/components/ui/States';
import { fetchFinance, type Finance } from './api';

/**
 * 盈利与财务表现。
 *
 * 结构与 `design/web/stock-finance.html` 对应：最新一期指标卡 → 营收/净利趋势 →
 * 盈利能力（ROE / 毛利率）→ 每股指标 → 业绩预告 → 口径说明。
 *
 * 报告期为累计口径（半年报即上半年累计），页面明确标注，避免被读成单季。
 */
export function StockFinancePage() {
  const { code = '' } = useParams();

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['stock', code, 'finance'],
    queryFn: () => fetchFinance(code),
    enabled: code.length > 0
  });

  if (isPending) {
    return <div className="sa-boot">正在载入 {code} 的财务数据…</div>;
  }

  if (!data) {
    return (
      <>
        <Head code={code} name={code} />
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

function Head({ code, name, data }: { code: string; name: string; data?: Finance }) {
  return (
    <div className="sa-pagehead">
      <div>
        <div className="breadcrumb">
          <Link to={`/stock/${code}`}>个股总览</Link>
          <span className="sep">/</span>
          <span>盈利与财务表现</span>
        </div>
        <h1>
          {name}
          <span className="mono fs-14 t-3" style={{ marginLeft: 8 }}>
            {code}
          </span>
        </h1>
        <div className="sub">
          报告期累计口径
          {data?.latest ? ` · 最新 ${data.latest.reportType ?? ''}（${data.latest.reportDate}）` : ''}
          {data?.latest?.noticeDate ? ` · 公告于 ${data.latest.noticeDate}` : ''}
        </div>
      </div>
    </div>
  );
}

function Content({ data }: { data: Finance }) {
  const latest = data.latest;
  const labels = data.trend.map((point) => point.label);

  // 营收与净利同图（量级差一个数量级时用双轴会误读，这里统一用柱 + 折线）
  const { ref: revenueRef } = useChart(combo, {
    labels,
    bars: [{ name: '营业总收入(亿)', data: data.trend.map((p) => p.revenue ?? 0) }],
    lines: [{ name: '归母净利润(亿)', data: data.trend.map((p) => p.netProfit ?? 0), color: undefined, area: true }],
    unit: ' 亿'
  });

  // 同比增速：正负方向本身就是信息
  const { ref: growthRef } = useChart(area, {
    labels,
    series: [
      { name: '营收同比(%)', data: data.trend.map((p) => p.revenueYoy ?? 0) },
      { name: '净利同比(%)', data: data.trend.map((p) => p.netProfitYoy ?? 0) }
    ],
    unit: '%'
  });

  // 盈利能力
  const { ref: profitabilityRef } = useChart(area, {
    labels,
    series: [
      { name: '加权ROE(%)', data: data.trend.map((p) => p.roe ?? 0), fill: false },
      { name: '毛利率(%)', data: data.trend.map((p) => p.grossMargin ?? 0), fill: false }
    ],
    unit: '%',
    startAtZero: true
  });

  // ROE 刻度：财务上的经验区间是 8%（尚可）与 15%（优秀），用它做刻度比 0–100 更有意义
  const roeGauge = useMemo(
    () => ({
      value: latest?.roe ?? 0,
      max: 30,
      name: '加权 ROE',
      unit: '%',
      warnAt: 8,
      dangerAt: 15,
      decimals: 2
    }),
    [latest?.roe]
  );
  const { ref: gaugeRef } = useChart(gauge, roeGauge);

  return (
    <>
      <Head code={data.code} name={data.name} data={data} />

      {/* 结论标签 */}
      {data.insights.length > 0 ? (
        <div className="row gap-2 wrap">
          {data.insights.map((text) => (
            <span key={text} className={`tag ${tagClass(text)}`}>
              {text}
            </span>
          ))}
        </div>
      ) : null}

      {/* 最新一期指标 */}
      <div className="grid grid-4 mt-3">
        <MetricCard label="营业总收入" value={yi(latest?.revenue)} delta={pct(latest?.revenueYoy)} deltaTone={latest?.revenueYoy} />
        <MetricCard label="归母净利润" value={yi(latest?.netProfit)} delta={pct(latest?.netProfitYoy)} deltaTone={latest?.netProfitYoy} />
        <MetricCard label="加权 ROE" value={num(latest?.roe, '%')} delta={latest?.bps === null || latest?.bps === undefined ? undefined : `每股净资产 ${num(latest.bps)} 元`} />
        <MetricCard label="销售毛利率" value={num(latest?.grossMargin, '%')} delta={`每股收益 ${num(latest?.eps)} 元`} />
      </div>

      <div className="grid grid-4 mt-3">
        <MetricCard label="每股经营现金流" value={num(latest?.operatingCashFlowPerShare)} delta={cashFlowHint(latest?.operatingCashFlowPerShare, latest?.eps)} deltaTone={latest?.operatingCashFlowPerShare} />
        <MetricCard label="扣非每股收益" value={num(latest?.deductedEps)} />
        <MetricCard label="营收环比" value={pct(latest?.revenueQoq)} deltaTone={latest?.revenueQoq} />
        <MetricCard label="股息率" value={num(latest?.dividendYield, '%')} delta={latest?.dividendPlan ?? undefined} />
      </div>

      {/* 营收与净利 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">营收与净利润</span>
          <span className="card-sub">柱 = 营业总收入 · 线 = 归母净利润（累计口径）</span>
        </div>
        <div className="card-body">
          {data.trend.length === 0 ? (
            <EmptyState title="暂无可画的报告期" hint="该标的的财报记录尚未采集完成。" />
          ) : (
            <div className="chart chart-lg" ref={revenueRef} />
          )}
        </div>
      </div>

      <div className="grid grid-2 mt-4">
        {/* 同比增速 */}
        <div className="card">
          <div className="card-head">
            <span className="card-title">同比增速</span>
            <span className="card-sub">取自上游的同口径计算</span>
          </div>
          <div className="card-body">
            <div className="chart chart-md" ref={growthRef} />
            <div className="chart-note">
              同比由上游按调整后同口径给出；本地不做两期相除，避免追溯调整时算错。
            </div>
          </div>
        </div>

        {/* 盈利能力 */}
        <div className="card">
          <div className="card-head">
            <span className="card-title">盈利能力</span>
            <span className="card-sub">ROE 与毛利率</span>
          </div>
          <div className="card-body">
            <div className="chart chart-md" ref={profitabilityRef} />
          </div>
        </div>
      </div>

      {/* ROE 刻度 + 业绩预告 */}
      <div className="grid grid-2 mt-4">
        <div className="card">
          <div className="card-head">
            <span className="card-title">ROE 刻度</span>
            <span className="card-sub">0–30%，刻度线 8% / 15%</span>
          </div>
          <div className="card-body">
            <div className="chart chart-md" ref={gaugeRef} />
            <div className="chart-note">
              刻度上限 30%：超过该值已属极少数，继续放大刻度反而看不清常态区间。
            </div>
          </div>
        </div>

        <div className="card">
          <div className="card-head">
            <span className="card-title">业绩预告</span>
            <span className="card-sub">{data.forecasts.length} 条</span>
          </div>
          <div className="card-body">
            {data.forecasts.length === 0 ? (
              <EmptyState
                title="暂无业绩预告"
                hint="该公司在采集区间内未发布业绩预告（不是所有公司都会发布）。"
              />
            ) : (
              <div className="col gap-3">
                {data.forecasts.map((forecast) => (
                  <div key={`${forecast.reportDate}-${forecast.noticeDate}`} className="col gap-1">
                    <div className="row-between">
                      <span className="fs-12 fw-600">
                        报告期 {forecast.reportDate}
                        {forecast.forecastType ? (
                          <span className="tag tag-outline" style={{ marginLeft: 6 }}>
                            {forecast.forecastType}
                          </span>
                        ) : null}
                      </span>
                      <span className="fs-11 t-3">{forecast.noticeDate ?? ''}</span>
                    </div>
                    <div className="fs-12 t-2">
                      预计净利 {yi(forecast.netProfitMin)} ~ {yi(forecast.netProfitMax)}
                      {forecast.changeMin !== null && forecast.changeMax !== null
                        ? ` · 同比 ${sign(forecast.changeMin)}% ~ ${sign(forecast.changeMax)}%`
                        : ''}
                    </div>
                    {forecast.summary ? <div className="fs-11 t-3">{forecast.summary}</div> : null}
                    <div className="divider" />
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* 分期明细 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">分期明细</span>
          <span className="card-sub">最近 {data.periods.length} 期（累计口径）</span>
        </div>
        <div className="card-body is-flush">
          <div className="tbl-wrap">
            <table className="tbl is-comfort">
              <thead>
                <tr>
                  <th>报告期</th>
                  <th className="num">营业总收入</th>
                  <th className="num">同比</th>
                  <th className="num">归母净利润</th>
                  <th className="num">同比</th>
                  <th className="num hide-mobile">毛利率</th>
                  <th className="num hide-mobile">ROE</th>
                  <th className="num hide-mobile">每股收益</th>
                  <th>分红方案</th>
                </tr>
              </thead>
              <tbody>
                {data.periods.map((period) => (
                  <tr key={period.reportDate} data-chg={(period.netProfitYoy ?? 0) >= 0 ? 'up' : 'down'}>
                    <td>
                      <span className="fs-12">{period.reportDate}</span>
                      {period.reportType ? <span className="fs-11 t-3" style={{ marginLeft: 6 }}>{period.reportType}</span> : null}
                    </td>
                    <td className="num mono">{yi(period.revenue)}</td>
                    <td className={`num ${tone(period.revenueYoy)}`}>{pct(period.revenueYoy)}</td>
                    <td className="num mono">{yi(period.netProfit)}</td>
                    <td className={`num ${tone(period.netProfitYoy)}`}>{pct(period.netProfitYoy)}</td>
                    <td className="num hide-mobile">{num(period.grossMargin, '%')}</td>
                    <td className="num hide-mobile">{num(period.roe, '%')}</td>
                    <td className="num hide-mobile">{num(period.eps)}</td>
                    <td className="fs-11 t-2">{period.dividendPlan ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <FreshnessNote asOf={data.asOf} source="东方财富公开报表接口（业绩报表 / 业绩预告）" />

      <div className="legend-block mt-4">
        <b>数据来源与口径</b>
        {data.notes.map((note) => (
          <div key={note}>{note}</div>
        ))}
      </div>
    </>
  );
}

function MetricCard({
  label,
  value,
  delta,
  deltaTone
}: {
  label: string;
  value: string;
  delta?: string;
  deltaTone?: number | null;
}) {
  return (
    <div className="card">
      <div className="card-body is-tight">
        <div className="kpi">
          <span className="kpi-label">{label}</span>
          <span className="kpi-value is-sm">{value}</span>
          {delta ? <span className={`kpi-delta ${deltaTone === undefined || deltaTone === null ? 't-3' : tone(deltaTone) === 'is-up' ? 't-up' : tone(deltaTone) === 'is-down' ? 't-down' : 't-3'}`}>{delta}</span> : null}
        </div>
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------
   展示工具
   ------------------------------------------------------------------ */

function yi(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : `${value.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} 亿`;
}

function num(value: number | null | undefined, suffix = ''): string {
  return value === null || value === undefined
    ? '—'
    : `${value.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}${suffix}`;
}

function pct(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : `${sign(value)}%`;
}

function sign(value: number): string {
  return `${value >= 0 ? '+' : ''}${value.toFixed(2)}`;
}

function tone(value: number | null | undefined): string {
  if (value === null || value === undefined) return '';
  return value > 0 ? 'is-up' : value < 0 ? 'is-down' : 'is-flat';
}

/** 经营现金流为负而每股收益为正时给出提示（盈利质量存疑）。 */
function cashFlowHint(cashFlow: number | null | undefined, eps: number | null | undefined): string | undefined {
  if (cashFlow === null || cashFlow === undefined) {
    return undefined;
  }

  if (cashFlow < 0 && (eps ?? 0) > 0) {
    return '现金流为负 · 盈利质量存疑';
  }

  return '元 / 股';
}

function tagClass(text: string): string {
  if (text.includes('下滑') || text.includes('不增利') || text.includes('存疑')) {
    return 'tag-down';
  }

  if (text.includes('增长')) {
    return 'tag-up';
  }

  return 'tag-outline';
}
