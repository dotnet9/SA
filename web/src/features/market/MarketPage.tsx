import { useMemo } from 'react';
import { Link } from 'react-router';
import { donut, hbar, spark, treemap, useChart } from '@/components/charts';
import { EmptyState, ErrorState, FreshnessNote, SourceDot } from '@/components/ui/States';
import { useMarketOverview } from './hooks';
import type { MarketOverview, RankingRow } from './api';

/**
 * 市场概览。
 *
 * 结构与 `design/web/market.html` 逐块对应：指数卡 → 涨跌家数 / 北向 / 两市资金 → 行业热力与排行 → 榜单 → 口径说明。
 * 与本批后端能力不一致的部分（事件热点、导出、指数迷你走势）按「显式空态 + 说明」处理，
 * 不用假数据填充（实施计划 §10）。
 */

/** 数字格式化：千分位 + 指定小数位。 */
function fmt(value: number | null | undefined, digits = 2): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '—';
  }

  return value.toLocaleString('zh-CN', { minimumFractionDigits: digits, maximumFractionDigits: digits });
}

/** 带符号的百分比。 */
function signed(value: number | null | undefined, digits = 2): string {
  if (value === null || value === undefined) {
    return '—';
  }

  return `${value >= 0 ? '+' : ''}${fmt(value, digits)}%`;
}

/** 带符号的绝对数值（涨跌点数、金额），不带百分号。 */
function signedAmount(value: number | null | undefined, digits = 2): string {
  if (value === null || value === undefined) {
    return '—';
  }

  return `${value >= 0 ? '+' : ''}${fmt(value, digits)}`;
}

/** 涨跌方向对应的类名。 */
function tone(value: number | null | undefined): string {
  if (value === null || value === undefined) return 'is-flat';
  return value > 0 ? 'is-up' : value < 0 ? 'is-down' : 'is-flat';
}

export function MarketPage() {
  const { data, error, isPending, refetch, failureCount } = useMarketOverview();

  if (isPending) {
    return <div className="sa-boot">正在载入市场概览…</div>;
  }

  if (!data) {
    return (
      <>
        <div className="sa-pagehead">
          <div>
            <h1>市场概览</h1>
            <div className="sub">全市场行情、行业热力与榜单</div>
          </div>
        </div>
        <div className="card">
          <div className="card-body">
            <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />
          </div>
        </div>
      </>
    );
  }

  return <MarketContent data={data} onRefresh={() => void refetch()} />;
}

