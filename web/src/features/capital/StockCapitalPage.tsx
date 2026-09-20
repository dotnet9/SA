import { useMemo } from 'react';
import { Link, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { area, combo, donut, useChart } from '@/components/charts';
import { EmptyState, ErrorState, FreshnessNote } from '@/components/ui/States';
import { fetchCapital, type Capital } from './api';

/**
 * 资金面与筹码。
 *
 * 结构与 `design/web/stock-capital.html` 对应：结论 → 资金流汇总卡 → 主力净额趋势
 * → 五档构成 → 两融趋势 → 陆股通（季频）→ 龙虎榜 → 大宗交易 → 口径说明。
 *
 * 「筹码分布」区块不渲染：分价位持仓数据本轮没有可用的公开源，
 * 页面在口径说明里明确写出，而不是画一个假的分布图。
 */
export function StockCapitalPage() {
  const { code = '' } = useParams();

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['stock', code, 'capital'],
    queryFn: () => fetchCapital(code),
    enabled: code.length > 0
  });

  if (isPending) {
    return <div className="sa-boot">正在载入 {code} 的资金面数据…</div>;
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

function Head({ code, name, data }: { code: string; name: string; data?: Capital }) {
  return (
    <div className="sa-pagehead">
      <div>
        <div className="breadcrumb">
          <Link to={`/stock/${code}`}>个股总览</Link>
          <span className="sep">/</span>
          <span>资金面与筹码</span>
        </div>
        <h1>
          {name}
          <span className="mono fs-14 t-3" style={{ marginLeft: 8 }}>
            {code}
          </span>
        </h1>
        <div className="sub">
          主力资金按日统计 · 金额单位亿元
          {data?.fundFlow.length ? ` · 资金流至 ${data.fundFlow[data.fundFlow.length - 1].date}` : ''}
        </div>
      </div>
    </div>
  );
}

function Content({ data }: { data: Capital }) {
  const latestMargin = data.margins.length > 0 ? data.margins[data.margins.length - 1] : null;

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

      {/* 汇总卡 */}
      <div className="grid grid-4 mt-3">
        <SummaryCard title="近 5 日主力" summary={data.summary} />
        <SummaryCard title="近 20 日主力" summary={data.summary20} />
        <div className="card">
          <div className="card-body is-tight">
            <div className="kpi">
              <span className="kpi-label">融资余额</span>
              <span className="kpi-value is-sm">{yi(latestMargin?.financeBalance)}</span>
              <span className="kpi-delta t-3">{latestMargin ? `${latestMargin.date} · 占流通市值 ${pct(latestMargin.financeBalanceRatio)}` : '尚无数据'}</span>
            </div>
          </div>
        </div>
        <div className="card">
          <div className="card-body is-tight">
            <div className="kpi">
              <span className="kpi-label">陆股通持股</span>
              <span className="kpi-value is-sm">
                {data.northbound.length > 0 ? wan(data.northbound[0].holdShares) : '—'}
              </span>
              <span className="kpi-delta t-3">
                {data.northbound.length > 0
                  ? `${data.northbound[0].dateType ?? data.northbound[0].holdDate} · 季度披露`
                  : '尚无数据'}
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* 主力净额趋势 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">主力资金净额</span>
          <span className="card-sub">柱 = 主力净额 · 线 = 涨跌幅（右轴）</span>
        </div>
        <div className="card-body">
          {data.fundFlow.length === 0 ? (
            <EmptyState title="暂无资金流数据" hint="该标的的资金流尚未采集完成（停牌期间也可能没有数据）。" />
          ) : (
            <MainNetChart data={data} />
          )}
        </div>
      </div>

      <div className="grid grid-3 mt-4">
        {/* 五档构成 */}
        <div className="card">
          <div className="card-head">
            <span className="card-title">最近一日五档构成</span>
            <span className="card-sub">按净额绝对值看结构</span>
          </div>
          <div className="card-body">
            {data.fundFlow.length === 0 ? (
              <EmptyState title="暂无数据" hint="资金流数据尚未采集完成。" />
            ) : (
              <LayersChart latest={data.fundFlow[data.fundFlow.length - 1]} />
            )}
          </div>
        </div>

        {/* 两融趋势 */}
        <div className="card">
          <div className="card-head">
            <span className="card-title">两融余额</span>
            <span className="card-sub">融资 / 融券（亿元）</span>
          </div>
          <div className="card-body">
            {data.margins.length < 2 ? (
              <EmptyState title="两融样本不足" hint="需要至少两期数据才能看出趋势。" />
            ) : (
              <MarginChart data={data} />
            )}
          </div>
        </div>

        {/* 陆股通 */}
        <div className="card">
          <div className="card-head">
            <span className="card-title">陆股通持股</span>
            <span className="card-sub">季频披露</span>
          </div>
          <div className="card-body">
            {data.northbound.length === 0 ? (
              <EmptyState
                title="暂无陆股通数据"
                hint="该标的未进入陆股通持股排名，或数据尚未采集完成。"
              />
            ) : (
              <div className="col gap-2">
                {data.northbound.slice(0, 4).map((row) => (
                  <div key={row.holdDate} className="col gap-1">
                    <div className="row-between">
                      <span className="fs-12 fw-600">{row.dateType ?? row.holdDate}</span>
                      <span className={`mono fs-12 ${(row.addShares ?? 0) >= 0 ? 't-up' : 't-down'}`}>
                        {(row.addShares ?? 0) >= 0 ? '+' : ''}
                        {wan(row.addShares)} 股
                      </span>
                    </div>
                    <div className="row-between fs-11 t-3">
                      <span>持股 {wan(row.holdShares)}</span>
                      <span>
                        占流通股 {pct(row.freeSharesRatio)}
                        {row.addSharesAmp === null ? '' : ` · ${row.addSharesAmp >= 0 ? '+' : ''}${row.addSharesAmp.toFixed(2)}%`}
                      </span>
                    </div>
                    <div className="divider" />
                  </div>
                ))}
                <div className="chart-note">
                  陆股通为季度披露，只能反映该季度整体增减，不能当作逐日北向净买入。
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* 龙虎榜 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">龙虎榜</span>
          <span className="card-sub">最近 {data.billboards.length} 次上榜</span>
        </div>
        <div className="card-body is-flush">
          {data.billboards.length === 0 ? (
            <EmptyState title="近期未上榜" hint="该标的在采集区间内没有龙虎榜记录。" />
          ) : (
            <div className="tbl-wrap">
              <table className="tbl is-comfort">
                <thead>
                  <tr>
                    <th>上榜日</th>
                    <th>原因</th>
                    <th className="num">净买入</th>
                    <th className="num hide-mobile">买入</th>
                    <th className="num hide-mobile">卖出</th>
                    <th className="num">当日涨跌</th>
                    <th className="num hide-mobile">次日</th>
                    <th className="num hide-mobile">后5日</th>
                    <th className="num hide-mobile">后10日</th>
                  </tr>
                </thead>
                <tbody>
                  {data.billboards.map((row) => (
                    <tr key={`${row.tradeDate}-${row.reason ?? ''}`} data-chg={(row.netAmount ?? 0) >= 0 ? 'up' : 'down'}>
                      <td className="mono">{row.tradeDate}</td>
                      <td className="fs-11 t-2">{row.reason ?? '—'}</td>
                      <td className={`num ${(row.netAmount ?? 0) >= 0 ? 't-up' : 't-down'}`}>{yi(row.netAmount)}</td>
                      <td className="num hide-mobile">{yi(row.buyAmount)}</td>
                      <td className="num hide-mobile">{yi(row.sellAmount)}</td>
                      <td className={`num ${tone(row.changePercent)}`}>{pct(row.changePercent)}</td>
                      <td className="num hide-mobile">{pct(row.next1Change)}</td>
                      <td className="num hide-mobile">{pct(row.next5Change)}</td>
                      <td className="num hide-mobile">{pct(row.next10Change)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {/* 大宗交易 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">大宗交易</span>
          <span className="card-sub">最近 {data.blockTrades.length} 笔</span>
        </div>
        <div className="card-body is-flush">
          {data.blockTrades.length === 0 ? (
            <EmptyState title="近期无大宗交易" hint="该标的在采集区间内没有大宗交易记录。" />
          ) : (
            <div className="tbl-wrap">
              <table className="tbl is-comfort">
                <thead>
                  <tr>
                    <th>成交日</th>
                    <th className="num">成交价</th>
                    <th className="num">折溢价</th>
                    <th className="num">成交量</th>
                    <th className="num">成交额</th>
                    <th>买方</th>
                    <th>卖方</th>
                  </tr>
                </thead>
                <tbody>
                  {data.blockTrades.map((row, index) => (
                    <tr key={`${row.tradeDate}-${index}`}>
                      <td className="mono">{row.tradeDate}</td>
                      <td className="num mono">{num(row.dealPrice)}</td>
                      <td className={`num ${tone(row.premiumRatio)}`}>{pct(row.premiumRatio)}</td>
                      <td className="num">{wan(row.dealVolume)}</td>
                      <td className="num">{yi(row.dealAmount)}</td>
                      <td className="fs-11 t-2">{row.buyerName ?? '—'}</td>
                      <td className="fs-11 t-2">{row.sellerName ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      <FreshnessNote asOf={data.asOf} source="东方财富公开接口（资金流 / 龙虎榜 / 大宗交易 / 两融 / 陆股通）" />

      <div className="legend-block mt-4">
        <b>数据来源与口径</b>
        {data.notes.map((note) => (
          <div key={note}>{note}</div>
        ))}
      </div>
    </>
  );
}

function SummaryCard({ title, summary }: { title: string; summary: Capital['summary'] }) {
  return (
    <div className="card">
      <div className="card-body is-tight">
        <div className="kpi">
          <span className="kpi-label">{title}</span>
          <span className={`kpi-value ${(summary?.mainNet ?? 0) >= 0 ? 't-up' : 't-down'}`}>
            {summary ? `${summary.mainNet >= 0 ? '+' : ''}${summary.mainNet.toFixed(2)} 亿` : '—'}
          </span>
          <span className="kpi-delta t-3">
            {summary ? `流入 ${summary.inflowDays}/${summary.days} 天` : '尚无数据'}
          </span>
        </div>
      </div>
    </div>
  );
}

/** 主力净额（正负柱）+ 涨跌幅（折线，右轴）。 */
function MainNetChart({ data }: { data: Capital }) {
  const { ref } = useChart(
    combo,
    {
      labels: data.fundFlow.map((point) => point.date.slice(5)),
      bars: [{ name: '主力净额(亿)', data: data.fundFlow.map((point) => point.mainNet) }],
      lines: [
        {
          name: '涨跌幅(%)',
          data: data.fundFlow.map((point) => point.changePercent ?? 0),
          onRightAxis: true
        }
      ],
      unit: '',
      rightAxisName: '%',
      leftAxisName: '亿'
    },
    {}
  );

  return <div className="chart chart-lg" ref={ref} />;
}

/** 最近一日五档净额构成。 */
function LayersChart({ latest }: { latest: Capital['fundFlow'][number] }) {
  const slices = useMemo(
    () => [
      { name: '超大单', value: Math.abs(latest.superLargeNet), tone: latest.superLargeNet >= 0 ? ('up' as const) : ('down' as const) },
      { name: '大单', value: Math.abs(latest.largeNet), tone: latest.largeNet >= 0 ? ('up' as const) : ('down' as const) },
      { name: '中单', value: Math.abs(latest.mediumNet), tone: latest.mediumNet >= 0 ? ('up' as const) : ('down' as const) },
      { name: '小单', value: Math.abs(latest.smallNet), tone: latest.smallNet >= 0 ? ('up' as const) : ('down' as const) }
    ],
    [latest]
  );

  const { ref } = useChart(donut, slices, {
    center: ['50%', '46%'],
    radius: ['52%', '74%'],
    legendOrient: 'horizontal',
    centerValue: `${latest.mainNet >= 0 ? '+' : ''}${latest.mainNet.toFixed(2)}`,
    centerLabel: '主力净额(亿)'
  });

  return (
    <>
      <div className="chart chart-md" ref={ref} />
      <div className="chart-note">
        {latest.date} · 环形图按各档净额绝对值占比着色（红=净流入、绿=净流出）。
      </div>
    </>
  );
}

/** 两融余额趋势。 */
function MarginChart({ data }: { data: Capital }) {
  const { ref } = useChart(area, {
    labels: data.margins.map((row) => row.date.slice(5)),
    series: [
      { name: '融资余额(亿)', data: data.margins.map((row) => row.financeBalance ?? 0) },
      { name: '融券余额(亿)', data: data.margins.map((row) => row.loanBalance ?? 0) }
    ],
    unit: ' 亿'
  });

  return (
    <>
      <div className="chart chart-md" ref={ref} />
      <div className="chart-note">融券余额量级通常远小于融资余额，同图时以融资余额为主。</div>
    </>
  );
}

/* ------------------------------------------------------------------
   展示工具
   ------------------------------------------------------------------ */

function yi(value: number | null | undefined): string {
  return value === null || value === undefined
    ? '—'
    : `${value.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} 亿`;
}

function wan(value: number | null | undefined): string {
  return value === null || value === undefined
    ? '—'
    : `${value.toLocaleString('zh-CN', { maximumFractionDigits: 2 })} 万`;
}

function num(value: number | null | undefined): string {
  return value === null || value === undefined
    ? '—'
    : value.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function pct(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : `${value >= 0 ? '+' : ''}${value.toFixed(2)}%`;
}

function tone(value: number | null | undefined): string {
  if (value === null || value === undefined) return '';
  return value > 0 ? 'is-up' : value < 0 ? 'is-down' : 'is-flat';
}

function tagClass(text: string): string {
  if (text.includes('净流入') || text.includes('增持') || text.includes('流入）')) return 'tag-up';
  if (text.includes('净流出') || text.includes('减持')) return 'tag-down';
  return 'tag-outline';
}
