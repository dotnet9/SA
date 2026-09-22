import { useState } from 'react';
import { useParams } from 'react-router';
import { area, band, kline, useChart } from '@/components/charts';
import { EmptyState, ErrorState } from '@/components/ui/States';
import { useStockFreshness, useTrend } from './hooks';
import type { Trend } from './api';

/**
 * 趋势与价格结构。
 *
 * 结构与 `design/web/stock-trend.html` 对应：吸顶工具条 → K 线主图（含均线图例与副图）
 * → 位置与关键价位 → 相对强弱 → 布林带 → 口径说明。
 *
 * 指标全部来自服务端（不在此处重算），因此图上数值与总览卡、接口完全一致。
 */
export function StockTrendPage() {
  const { code = '' } = useParams();
  const [bollVisible, setBollVisible] = useState(false);
  const [subIndicator, setSubIndicator] = useState<'macd' | 'kdj'>('macd');
  const [limit, setLimit] = useState(120);

  const { data, error, isPending, refetch, failureCount } = useTrend(code, limit);
  const { data: freshness } = useStockFreshness(code);

  if (isPending) {
    return <div className="sa-boot">正在载入 {code} 的趋势数据…</div>;
  }

  if (!data) {
    return (
      <>
        <div className="card">
          <div className="card-body">
            <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />
          </div>
        </div>
        {freshness ? (
          <div className="legend-block mt-4">
            日线已入库至 {freshness.dailyLastDate ?? '（尚未回补）'} · 指标已入库至{' '}
            {freshness.indicatorLastDate ?? '（尚未计算）'}
            {freshness.collecting ? ' · 正在按需采集，页面会自动重试' : ''}
          </div>
        ) : null}
      </>
    );
  }

  return (
    <Content
      data={data}
      bollVisible={bollVisible}
      subIndicator={subIndicator}
      limit={limit}
      onToggleBoll={() => setBollVisible((current) => !current)}
      onChangeSub={(value) => setSubIndicator(value)}
      onChangeLimit={(value) => setLimit(value)}
    />
  );
}