function MarketContent({ data, onRefresh }: { data: MarketOverview; onRefresh: () => void }) {
  const { breadth, fundFlow, industries, rankings, status } = data;

  const ratio = breadth.down > 0 ? breadth.up / breadth.down : null;

  return (
    <>
      <div className="sa-pagehead">
        <div>
          <h1>市场概览</h1>
          <div className="sub">
            {status.asOf ?? '—'} · {status.marketPhase} · 数据更新于 {status.updatedAt ?? '—'} · 全市场 {breadth.total} 只
          </div>
        </div>
        <div className="sa-pagehead-actions">
          <button type="button" className="btn btn-outline btn-sm" onClick={onRefresh}>
            刷新数据
          </button>
        </div>
      </div>

      {/* 数据源健康：任何一源降级都要在页面上可见（实施计划 §5.4） */}
      <div className="row gap-4" style={{ flexWrap: 'wrap' }}>
        {status.sources.map((source) => (
          <span key={source.name} className="row gap-1" style={{ alignItems: 'center' }}>
            <SourceDot status={source.status} />
            <span className="fs-11 t-3">
              {source.name}
              {source.latencyMs !== null ? ` · ${source.latencyMs}ms` : ''}
              {source.failCount > 0 ? ` · 连续失败 ${source.failCount} 次` : ''}
            </span>
          </span>
        ))}
      </div>

      {/* 指数卡片 */}
      <div className="grid grid-5">
        {data.indices.map((index) => (
          <IndexCardView key={index.code} index={index} />
        ))}
      </div>

      <div className="grid grid-3 mt-4">
        {/* 涨跌家数 */}
        <div className="card is-accent">
          <div className="card-head">
            <span className="card-title">
              涨跌家数
              <span
                className="infoq"
                data-tip="口径：全市场（含北交所）当日涨跌家数统计，仅统计有行情的标的；停牌与退市标的计入总数之外。"
              >
                ?
              </span>
            </span>
            <span className="card-sub">全市场 {breadth.total} 只</span>
          </div>
          <div className="card-body">
            <BreadthDonut up={breadth.up} down={breadth.down} flat={breadth.flat} />
            <div className="grid grid-3 mt-3">
              <div className="kpi">
                <span className="kpi-label">涨停</span>
                <span className="kpi-value t-up">{breadth.limitUp}</span>
                <span className="kpi-delta t-3">交易所口径</span>
              </div>
              <div className="kpi">
                <span className="kpi-label">跌停</span>
                <span className="kpi-value t-down">{breadth.limitDown}</span>
                <span className="kpi-delta t-3">交易所口径</span>
              </div>
              <div className="kpi">
                <span className="kpi-label">涨跌比</span>
                <span className={`kpi-value ${ratio !== null && ratio >= 1 ? 't-up' : 't-down'}`}>
                  {ratio === null ? '—' : fmt(ratio, 2)}
                </span>
                <span className="kpi-delta t-3">{ratio !== null && ratio >= 1 ? '多头占优' : '空头占优'}</span>
              </div>
            </div>
            <FreshnessNote asOf={breadth.asOf} source="全市场个股快照（服务端聚合）" />
          </div>
        </div>

        {/* 北向资金：本轮为显式空态（见北向说明） */}
        <div className="card">
          <div className="card-head">
            <span className="card-title">北向资金</span>
            <span className="card-sub">本批未接入</span>
          </div>
          <div className="card-body">
            <EmptyState title="暂无法提供北向净流入" hint={breadth.northboundNote} />
          </div>
        </div>

        {/* 两市资金与杠杆 */}
        <div className="card">
          <div className="card-head">
            <span className="card-title">两市资金与杠杆</span>
            <span className="card-sub">收盘快照</span>
          </div>
          <div className="card-body col gap-3">
            <div className="kpi">
              <span className="kpi-label">
                两市成交额
                <span className="infoq" data-tip="口径：全市场个股成交额合计，单位亿元。">
                  ?
                </span>
              </span>
              <span className="kpi-value is-xl">
                {fmt(breadth.turnover, 2)} <span className="fs-14 t-3">亿</span>
              </span>
              <span className="kpi-delta t-3">
                {breadth.turnoverPct === null ? '较上一交易日 —' : `较上一交易日 ${signed(breadth.turnoverPct)}`}
              </span>
            </div>
            <div className="row gap-3">
              <div className="kpi grow">
                <span className="kpi-label">融资余额</span>
                <span className="kpi-value is-sm">{fmt(breadth.marginBalance, 2)} 亿</span>
                <span className="kpi-delta t-3">{breadth.marginAsOf ? `截至 ${breadth.marginAsOf}` : '尚无数据'}</span>
              </div>
              <div className="kpi grow">
                <span className="kpi-label">融券余额</span>
                <span className="kpi-value is-sm">{fmt(fundFlow.loanBalance, 2)} 亿</span>
                <span className="kpi-delta t-3">{fundFlow.marginAsOf ? `截至 ${fundFlow.marginAsOf}` : '尚无数据'}</span>
              </div>
            </div>
            <div className="fold is-collapsed">
              <div className="fold-head">
                资金分层（两市主力，沪 + 深合计）<span className="caret">▼</span>
              </div>
              <div className="fold-body col gap-2">
                <FundFlowRow label="主力净额" value={fundFlow.layers.mainNet} strong />
                <FundFlowRow label="超大单净额" value={fundFlow.layers.superLarge} />
                <FundFlowRow label="大单净额" value={fundFlow.layers.large} />
                <FundFlowRow label="中单净额" value={fundFlow.layers.medium} />
                <FundFlowRow label="小单净额" value={fundFlow.layers.small} />
                <div className="fs-11 t-3">
                  口径：主力 = 大单 + 超大单；数据时间 {fundFlow.asOf || '—'}
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* 行业 */}
      <div className="grid grid-2 mt-4">
        <div className="card">
          <div className="card-head">
            <span className="card-title">行业热力</span>
            <span className="card-sub">
              涨跌幅前 {HeatmapSize} 个板块（共 {industries.length} 个）· 面积 = 涨跌幅强度 · 颜色 = 涨跌
            </span>
          </div>
          <div className="card-body">
            <IndustryHeatmap industries={industries} />
          </div>
        </div>

        <div className="card">
          <div className="card-head">
            <span className="card-title">行业涨跌排行</span>
            <span className="card-sub">含主力净流入</span>
          </div>
          <div className="card-body">
            <IndustryRanking industries={industries} />
          </div>
        </div>
      </div>

      {/* 榜单 */}
      <div className="grid grid-3 mt-4">
        <RankingTable title="成交额榜" sub="亿元" rows={rankings.amount} valueKey="amount" />
        <RankingTable title="涨幅榜" sub="剔除新股与 ST" rows={rankings.gainers} valueKey="pct" />
        <RankingTable title="跌幅榜" sub="剔除新股与 ST" rows={rankings.losers} valueKey="pct" />
      </div>

      <div className="legend-block mt-4">
        <b>数据来源与口径</b>
        <br />
        指数与行情：东方财富公开接口，交易时段按 {60} 秒扫描全市场（端点点单页上限 100 行，一轮约 60 次请求）；收盘后保留收盘快照。
        <br />
        涨跌家数与成交额：由全市场个股快照在服务端聚合；涨停/跌停取交易所专用口径，不用涨跌幅阈值反推。
        <br />
        榜单：涨跌幅榜剔除 ST（±5% 限制）与新股 / 次新股（名称前缀 N / C，涨跌幅不设限或远大于常态），
        否则极端值会长期霸榜；成交额榜不剔除。
        <br />
        资金流与两融：东方财富公开接口；两融按沪深两市均已披露的最近交易日取值，故日期可能早于行情一天。
        <br />
        行业分类：<b>东财行业</b>（需求规格中的申万一级无公开直取源，已登记为口径偏差）。
        行业热力图只画涨跌幅最大的前 {HeatmapSize} 个板块，全部约 500 个画在同一张树图里会失去面积差异的意义。
        <br />
        北向资金：公开接口已不再提供逐日净买入，本批不提供该数值，也不以估算值替代。
        <br />
        事件热点卡片随「事件与拓扑」模块（第 9 批）接入；本页不展示未接入的数据。
        <br />
        数据来源于公开接口，仅供研究参考，不构成投资建议。
      </div>
    </>
  );
}

