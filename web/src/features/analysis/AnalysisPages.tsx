import { useMemo, useState } from 'react';
import { Link, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { hbar, useChart } from '@/components/charts';
import { EmptyState, ErrorState, FreshnessNote } from '@/components/ui/States';
import { fetchCausalChain, fetchProsperity, type CausalChain, type Prosperity } from './api';

/**
 * 行业景气度。
 *
 * 规则引擎打分（非模型推断）：五个维度各自的<b>实际值、得分、权重与口径</b>都列出来，用户可逐项核对。
 * 样本不足的维度按中性 50 分计入并注明，而不是当 0 分——数据缺失不等于行业变差。
 */
export function ProsperityPage() {
  const [take, setTake] = useState(30);

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['prosperity', take],
    queryFn: () => fetchProsperity(take)
  });

  const [selected, setSelected] = useState<string | null>(null);

  if (isPending) {
    return <div className="sa-boot">正在载入行业景气度…</div>;
  }

  if (!data) {
    return (
      <>
        <ProsperityHead />
        <div className="card">
          <div className="card-body">
            <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />
          </div>
        </div>
      </>
    );
  }

  const current = data.items.find((item) => item.code === selected) ?? data.items[0];

  return (
    <>
      <ProsperityHead asOf={data.asOf} />

      {data.items.length === 0 ? (
        <div className="card">
          <div className="card-body">
            <EmptyState
              title="暂无可打分的行业"
              hint="景气度需要行业板块快照与全市场快照，采集完成后即会出现。"
            />
          </div>
        </div>
      ) : (
        <>
          <div className="row gap-2 wrap" style={{ alignItems: 'center' }}>
            <span className="segmented">
              {[10, 30, 0].map((value) => (
                <span key={value} className={take === value ? 'is-active' : undefined} onClick={() => setTake(value)}>
                  {value === 0 ? '全部行业' : `前 ${value} 名`}
                </span>
              ))}
            </span>
            <span className="fs-11 t-3">共 {data.items.length} 个行业 · 按综合景气分倒序</span>
          </div>

          <div className="grid grid-2 mt-3">
            {/* 排行 */}
            <div className="card">
              <div className="card-head">
                <span className="card-title">景气度排行</span>
                <span className="card-sub">点击任意行查看打分构成</span>
              </div>
              <div className="card-body is-flush">
                <div className="tbl-wrap" style={{ maxHeight: 520, overflowY: 'auto' }}>
                  <table className="tbl is-comfort">
                    <thead>
                      <tr>
                        <th style={{ width: 44 }}>#</th>
                        <th>行业</th>
                        <th className="num">景气分</th>
                        <th>分档</th>
                        <th className="num hide-mobile">相对强弱</th>
                        <th className="num hide-mobile">带宽</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.items.map((item, index) => (
                        <tr
                          key={item.code}
                          className={current?.code === item.code ? 'is-active' : undefined}
                          style={{ cursor: 'pointer' }}
                          onClick={() => setSelected(item.code)}
                        >
                          <td className="mono t-3">{index + 1}</td>
                          <td>
                            <span className="fs-12">{item.name}</span>
                            <span className="fs-11 t-3" style={{ marginLeft: 6 }}>
                              {item.memberCount} 只
                            </span>
                          </td>
                          <td className={`num mono ${item.score >= 70 ? 't-up' : item.score < 45 ? 't-down' : ''}`}>
                            {item.score}
                          </td>
                          <td>
                            <span className={`tag ${gradeClass(item.score)}`}>{item.grade}</span>
                          </td>
                          <td className={`num hide-mobile ${(item.relativeStrength ?? 0) >= 0 ? 't-up' : 't-down'}`}>
                            {item.relativeStrength === null ? '—' : `${item.relativeStrength >= 0 ? '+' : ''}${item.relativeStrength.toFixed(2)}%`}
                          </td>
                          <td className="num hide-mobile">{item.bandwidth === null ? '—' : item.bandwidth.toFixed(2)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>

            {/* 打分构成 */}
            <div className="col gap-4">
              <div className="card is-accent">
                <div className="card-head">
                  <span className="card-title">{current?.name} · 打分构成</span>
                  <span className="card-sub">
                    综合 {current?.score} 分（{current?.grade}）
                  </span>
                </div>
                <div className="card-body is-flush">
                  <div className="tbl-wrap">
                    <table className="tbl">
                      <thead>
                        <tr>
                          <th>维度</th>
                          <th>实际值</th>
                          <th className="num">得分</th>
                          <th className="num">权重</th>
                          <th className="num">贡献</th>
                        </tr>
                      </thead>
                      <tbody>
                        {(current?.factors ?? []).map((factor) => (
                          <tr key={factor.name}>
                            <td className="fs-12">{factor.name}</td>
                            <td className="fs-11 t-2">{factor.value}</td>
                            <td className={`num ${factor.score >= 60 ? 't-up' : factor.score <= 40 ? 't-down' : ''}`}>
                              {factor.score}
                            </td>
                            <td className="num t-3">{factor.weight}%</td>
                            <td className="num mono">{factor.weighted}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                  <div className="chart-note" style={{ padding: '8px 12px' }}>
                    {(current?.factors ?? []).map((factor) => (
                      <div key={factor.name}>
                        · <b>{factor.name}</b>：{factor.note}
                      </div>
                    ))}
                  </div>
                </div>
              </div>

              <div className="card">
                <div className="card-head">
                  <span className="card-title">景气分与带宽对照</span>
                  <span className="card-sub">带宽 = 行业与基准的日收益相关性</span>
                </div>
                <div className="card-body">
                  <ProsperityChart items={data.items.slice(0, 12)} />
                </div>
              </div>
            </div>
          </div>
        </>
      )}

      <FreshnessNote asOf={data.asOf} source="行业板块快照 + 行业指数日线 + 全市场快照（规则引擎）" />

      <div className="legend-block mt-4">
        <b>数据来源与口径</b>
        {data.notes.map((note) => (
          <div key={note}>{note}</div>
        ))}
      </div>
    </>
  );
}

function ProsperityHead({ asOf }: { asOf?: string | null }) {
  return (
    <div className="sa-pagehead">
      <div>
        <div className="breadcrumb">
          <Link to="/market">市场概览</Link>
          <span className="sep">/</span>
          <span>行业景气度</span>
        </div>
        <h1>行业景气度</h1>
        <div className="sub">
          规则引擎打分（非模型推断）· 五个维度各自的权重与口径都列出，可逐项核对
          {asOf ? ` · 行情时间 ${asOf}` : ''}
        </div>
      </div>
    </div>
  );
}

/** 景气分（条长）与带宽（提示里）对照。 */
function ProsperityChart({ items }: { items: Prosperity[] }) {
  const rows = useMemo(
    () =>
      items.map((item) => ({
        name: item.name,
        pct: item.score,
        note:
          `景气分 ${item.score}（${item.grade}）`
          + (item.relativeStrength === null ? '' : ` · 相对强弱 ${item.relativeStrength.toFixed(2)}%`)
          + (item.bandwidth === null ? ' · 带宽样本不足' : ` · 带宽 ${item.bandwidth.toFixed(2)}`)
      })),
    [items]
  );

  // 分数是 0–100 的正值，用 valueKey='pct' 复用横条工厂（颜色按分数与 50 的关系自动取涨跌色）
  const { ref } = useChart(hbar, rows.map((row) => ({ ...row, pct: row.pct - 50 })), { unit: '', valueKey: 'pct' });

  return (
    <>
      <div className="chart chart-md" ref={ref} />
      <div className="chart-note">
        条长以 50 分为中线（向右=高于中性、向左=低于中性），颜色表示方向；悬浮可看相对强弱与带宽。
      </div>
    </>
  );
}

function gradeClass(score: number): string {
  if (score >= 85) return 'tag-up';
  if (score >= 70) return 'tag-up';
  if (score >= 45) return 'tag-outline';
  return 'tag-down';
}

/**
 * 个股因果链与传导带宽。
 *
 * 与「事件与影响」的区别：那张页列的是「发生了什么」，这张页回答「事件之后个股与行业分别怎么走、
 * 个股对行业的跟随程度有多强」。每一环都带可复算的证据与置信度。
 */
export function CausalChainPage() {
  const { code = '' } = useParams();

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['stock', code, 'causal'],
    queryFn: () => fetchCausalChain(code),
    enabled: code.length > 0
  });

  if (!code) {
    return <EmptyState title="缺少证券代码" hint="请从个股页进入，或在地址栏补上代码。" />;
  }

  if (isPending) {
    return <div className="sa-boot">正在载入 {code} 的因果链…</div>;
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

  return <CausalContent data={data} />;
}

function CausalContent({ data }: { data: CausalChain }) {
  return (
    <>
      <Head code={data.code} name={data.name} industry={data.industry} asOf={data.asOf} />

      {data.insights.length > 0 ? (
        <div className="row gap-2 wrap">
          {data.insights.map((text) => (
            <span key={text} className="tag tag-outline">
              {text}
            </span>
          ))}
        </div>
      ) : null}

      <div className="grid grid-2 mt-3">
        {/* 链条 */}
        <div className="card is-accent">
          <div className="card-head">
            <span className="card-title">因果链</span>
            <span className="card-sub">事件 → 行业反应 → 当前状态（每环带证据与置信度）</span>
          </div>
          <div className="card-body col gap-3">
            {data.links.length === 0 ? (
              <EmptyState title="暂无链条环节" hint="需要日线或事件数据；采集完成后自动出现。" />
            ) : (
              data.links.map((link) => (
                <div key={`${link.order}-${link.title}`} className="col gap-1">
                  <div className="row-between wrap gap-2">
                    <span className="row gap-2" style={{ alignItems: 'center' }}>
                      <span className="tag tag-brand">{link.stage}</span>
                      <span className={`fw-600 fs-12 ${link.tone === 'up' ? 't-up' : link.tone === 'down' ? 't-down' : ''}`}>
                        {link.title}
                      </span>
                    </span>
                    <span className="fs-11 t-3" title={link.confidenceNote}>
                      置信度 {link.confidence}
                    </span>
                  </div>
                  <div className="fs-11 t-2">{link.evidence}</div>
                  <div className="fs-11 t-3">{link.confidenceNote}</div>
                  <div className="divider" />
                </div>
              ))
            )}
          </div>
        </div>

        {/* 传导带宽 */}
        <div className="col gap-4">
          <div className="card">
            <div className="card-head">
              <span className="card-title">传导带宽</span>
              <span className="card-sub">{data.industry ?? '行业未知'}</span>
            </div>
            <div className="card-body col gap-2">
              <BandwidthRow label="与行业指数相关性" value={data.bandwidth.industryCorrelation} digits={2} />
              <BandwidthRow label="与基准（沪深300）相关性" value={data.bandwidth.benchmarkCorrelation} digits={2} />
              <BandwidthRow label="对行业的贝塔" value={data.bandwidth.beta} digits={2} />
              <BandwidthRow label="传导带宽" value={data.bandwidth.bandwidth} digits={2} strong />
              <div className="fs-11 t-3">重叠样本 {data.bandwidth.samples} 天</div>
              <div className="chart-note">
                <span>{data.bandwidth.note}</span>
              </div>
            </div>
          </div>

          <div className="card">
            <div className="card-head">
              <span className="card-title">怎么读这张页</span>
            </div>
            <div className="card-body">
              <div className="legend-block">
                <b>带宽越大</b>：个股越跟随行业，行业信号对该股的参考价值越高。
                <br />
                <b>贝塔 &gt; 1</b>：行业上涨时该股涨得更多（弹性更高），下跌时也跌得更多。
                <br />
                <b>置信度</b>由样本量与该环的可验证程度决定：直接来自最新日线的环节置信度最高，
                事件影响则需要事件日之后的行情样本才可信。
                <br />
                <b>相关性不等于因果</b>：本页回答「事件之后两者分别怎么走」，
                不声称「谁导致了谁」——后者需要供货占比、成本占比等产业数据，本轮没有可用的公开源。
              </div>
              <div className="row gap-2 wrap mt-3">
                <Link className="btn btn-sm btn-outline" to={`/stock/${data.code}/events`}>
                  查看事件时间线
                </Link>
                <Link className="btn btn-sm btn-outline" to={`/stock/${data.code}/industry`}>
                  查看同业对比
                </Link>
                <Link className="btn btn-sm btn-ghost" to={`/stock/${data.code}/trend`}>
                  查看趋势与价格结构
                </Link>
              </div>
            </div>
          </div>
        </div>
      </div>

      <FreshnessNote asOf={data.asOf} source="本地日线（个股 / 行业指数 90.BKxxxx / 沪深300）+ 已落地事件" />

      <div className="legend-block mt-4">
        <b>数据来源与口径</b>
        {data.notes.map((note) => (
          <div key={note}>{note}</div>
        ))}
      </div>
    </>
  );
}

function BandwidthRow({
  label,
  value,
  digits,
  strong
}: {
  label: string;
  value: number | null;
  digits: number;
  strong?: boolean;
}) {
  return (
    <div className="row-between">
      <span className={`fs-12 ${strong ? 't-1 fw-600' : 't-2'}`}>{label}</span>
      <span className={`mono fs-12 ${value === null ? 't-3' : ''}`}>
        {value === null ? '—' : value.toFixed(digits)}
      </span>
    </div>
  );
}

function Head({
  code,
  name,
  industry,
  asOf
}: {
  code: string;
  name: string;
  industry?: string | null;
  asOf?: string | null;
}) {
  return (
    <div className="sa-pagehead">
      <div>
        <div className="breadcrumb">
          <Link to={`/stock/${code}`}>个股总览</Link>
          <span className="sep">/</span>
          <span>因果链与传导带宽</span>
        </div>
        <h1>
          {name}
          <span className="mono fs-14 t-3" style={{ marginLeft: 8 }}>
            {code}
          </span>
        </h1>
        <div className="sub">
          规则引擎推算（非模型推断）· 每一环都给出可复算的证据与置信度
          {industry ? ` · 所属行业「${industry}」` : ''}
          {asOf ? ` · 行情时间 ${asOf}` : ''}
        </div>
      </div>
    </div>
  );
}
