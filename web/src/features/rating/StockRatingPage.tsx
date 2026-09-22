import { useMemo } from 'react';
import { Link, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { band, hbar, useChart } from '@/components/charts';
import { EmptyState, ErrorState, FreshnessNote } from '@/components/ui/States';
import { fetchRating, type Rating } from './api';

/**
 * 机构评级与预测。
 *
 * 结构与 `design/web/stock-rating.html` 对应：结论 → 评级分布与目标价 → EPS 预测 → 机构倾向 → 口径说明。
 *
 * 目标价空间一律标注「相对现价」，并且区分已实现 EPS 与预测 EPS——这两处最容易引起误读。
 */
export function StockRatingPage() {
  const { code = '' } = useParams();

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['stock', code, 'rating'],
    queryFn: () => fetchRating(code),
    enabled: code.length > 0
  });

  if (isPending) {
    return <div className="sa-boot">正在载入 {code} 的机构评级…</div>;
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

function Head({ code, name, data }: { code: string; name: string; data?: Rating }) {
  return (
    <div className="sa-pagehead">
      <div>
        <div className="breadcrumb">
          <Link to={`/stock/${code}`}>个股总览</Link>
          <span className="sep">/</span>
          <span>机构评级与预测</span>
        </div>
        <h1>
          {name}
          <span className="mono fs-14 t-3" style={{ marginLeft: 8 }}>
            {code}
          </span>
        </h1>
        <div className="sub">
          {data ? `${data.orgNum} 家机构覆盖 · ${data.consensusLevel}` : '公开研报汇总'}
          {data?.asOf ? ` · 数据时间 ${data.asOf}` : ''}
        </div>
      </div>
    </div>
  );
}

function Content({ data }: { data: Rating }) {
  return (
    <>
      <Head code={data.code} name={data.name} data={data} />

      {data.insights.length > 0 ? (
        <div className="row gap-2 wrap">
          {data.insights.map((text) => (
            <span key={text} className={`tag ${tagClass(text)}`}>
              {text}
            </span>
          ))}
        </div>
      ) : null}

      {data.orgNum === 0 ? (
        <div className="card mt-3">
          <div className="card-body">
            <EmptyState
              title="暂无机构覆盖"
              hint="该标的在采集区间内没有被机构给出评级。没有覆盖本身也是一种信息，因此不渲染成「中性」。"
            />
          </div>
        </div>
      ) : (
        <>
          <div className="grid grid-3 mt-3">
            <div className="card">
              <div className="card-head">
                <span className="card-title">目标价与现价</span>
                <span className="card-sub">空间以现价为基准</span>
              </div>
              <div className="card-body col gap-2">
                {data.aimPriceMax === null ? (
                  <EmptyState title="暂无目标价" hint="机构未给出目标价，或数据尚未采集完成。" />
                ) : (
                  <>
                    <div className="row-between">
                      <span className="fs-12 t-2">现价</span>
                      <span className="mono fs-12">{price(data.currentPrice)}</span>
                    </div>
                    <div className="row-between">
                      <span className="fs-12 t-2">目标价上限</span>
                      <span className={`mono fs-12 ${(data.upsideMax ?? 0) >= 0 ? 't-up' : 't-down'}`}>
                        {price(data.aimPriceMax)}（{pct(data.upsideMax)}）
                      </span>
                    </div>
                    <div className="row-between">
                      <span className="fs-12 t-2">目标价下限</span>
                      <span className={`mono fs-12 ${(data.upsideMin ?? 0) >= 0 ? 't-up' : 't-down'}`}>
                        {price(data.aimPriceMin)}（{pct(data.upsideMin)}）
                      </span>
                    </div>
                    <div className="chart-note">
                      括号内是「相对现价」的距离，不代表预期收益；目标价可能已被现价超越。
                    </div>
                  </>
                )}
              </div>
            </div>

            {/* 机构倾向 */}
            <div className="card">
              <div className="card-head">
                <span className="card-title">机构倾向</span>
                <span className="card-sub">看多 = 买入 + 增持</span>
              </div>
              <div className="card-body col gap-2">
                <div className="kpi">
                  <span className="kpi-label">看多占比</span>
                  <span className={`kpi-value ${(data.bullishRatio ?? 0) >= 70 ? 't-up' : (data.bullishRatio ?? 0) >= 50 ? 't-2' : 't-down'}`}>
                    {data.bullishRatio === null ? '—' : `${data.bullishRatio}%`}
                  </span>
                  <span className="kpi-delta t-3">{data.consensusLevel}</span>
                </div>
                <div className="chart-note">
                  评级档位沿用上游定义，本地不做加权评分：否则会出现第二套评级标准。
                </div>
              </div>
            </div>
          </div>

          {/* 评级分布条形 */}
          <div className="card mt-4">
            <div className="card-head">
              <span className="card-title">各档位家数</span>
              <span className="card-sub">买入 → 卖出</span>
            </div>
            <div className="card-body">
              <RatingBars data={data} />
            </div>
          </div>
        </>
      )}

      {/* EPS 预测 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">EPS 预测</span>
          <span className="card-sub">实心 = 已实现 · 空心 = 预测</span>
        </div>
        <div className="card-body">
          {data.years.length === 0 ? (
            <EmptyState title="暂无 EPS 预测" hint="机构未给出盈利预测。" />
          ) : (
            <EpsChart data={data} />
          )}
        </div>
      </div>

      {/* 分期明细 */}
      {data.years.length > 0 ? (
        <div className="card mt-4">
          <div className="card-head">
            <span className="card-title">预测明细</span>
            <span className="card-sub">EPS 与同比增速</span>
          </div>
          <div className="card-body is-flush">
            <div className="tbl-wrap">
              <table className="tbl is-comfort">
                <thead>
                  <tr>
                    <th>年度</th>
                    <th className="num">EPS（元）</th>
                    <th className="num">同比增速</th>
                    <th>性质</th>
                  </tr>
                </thead>
                <tbody>
                  {data.years.map((year) => (
                    <tr key={year.year}>
                      <td className="mono">{year.year}</td>
                      <td className="num mono">{year.eps.toFixed(2)}</td>
                      <td className={`num ${(year.growthVsPrevious ?? 0) >= 0 ? 't-up' : 't-down'}`}>
                        {year.growthVsPrevious === null ? '—' : `${year.growthVsPrevious >= 0 ? '+' : ''}${year.growthVsPrevious.toFixed(2)}%`}
                      </td>
                      <td>
                        <span className={`tag ${year.isActual ? 'tag-outline' : 'tag-up'}`}>
                          {year.isActual ? '已实现' : '预测'}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      ) : null}

      <FreshnessNote asOf={data.asOf} source="东方财富公开接口（机构评级预测）" />

      <div className="legend-block mt-4">
        <b>数据来源与口径</b>
        {data.notes.map((note) => (
          <div key={note}>{note}</div>
        ))}
      </div>
    </>
  );
}

/** 各档位家数条形。 */
function RatingBars({ data }: { data: Rating }) {
  const rows = useMemo(
    () =>
      data.buckets.map((bucket) => ({
        name: bucket.level,
        // 条形长度用家数；颜色语义由 tone 决定（卖出为负向，图表用 pct 的正负表达）
        pct: bucket.tone === 'down' ? -bucket.count : bucket.count
      })),
    [data.buckets]
  );

  const { ref } = useChart(hbar, rows, { unit: ' 家', valueKey: 'pct', max: data.orgNum });

  return (
    <>
      <div className="chart chart-sm" ref={ref} />
      <div className="chart-note">条长为家数；减持与卖出以负向长度表示，便于一眼看出方向。</div>
    </>
  );
}

/** EPS 预测（含已实现值）。 */
function EpsChart({ data }: { data: Rating }) {
  const { ref } = useChart(band, {
    labels: data.years.map((year) => `${year.year}${year.isActual ? '(A)' : '(E)'}`),
    line: data.years.map((year) => year.eps),
    // 区间带用「±10% 预测区间」表达预测的不确定性
    upper: data.years.map((year) => year.eps * 1.1),
    lower: data.years.map((year) => year.eps * 0.9),
    lineName: 'EPS',
    bandName: '±10% 参考区间',
    unit: ' 元'
  });

  return (
    <>
      <div className="chart chart-md" ref={ref} />
      <div className="chart-note">
        区间带是固定 ±10% 的参考范围（便于看清趋势的斜率），不是机构给出的预测区间。
      </div>
    </>
  );
}

/* ------------------------------------------------------------------
   展示工具
   ------------------------------------------------------------------ */

function price(value: number | null): string {
  return value === null ? '—' : `${value.toFixed(2)} 元`;
}

function pct(value: number | null): string {
  return value === null ? '—' : `${value >= 0 ? '+' : ''}${value.toFixed(2)}%`;
}

function tagClass(text: string): string {
  if (text.includes('看多') || text.includes('+')) return 'tag-up';
  if (text.includes('低于现价') || text.includes('无行情')) return 'tag-down';
  return 'tag-outline';
}