/** 指数卡（含迷你走势）。 */
function IndexCardView({ index }: { index: MarketOverview['indices'][number] }) {
  const { ref } = useChart(spark, index.spark, { up: index.pct >= 0 });
  const cls = tone(index.pct);

  return (
    <div className="card is-accent">
      <div className="card-body is-tight">
        <div className="row-between">
          <span className="fs-13 fw-600">{index.name}</span>
          <span className="mono fs-11 t-3">{index.code}</span>
        </div>
        <div className="row-between mt-2" style={{ alignItems: 'flex-end' }}>
          <div>
            <div className={`mono fs-22 fw-700 ${cls}`}>{fmt(index.price)}</div>
            <div className={`chg ${cls} fs-12`}>
              {signedAmount(index.chg)} <span>{signed(index.pct)}</span>
            </div>
          </div>
          <div style={{ width: 96 }}>
            {index.spark.length >= 2 ? (
              <div className="chart chart-xs" ref={ref} />
            ) : (
              <span className="hint" title="指数日线随「趋势与价格结构」模块（第 3 批）接入">
                走势待接入
              </span>
            )}
          </div>
        </div>
        <div className="row-between mt-2">
          <span className="hint">成交 {fmt(index.amount, 2)} 亿</span>
          <span className="hint">{index.asOf}</span>
        </div>
      </div>
    </div>
  );
}

/** 涨跌家数环形图。 */
function BreadthDonut({ up, down, flat }: { up: number; down: number; flat: number }) {
  const data = useMemo(
    () => [
      { name: '上涨', value: up, tone: 'up' as const },
      { name: '下跌', value: down, tone: 'down' as const },
      { name: '平盘', value: flat, tone: 'flat' as const }
    ],
    [up, down, flat]
  );

  const total = up + down + flat;
  const { ref } = useChart(donut, data, {
    legendOrient: 'vertical',
    center: ['34%', '50%'],
    radius: ['50%', '72%'],
    centerLabel: '合计',
    centerValue: String(total)
  });

  return <div className="chart chart-md" ref={ref} />;
}

