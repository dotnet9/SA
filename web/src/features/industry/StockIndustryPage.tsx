import { useMemo } from 'react';
import { Link, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { hbar, treemap, useChart } from '@/components/charts';
import { EmptyState, ErrorState, FreshnessNote } from '@/components/ui/States';
import { apiGet } from '@/lib/api';

/** 行业接口类型，对应后端 `SA.Contracts.Industry`。 */
export interface IndustryOverview {
  code: string | null;
  name: string;
  pct: number;
  flow: number;
  upCount: number;
  downCount: number;
  memberCount: number;
  medianPct: number | null;
  medianPe: number | null;
  leader: string | null;
  rank: number | null;
  totalIndustries: number;
}

export interface PeerRow {
  code: string;
  name: string;
  price: number;
  pct: number;
  turnover: number;
  peTtm: number | null;
  pb: number | null;
  cap: number;
  amount: number;
  board: string;
  isSt: boolean;
  isSelf: boolean;
}

export interface IndustryPosition {
  pctRank: number | null;
  pctTotal: number;
  capRank: number | null;
  capPercentile: number | null;
  peRank: number | null;
  pePercentile: number | null;
  peVsMedian: number | null;
  pctVsMedian: number | null;
}

export interface IndustryComparison {
  code: string;
  name: string;
  industry: string | null;
  asOf: string | null;
  overview: IndustryOverview | null;
  position: IndustryPosition;
  peers: PeerRow[];
  topIndustries: IndustryOverview[];
  insights: string[];
  notes: string[];
}

/** 取行业与同业对比。 */
export function fetchIndustry(code: string): Promise<IndustryComparison> {
  return apiGet<IndustryComparison>(`/api/stocks/${encodeURIComponent(code)}/industry`);
}

/**
 * 行业与同业对比。
 *
 * 结构与 `design/web/stock-industry.html` 对应：结论 → 行业概览与位置 → 行业排行榜（热力 + 条形）
 * → 同业对比表 → 口径说明。
 *
 * 本页不新增采集源：全部基于全市场快照与行业板块快照计算（同一时点、同一口径）。
 */
export function StockIndustryPage() {
  const { code = '' } = useParams();

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['stock', code, 'industry'],
    queryFn: () => fetchIndustry(code),
    enabled: code.length > 0
  });

  if (isPending) {
    return <div className="sa-boot">正在载入 {code} 的行业对比…</div>;
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

function Content({ data }: { data: IndustryComparison }) {
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

      {/* 行业概览 */}
      <div className="grid grid-4 mt-3">
        <div className="card">
          <div className="card-body is-tight">
            <div className="kpi">
              <span className="kpi-label">行业涨跌幅</span>
              <span className={`kpi-value ${(data.overview?.pct ?? 0) >= 0 ? 't-up' : 't-down'}`}>
                {data.overview ? pct(data.overview.pct) : '—'}
              </span>
              <span className="kpi-delta t-3">
                {data.overview?.rank ? `排名 ${data.overview.rank}/${data.overview.totalIndustries}` : '无行业数据'}
              </span>
            </div>
          </div>
        </div>

        <div className="card">
          <div className="card-body is-tight">
            <div className="kpi">
              <span className="kpi-label">行业主力净流入</span>
              <span className={`kpi-value is-sm ${(data.overview?.flow ?? 0) >= 0 ? 't-up' : 't-down'}`}>
                {data.overview ? yi(data.overview.flow) : '—'}
              </span>
              <span className="kpi-delta t-3">
                {data.overview ? `领涨 ${data.overview.leader ?? '—'}` : ''}
              </span>
            </div>
          </div>
        </div>

        <div className="card">
          <div className="card-body is-tight">
            <div className="kpi">
              <span className="kpi-label">行业内涨幅排名</span>
              <span className="kpi-value is-sm">
                {data.position.pctRank === null ? '—' : `${data.position.pctRank}/${data.position.pctTotal}`}
              </span>
              <span className="kpi-delta t-3">
                {data.position.pctVsMedian === null
                  ? '行业中位数不可用'
                  : `相对中位 ${sign(data.position.pctVsMedian)} pt`}
              </span>
            </div>
          </div>
        </div>

        <div className="card">
          <div className="card-body is-tight">
            <div className="kpi">
              <span className="kpi-label">估值对比（PE TTM）</span>
              <span className="kpi-value is-sm">
                {data.position.peVsMedian === null ? '—' : `${sign(data.position.peVsMedian)}%`}
              </span>
              <span className="kpi-delta t-3">
                {data.position.pePercentile === null
                  ? '该股 PE 不可用（可能为亏损）'
                  : `行业分位 ${data.position.pePercentile}%`}
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* 行业排行 */}
      <div className="grid grid-2 mt-4">
        <div className="card">
          <div className="card-head">
            <span className="card-title">行业涨跌幅排行</span>
            <span className="card-sub">前 10 名（当前行业以标签标出）</span>
          </div>
          <div className="card-body">
            {data.topIndustries.length === 0 ? (
              <EmptyState title="暂无行业数据" hint="行业板块快照尚未采集完成。" />
            ) : (
              <IndustryRankingChart data={data} />
            )}
          </div>
        </div>

        <div className="card">
          <div className="card-head">
            <span className="card-title">行业热力</span>
            <span className="card-sub">面积 = 涨跌幅强度 · 当前行业高亮</span>
          </div>
          <div className="card-body">
            {data.topIndustries.length === 0 ? (
              <EmptyState title="暂无行业数据" hint="行业板块快照尚未采集完成。" />
            ) : (
              <IndustryHeatChart data={data} />
            )}
          </div>
        </div>
      </div>

      {/* 同业对比 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">同业对比</span>
          <span className="card-sub">
            {data.industry ?? '未知行业'} · 共 {data.position.pctTotal} 家可比 · 按总市值倒序前 {data.peers.length} 家
          </span>
        </div>
        <div className="card-body is-flush">
          {data.peers.length === 0 ? (
            <EmptyState
              title="暂无同业数据"
              hint="该标的的行业信息缺失，或全市场快照尚未采集完成。"
            />
          ) : (
            <div className="tbl-wrap">
              <table className="tbl is-comfort">
                <thead>
                  <tr>
                    <th>名称 / 代码</th>
                    <th>板块</th>
                    <th className="num">现价</th>
                    <th className="num">涨跌幅</th>
                    <th className="num hide-mobile">换手率</th>
                    <th className="num hide-mobile">PE(TTM)</th>
                    <th className="num hide-mobile">PB</th>
                    <th className="num">总市值</th>
                    <th className="num hide-mobile">成交额</th>
                  </tr>
                </thead>
                <tbody>
                  {data.peers.map((peer) => (
                    <tr
                      key={peer.code}
                      data-chg={peer.pct >= 0 ? 'up' : 'down'}
                      className={peer.isSelf ? 'is-active' : undefined}
                    >
                      <td>
                        <Link className="stock-cell" to={`/stock/${peer.code}`}>
                          <span className="sc-name">
                            {peer.name}
                            {peer.isSt ? <span className="tag tag-danger" style={{ marginLeft: 6 }}>ST</span> : null}
                            {peer.isSelf ? <span className="tag tag-up" style={{ marginLeft: 6 }}>本股</span> : null}
                          </span>
                          <span className="sc-code">{peer.code}</span>
                        </Link>
                      </td>
                      <td className="fs-11 t-2">{peer.board}</td>
                      <td className="num mono">{num(peer.price)}</td>
                      <td className={`num ${peer.pct > 0 ? 'is-up' : peer.pct < 0 ? 'is-down' : 'is-flat'}`}>{pct(peer.pct)}</td>
                      <td className="num hide-mobile">{num(peer.turnover)}%</td>
                      <td className="num hide-mobile">{num(peer.peTtm)}</td>
                      <td className="num hide-mobile">{num(peer.pb)}</td>
                      <td className="num">{yi(peer.cap)}</td>
                      <td className="num hide-mobile">{yi(peer.amount)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      <FreshnessNote asOf={data.asOf} source="全市场快照 + 行业板块快照（本模块不新增采集源）" />

    </>
  );
}

/** 行业排行（横向条形，当前行业用标签提示）。 */
function IndustryRankingChart({ data }: { data: IndustryComparison }) {
  const rows = useMemo(() => {
    const top = data.topIndustries.map((industry) => ({
      name: industry.code === data.overview?.code ? `${industry.name}（本行业）` : industry.name,
      pct: industry.pct,
      flow: industry.flow,
      note: industry.rank ? `排名 ${industry.rank}/${industry.totalIndustries}` : undefined
    }));

    // 当前行业不在前 10 时补一行，否则用户看不到自己关注的行业
    if (data.overview && !data.topIndustries.some((industry) => industry.code === data.overview?.code)) {
      top.push({
        name: `${data.overview.name}（本行业）`,
        pct: data.overview.pct,
        flow: data.overview.flow,
        note: data.overview.rank ? `排名 ${data.overview.rank}/${data.overview.totalIndustries}` : undefined
      });
    }

    return top;
  }, [data]);

  const { ref } = useChart(hbar, rows, { unit: '%', valueKey: 'pct' });

  return <div className="chart chart-lg" ref={ref} />;
}

/** 行业热力（树图）。 */
function IndustryHeatChart({ data }: { data: IndustryComparison }) {
  const items = useMemo(
    () =>
      data.topIndustries.map((industry) => ({
        name: industry.name,
        pct: industry.pct,
        flow: industry.flow,
        leader: industry.leader ?? undefined,
        code: industry.code ?? undefined
      })),
    [data.topIndustries]
  );

  const { ref } = useChart(treemap, items);

  return (
    <>
      <div className="chart chart-lg" ref={ref} />
      <div className="chart-note">仅展示行业涨跌幅前 10 名：热力图的意义来自面积差异，行业过多会退化成同尺寸方块。</div>
    </>
  );
}

/* ------------------------------------------------------------------
   展示工具
   ------------------------------------------------------------------ */

function num(value: number | null | undefined): string {
  return value === null || value === undefined
    ? '—'
    : value.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function yi(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : `${num(value)} 亿`;
}

function pct(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : `${sign(value)}%`;
}

function sign(value: number): string {
  return `${value >= 0 ? '+' : ''}${value.toFixed(2)}`;
}

function tagClass(text: string): string {
  if (text.includes('强于') || text.includes('前 ')) return 'tag-up';
  if (text.includes('弱于')) return 'tag-down';
  return 'tag-outline';
}
