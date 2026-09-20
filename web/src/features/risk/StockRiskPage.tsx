import { Link, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { gauge, useChart } from '@/components/charts';
import { EmptyState, ErrorState, FreshnessNote } from '@/components/ui/States';
import { fetchRisk, type Risk } from '@/features/rating/api';

/**
 * 风险与舆情监控。
 *
 * 结构与 `design/web/stock-risk.html` 对应：风险分 → 风险清单 → 关键指标对照 → 口径说明。
 *
 * 每条风险都同时给出「实际值」与「阈值」，因此不需要相信一个黑箱结论。
 * 诉讼、监管问询与舆情需要公告与新闻数据源，本轮没有，页面明确说明不提供该维度。
 */
export function StockRiskPage() {
  const { code = '' } = useParams();

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['stock', code, 'risk'],
    queryFn: () => fetchRisk(code),
    enabled: code.length > 0
  });

  if (isPending) {
    return <div className="sa-boot">正在载入 {code} 的风险数据…</div>;
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

function Head({ code, name, data }: { code: string; name: string; data?: Risk }) {
  return (
    <div className="sa-pagehead">
      <div>
        <div className="breadcrumb">
          <Link to={`/stock/${code}`}>个股总览</Link>
          <span className="sep">/</span>
          <span>风险与舆情监控</span>
        </div>
        <h1>
          {name}
          <span className="mono fs-14 t-3" style={{ marginLeft: 8 }}>
            {code}
          </span>
        </h1>
        <div className="sub">
          {data ? `风险分 ${data.score}/100 · 等级 ${data.grade}` : '基于本地可复算数据'}
          {data?.asOf ? ` · 行情时间 ${data.asOf}` : ''}
        </div>
      </div>
    </div>
  );
}

function Content({ data }: { data: Risk }) {
  return (
    <>
      <Head code={data.code} name={data.name} data={data} />

      {data.insights.length > 0 ? (
        <div className="row gap-2 wrap">
          {data.insights.map((text) => (
            <span key={text} className={`tag ${tagClass(data.grade, text)}`}>
              {text}
            </span>
          ))}
        </div>
      ) : null}

      <div className="grid grid-2 mt-3">
        {/* 风险分刻度 */}
        <div className="card">
          <div className="card-head">
            <span className="card-title">风险分</span>
            <span className="card-sub">0–100，越高越需要关注</span>
          </div>
          <div className="card-body">
            <RiskGauge data={data} />
          </div>
        </div>

        {/* 关键指标对照 */}
        <div className="card">
          <div className="card-head">
            <span className="card-title">关键指标与阈值</span>
            <span className="card-sub">触发项以标记标出</span>
          </div>
          <div className="card-body is-flush">
            <div className="tbl-wrap">
              <table className="tbl">
                <thead>
                  <tr>
                    <th>指标</th>
                    <th className="num">实际值</th>
                    <th>阈值</th>
                  </tr>
                </thead>
                <tbody>
                  {data.metrics.map((metric) => (
                    <tr key={metric.label} data-chg={metric.triggered ? 'down' : 'up'}>
                      <td>
                        <span className="fs-12">{metric.label}</span>
                        {metric.triggered ? <span className="tag tag-down" style={{ marginLeft: 6 }}>触发</span> : null}
                      </td>
                      <td className="num mono">{metric.value}</td>
                      <td className="fs-11 t-3">{metric.threshold ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>

      {/* 风险清单 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">风险清单</span>
          <span className="card-sub">{data.items.length} 项</span>
        </div>
        <div className="card-body">
          {data.items.length === 0 ? (
            <EmptyState
              title="未触发任何风险阈值"
              hint="在已覆盖的维度（退市 / 财务 / 质押 / 波动 / 回撤 / 流动性 / 估值）上均未触发阈值。这不等于没有风险，只是这些维度上没有异常。"
            />
          ) : (
            <div className="col gap-3">
              {data.items.map((item) => (
                <div key={item.key} className="col gap-1">
                  <div className="row-between wrap gap-2">
                    <span className="row gap-2" style={{ alignItems: 'center' }}>
                      <span className={`tag ${item.level === 'high' ? 'tag-down' : item.level === 'medium' ? 'tag-warn' : 'tag-outline'}`}>
                        {item.level === 'high' ? '高' : item.level === 'medium' ? '中' : '低'}
                      </span>
                      <span className="tag tag-outline">{item.category}</span>
                      <span className="fw-600 fs-12">{item.title}</span>
                    </span>
                    <span className="fs-11 t-3">
                      实际 {item.metric ?? '—'} · 阈值 {item.threshold ?? '—'}
                    </span>
                  </div>
                  <div className="fs-11 t-2">{item.detail}</div>
                  <div className="divider" />
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      <FreshnessNote asOf={data.asOf} source="本地日线 + 业绩报表 + 股权质押 + 全市场快照（不新增采集源）" />

      <div className="legend-block mt-4">
        <b>数据来源与口径</b>
        {data.notes.map((note) => (
          <div key={note}>{note}</div>
        ))}
      </div>
    </>
  );
}

/** 风险分刻度：阈值 30（中）/ 55（高）与后端计分规则一致。 */
function RiskGauge({ data }: { data: Risk }) {
  const { ref } = useChart(gauge, {
    value: data.score,
    max: 100,
    name: `风险等级 ${data.grade}`,
    warnAt: 30,
    dangerAt: 55,
    decimals: 0
  });

  return (
    <>
      <div className="chart chart-md" ref={ref} />
      <div className="chart-note">
        刻度线与后端计分一致：{data.items.filter((item) => item.level === 'high').length} 项高风险（各 20 分）、
        {data.items.filter((item) => item.level === 'medium').length} 项中风险（各 8 分），上限 100。
      </div>
    </>
  );
}

function tagClass(grade: string, text: string): string {
  if (text.includes('未触发')) return 'tag-up';
  if (grade === '高') return 'tag-down';
  if (grade === '中') return 'tag-warn';
  return 'tag-outline';
}