/** 资金分层行。 */
function FundFlowRow({ label, value, strong }: { label: string; value: number; strong?: boolean }) {
  return (
    <div className="row-between">
      <span className={`fs-12 ${strong ? 't-1 fw-600' : 't-2'}`}>{label}</span>
      <span className={`mono ${tone(value) === 'is-up' ? 't-up' : tone(value) === 'is-down' ? 't-down' : 't-3'}`}>
        {value >= 0 ? '+' : ''}
        {fmt(value, 2)} 亿
      </span>
    </div>
  );
}

/** 行业热力图展示的板块数上限。 */
const HeatmapSize = 60;

/**
 * 行业热力图。
 *
 * 东财行业板块共约 500 个，全部画出来会变成一墙大小相近的方块——
 * 而树图的信息量恰恰来自面积差异，此时「面积 = 涨跌幅强度」不再传达任何信息。
 * 因此只画涨跌幅最大的前 N 个（在卡片副标题里写明取法），保持与原型一致的编码。
 */
function IndustryHeatmap({ industries }: { industries: MarketOverview['industries'] }) {
  const data = useMemo(
    () =>
      [...industries]
        .sort((a, b) => Math.abs(b.pct) - Math.abs(a.pct))
        .slice(0, HeatmapSize)
        .map((sector) => ({
          name: sector.name,
          pct: sector.pct,
          flow: sector.flow,
          leader: sector.leader ?? undefined,
          code: sector.code
        })),
    [industries]
  );

  const { ref } = useChart(treemap, data);

  if (industries.length === 0) {
    return <EmptyState title="暂无行业数据" hint="行业板块快照尚未采集完成。" />;
  }

  return <div className="chart chart-lg" ref={ref} />;
}

/** 行业涨跌排行。 */
function IndustryRanking({ industries }: { industries: MarketOverview['industries'] }) {
  // 原型按涨跌幅排序取前 12，并在提示里带出主力净流入
  const data = useMemo(
    () =>
      [...industries]
        .sort((a, b) => b.pct - a.pct)
        .slice(0, 12)
        .map((sector) => ({
          name: sector.name,
          pct: sector.pct,
          flow: sector.flow,
          note: sector.leader ? `领涨：${sector.leader}` : undefined
        })),
    [industries]
  );

  const { ref } = useChart(hbar, data);

  if (data.length === 0) {
    return <EmptyState title="暂无行业数据" hint="行业板块快照尚未采集完成。" />;
  }

  return <div className="chart chart-lg" ref={ref} />;
}

/** 榜单表格。 */
function RankingTable({
  title,
  sub,
  rows,
  valueKey
}: {
  title: string;
  sub: string;
  rows: RankingRow[];
  valueKey: 'amount' | 'pct';
}) {
  return (
    <div className="card">
      <div className="card-head">
        <span className="card-title">{title}</span>
        <span className="card-sub">{sub}</span>
      </div>
      <div className="card-body is-flush">
        {rows.length === 0 ? (
          <EmptyState title="暂无榜单数据" hint="全市场快照尚未采集完成。" />
        ) : (
          <div className="tbl-wrap">
            <table className="tbl">
              <thead>
                <tr>
                  <th>名称</th>
                  <th className="num">{valueKey === 'amount' ? '成交额' : '涨跌幅'}</th>
                  {/* 涨幅/跌幅榜的第二列已经是涨跌幅，再加一列会重复 */}
                  {valueKey === 'amount' ? <th className="num">涨跌幅</th> : null}
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.code} data-chg={row.pct >= 0 ? 'up' : 'down'} className="is-clickable">
                    <td>
                      <Link className="stock-cell" to={`/stock/${row.code}`}>
                        <span className="sc-name">
                          {row.name}
                          {row.isSt ? <span className="tag tag-danger" style={{ marginLeft: 6 }}>ST</span> : null}
                        </span>
                        <span className="sc-code">{row.code}</span>
                      </Link>
                    </td>
                    <td className={`num ${valueKey === 'pct' ? tone(row.pct) : ''}`}>
                      {valueKey === 'amount' ? `${fmt(row.amount, 2)} 亿` : signed(row.pct)}
                    </td>
                    {valueKey === 'amount' ? (
                      <td className={`num ${tone(row.pct)}`}>{signed(row.pct)}</td>
                    ) : null}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