function Content({
  data,
  bollVisible,
  subIndicator,
  limit,
  onToggleBoll,
  onChangeSub,
  onChangeLimit
}: {
  data: Trend;
  bollVisible: boolean;
  subIndicator: 'macd' | 'kdj';
  limit: number;
  onToggleBoll: () => void;
  onChangeSub: (value: 'macd' | 'kdj') => void;
  onChangeLimit: (value: number) => void;
}) {
  const { ref: klineRef } = useChart(kline, {
    bars: data.candles,
    ma: data.ma,
    macd: data.macd,
    kdj: data.kdj,
    boll: data.boll
  }, { volume: true, macd: subIndicator === 'macd', kdj: subIndicator === 'kdj', boll: bollVisible, windowSize: limit });

  const rsLabels = data.candles.map((candle) => candle.d.slice(5));
  const { ref: rsRef } = useChart(area, {
    labels: rsLabels.slice(-60),
    data: data.relativeStrength.line.slice(-60),
    unit: ' pt',
    name: `相对${data.relativeStrength.benchmark}`,
    startAtZero: true
  });

  const bollLabels = data.candles.map((candle) => candle.d.slice(5));
  const { ref: bandRef } = useChart(band, {
    labels: bollLabels,
    line: data.candles.map((candle) => candle.c),
    upper: data.boll.up.map((value) => value ?? Number.NaN),
    lower: data.boll.low.map((value) => value ?? Number.NaN),
    lineName: '收盘',
    bandName: '布林带(20,2)',
    unit: ''
  });

  // 趋势动能用 MACD 柱的最新值映射到 0–100，仅作为「动能强弱」的直观刻度
    return (
    <>

      {/* 吸顶工具条 */}
      <div className="trend-toolbar">
        <div className="row-between wrap gap-3">
          <span className="segmented">
            {[60, 120, 240].map((value) => (
              <span key={value} className={limit === value ? 'is-active' : undefined} onClick={() => onChangeLimit(value)}>
                {value === 240 ? '全部' : `${value} 根`}
              </span>
            ))}
          </span>
          <span className="row gap-2 wrap">
            <span className="segmented">
              <span className={subIndicator === 'macd' ? 'is-active' : undefined} onClick={() => onChangeSub('macd')}>
                MACD
              </span>
              <span className={subIndicator === 'kdj' ? 'is-active' : undefined} onClick={() => onChangeSub('kdj')}>
                KDJ
              </span>
            </span>
            <button
              type="button"
              className={`btn btn-sm ${bollVisible ? 'btn-primary' : 'btn-outline'}`}
              onClick={onToggleBoll}
            >
              布林带
            </button>
          </span>
        </div>
      </div>

      {/* 主图 */}
      <div className="card">
        <div className="card-head">
          <span className="card-title">K 线与均线</span>
          <span className="card-sub">
            指标由服务端计算 · 拖动可缩放（inside 模式）
          </span>
          <div className="card-tools">
            <span className="legend-inline">
              <span className="li">
                <span className="sw" style={{ background: 'var(--chart-1)' }} />
                MA5
              </span>
              <span className="li">
                <span className="sw" style={{ background: 'var(--chart-2)' }} />
                MA10
              </span>
              <span className="li">
                <span className="sw" style={{ background: 'var(--chart-3)' }} />
                MA20
              </span>
              <span className="li">
                <span className="sw" style={{ background: 'var(--chart-4)' }} />
                MA60
              </span>
            </span>
          </div>
        </div>
        <div className="card-body">
          <div className="chart chart-2xl" ref={klineRef} />
          <div className="chart-note">
            副图：{subIndicator === 'macd' ? 'MACD(12,26,9)' : 'KDJ(9,3,3)'}
            {bollVisible ? ' · 已叠加布林带(20,2)' : ''}；成交量按当日涨跌着色。
          </div>
        </div>
      </div>

      {/* 位置与关键价位 */}
      <div className="grid grid-3 mt-4">
        <div className="card">
          <div className="card-head">
            <span className="card-title">位置与分位</span>
            <span className="card-sub">样本不足显示「—」</span>
          </div>
          <div className="card-body col gap-2">
            <LevelRow label="250 日最高" value={price(data.levels.high250)} />
            <LevelRow label="250 日最低" value={price(data.levels.low250)} />
            <LevelRow label="距 20 日线" value={pct(data.levels.aboveMa20Pct)} toneBySign={data.levels.aboveMa20Pct} />
            <LevelRow label="距 250 日线" value={pct(data.levels.aboveMa250Pct)} toneBySign={data.levels.aboveMa250Pct} />
            <LevelRow label="250 日分位" value={data.levels.quantile250 === null ? '—' : `${data.levels.quantile250}%`} />
            <LevelRow label="3 年分位" value={data.levels.quantile3y === null ? '—' : `${data.levels.quantile3y}%`} />
            <div className="chart-note">
              分位样本 {data.levels.samples} 根
              {data.levels.quantile3y === null ? '；3 年分位需要约 500 个交易日样本，当前不足' : ''}
            </div>
          </div>
        </div>

        <div className="card">
          <div className="card-head">
            <span className="card-title">相对强弱</span>
            <span className="card-sub">对比{data.relativeStrength.benchmark}</span>
          </div>
          <div className="card-body">
            {data.relativeStrength.value === null ? (
              <EmptyState
                title="相对强弱暂不可用"
                hint={`基准指数（${data.relativeStrength.benchmark}）日线尚未回补，或个股日线样本不足。`}
              />
            ) : (
              <>
                <div className="kpi">
                  <span className="kpi-label">区间超额收益</span>
                  <span className={`kpi-value ${data.relativeStrength.value >= 0 ? 't-up' : 't-down'}`}>
                    {data.relativeStrength.value >= 0 ? '+' : ''}
                    {data.relativeStrength.value}%
                  </span>
                </div>
                <div className="chart chart-md mt-3" ref={rsRef} />
              </>
            )}
            <div className="chart-note">口径：个股累计收益 − 基准累计收益（同区间、逐日对齐）。</div>
          </div>
        </div>

              </div>

      {/* 布林带 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">价格与布林带</span>
          <span className="card-sub">BOLL(20,2) · 服务端计算</span>
        </div>
        <div className="card-body">
          <div className="chart chart-lg" ref={bandRef} />
        </div>
      </div>

      {/* 结论 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">趋势摘要</span>
          <span className="card-sub">全部由可复算规则给出</span>
        </div>
        <div className="card-body row gap-2 wrap">
          {data.insights.map((insight) => (
            <span key={insight.label + insight.text} className={`tag ${tagClass(insight.tone)}`}>
              {insight.label}：{insight.text}
            </span>
          ))}
        </div>
      </div>

    </>
  );
}

function LevelRow({ label, value, toneBySign }: { label: string; value: string; toneBySign?: number | null }) {
  const tone = toneBySign === undefined || toneBySign === null ? 't-2' : toneBySign >= 0 ? 't-up' : 't-down';

  return (
    <div className="row-between">
      <span className="fs-12 t-2">{label}</span>
      <span className={`mono fs-12 ${tone}`}>{value}</span>
    </div>
  );
}

function price(value: number | null): string {
  return value === null ? '—' : value.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function pct(value: number | null): string {
  return value === null ? '—' : `${value >= 0 ? '+' : ''}${value.toFixed(2)}%`;
}

function tagClass(tone: string): string {
  switch (tone) {
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
