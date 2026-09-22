import { useState } from 'react';
import { useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { area, donut, hbar, useChart } from '@/components/charts';
import { EmptyState, ErrorState, FreshnessNote } from '@/components/ui/States';
import { fetchEquity, type Equity, type EquityPeriod } from './api';

/**
 * 投资与股权结构。
 *
 * 结构与 `design/web/stock-equity.html` 对应：结论 → 股东集中度 → 十大股东（全量 / 流通切换）
 * → 股东户数趋势 → 股权质押 → 口径说明。
 *
 * 十大股东与十大流通股东的报告期可能不同，因此分成两个标签页并各自标注报告期，
 * 不做「同一天数据」的暗示。
 */
export function StockEquityPage() {
  const { code = '' } = useParams();

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['stock', code, 'equity'],
    queryFn: () => fetchEquity(code),
    enabled: code.length > 0
  });

  if (isPending) {
    return <div className="sa-boot">正在载入 {code} 的股权结构…</div>;
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

function Content({ data }: { data: Equity }) {
  const [tab, setTab] = useState<'hold' | 'free'>('hold');

  const period = tab === 'hold' ? data.latest : data.latestFreeFloat;

  return (
    <>

      {data.insights.length > 0 ? (
        <div className="row gap-2 wrap">
          {data.insights.map((text) => (
            <span key={text} className={`tag ${tagClass(text)}`}>
              {text}
            </span>
          ))}
        </div>
      ) : null}

      <div className="grid grid-3 mt-3">
        <ConcentrationCard period={data.latest} title="前十大股东" />
        <ConcentrationCard period={data.latestFreeFloat} title="前十大流通股东" />
        <PledgeCard data={data} />
      </div>

      {/* 十大股东 / 流通股东 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">股东名单</span>
          <span className="card-sub">
            {period ? `报告期 ${period.endDate}${period.noticeDate ? ` · 公告 ${period.noticeDate}` : ''}` : '无数据'}
          </span>
          <div className="card-tools">
            <span className="segmented">
              <span className={tab === 'hold' ? 'is-active' : undefined} onClick={() => setTab('hold')}>
                十大股东
              </span>
              <span className={tab === 'free' ? 'is-active' : undefined} onClick={() => setTab('free')}>
                十大流通股东
              </span>
            </span>
          </div>
        </div>
        <div className="card-body is-flush">
          {!period || period.holders.length === 0 ? (
            <EmptyState
              title="暂无股东数据"
              hint="该报告期的股东名单尚未采集完成；股东数据为季频披露，新股可能还没有首期披露。"
            />
          ) : (
            <div className="tbl-wrap">
              <table className="tbl is-comfort">
                <thead>
                  <tr>
                    <th style={{ width: 48 }}>排名</th>
                    <th>股东名称</th>
                    <th className="num">持股数（万股）</th>
                    <th className="num">占总股本</th>
                    <th className="num hide-mobile">占流通股</th>
                    <th className="num hide-mobile">持股市值</th>
                    <th>变动</th>
                    <th className="hide-mobile">类型</th>
                  </tr>
                </thead>
                <tbody>
                  {period.holders.map((holder) => (
                    <tr key={`${holder.rank}-${holder.name}`}>
                      <td className="mono t-3">{holder.rank}</td>
                      <td>{holder.name}</td>
                      <td className="num mono">{wan(holder.holdNum)}</td>
                      <td className="num">{pct(holder.holdRatio)}</td>
                      <td className="num hide-mobile">{pct(holder.freeHoldRatio)}</td>
                      <td className="num hide-mobile">{yi(holder.marketCap)}</td>
                      <td className={`fs-11 ${changeTone(holder.change)}`}>{holder.change ?? '—'}</td>
                      <td className="fs-11 t-2 hide-mobile">{holder.holderType ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {/* 股东户数趋势 */}
      <div className="grid grid-2 mt-4">
        <div className="card">
          <div className="card-head">
            <span className="card-title">股东户数趋势</span>
            <span className="card-sub">户数下降通常对应筹码集中</span>
          </div>
          <div className="card-body">
            {data.holderCounts.length < 2 ? (
              <EmptyState title="股东户数样本不足" hint="需要至少两期数据才能看出趋势。" />
            ) : (
              <HolderCountChart counts={data.holderCounts} />
            )}
          </div>
        </div>

        <div className="card">
          <div className="card-head">
            <span className="card-title">户均持股</span>
            <span className="card-sub">股 / 户 · 市值（万元）</span>
          </div>
          <div className="card-body">
            {data.holderCounts.length < 2 ? (
              <EmptyState title="样本不足" hint="需要至少两期数据。" />
            ) : (
              <AvgHoldChart counts={data.holderCounts} />
            )}
          </div>
        </div>
      </div>

      {/* 历史报告期 */}
      {data.history.length > 0 ? (
        <div className="card mt-4">
          <div className="card-head">
            <span className="card-title">历史报告期集中度</span>
            <span className="card-sub">前十大股东合计占比</span>
          </div>
          <div className="card-body">
            <HistoryChart periods={data.history} latest={data.latest} />
          </div>
        </div>
      ) : null}

      <FreshnessNote asOf={data.asOf} source="东方财富公开 F10 报表" />

    </>
  );
}

/** 集中度卡：用环形图表示前十大合计占比与其余股份。 */
function ConcentrationCard({ period, title }: { period: EquityPeriod | null; title: string }) {
  const total = period?.totalRatio ?? null;
  const { ref } = useChart(
    donut,
    total === null
      ? []
      : [
          { name: title, value: total, color: 'var(--chart-1)' },
          { name: '其他股东', value: Math.max(0, 100 - total), color: 'var(--chart-4)' }
        ],
    {
      center: ['50%', '50%'],
      radius: ['58%', '78%'],
      legendOrient: 'horizontal',
      centerValue: total === null ? '—' : `${total.toFixed(2)}%`,
      centerLabel: title
    }
  );

  return (
    <div className="card">
      <div className="card-head">
        <span className="card-title">{title}集中度</span>
        <span className="card-sub">{period?.endDate ?? '无数据'}</span>
      </div>
      <div className="card-body">
        {total === null ? (
          <EmptyState title="未计算合计比例" hint={period?.concentration ?? '该报告期的前十大股东不齐全。'} />
        ) : (
          <div className="chart chart-md" ref={ref} />
        )}
      </div>
    </div>
  );
}

/** 质押卡。 */
function PledgeCard({ data }: { data: Equity }) {
  const pledge = data.pledge;

  return (
    <div className="card">
      <div className="card-head">
        <span className="card-title">股权质押</span>
        <span className="card-sub">{pledge?.tradeDate ?? '无数据'}</span>
      </div>
      <div className="card-body col gap-2">
        {!pledge ? (
          <EmptyState title="暂无质押数据" hint="该标的没有质押记录，或数据尚未采集完成。" />
        ) : (
          <>
            <div className="kpi">
              <span className="kpi-label">质押比例（占总股本）</span>
              <span className={`kpi-value is-sm ${(pledge.pledgeRatio ?? 0) >= 30 ? 't-down' : ''}`}>
                {pct(pledge.pledgeRatio)}
              </span>
            </div>
            <div className="row-between">
              <span className="fs-12 t-2">质押股数</span>
              <span className="mono fs-12">{pledge.pledgeShares === null ? '—' : `${pledge.pledgeShares.toLocaleString('zh-CN')} 万股`}</span>
            </div>
            <div className="row-between">
              <span className="fs-12 t-2">质押市值</span>
              <span className="mono fs-12">{yi(pledge.pledgeMarketCap)}</span>
            </div>
            <div className="row-between">
              <span className="fs-12 t-2">质押笔数</span>
              <span className="mono fs-12">{pledge.pledgeDealNum ?? '—'}</span>
            </div>
            <div className="chart-note">
              质押比例 ≥ 30% 视为偏高；口径为占总股本，非占流通股。
            </div>
          </>
        )}
      </div>
    </div>
  );
}

/** 股东户数趋势（面积图）。 */
function HolderCountChart({ counts }: { counts: Equity['holderCounts'] }) {
  const { ref } = useChart(area, {
    labels: counts.map((count) => count.endDate.slice(2, 7)),
    data: counts.map((count) => count.holderNum),
    unit: ' 户',
    name: '股东户数'
  });

  return <div className="chart chart-md" ref={ref} />;
}

/** 户均持股（横向条形，按报告期从新到旧）。 */
function AvgHoldChart({ counts }: { counts: Equity['holderCounts'] }) {
  const recent = [...counts].reverse().slice(0, 8);
  const { ref } = useChart(hbar, recent.map((count) => ({
    name: count.endDate.slice(2, 7),
    pct: count.avgHoldNum ?? 0,
    flow: count.avgMarketCap ?? undefined
  })), { unit: ' 股', valueKey: 'pct' });

  return (
    <>
      <div className="chart chart-md" ref={ref} />
      <div className="chart-note">条长为户均持股（股）；悬浮可看户均市值（万元）。</div>
    </>
  );
}

/** 历史报告期集中度。 */
function HistoryChart({ periods, latest }: { periods: EquityPeriod[]; latest: EquityPeriod | null }) {
  const rows = [...periods]
    .filter((period) => period.totalRatio !== null)
    .sort((a, b) => a.endDate.localeCompare(b.endDate));

  const all = latest?.totalRatio !== null && latest !== null
    ? [...rows, { ...latest, endDate: latest.endDate }]
    : rows;

  const { ref } = useChart(area, {
    labels: all.map((period) => period.endDate),
    data: all.map((period) => period.totalRatio ?? 0),
    unit: '%',
    name: '前十大合计'
  });

  if (rows.length === 0) {
    return <EmptyState title="历史期前十大不齐全" hint="缺行的报告期不计算合计比例，因此不画入趋势。" />;
  }

  return <div className="chart chart-md" ref={ref} />;
}

/* ------------------------------------------------------------------
   展示工具
   ------------------------------------------------------------------ */

/** 股 → 万股。 */
function wan(value: number): string {
  return (value / 10_000).toLocaleString('zh-CN', { maximumFractionDigits: 2 });
}

function pct(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : `${value.toFixed(2)}%`;
}

function yi(value: number | null | undefined): string {
  return value === null || value === undefined
    ? '—'
    : `${value.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} 亿`;
}

function changeTone(change: string | null): string {
  if (!change) return 't-3';
  if (change.includes('新进') || change.includes('增')) return 't-up';
  if (change.includes('减') || change.includes('退出')) return 't-down';
  return 't-3';
}

function tagClass(text: string): string {
  if (text.includes('集中') || text.includes('新进')) return 'tag-up';
  if (text.includes('分散')) return 'tag-down';
  if (text.includes('质押比例') && text.includes('偏高')) return 'tag-warn';
  return 'tag-outline';
}
