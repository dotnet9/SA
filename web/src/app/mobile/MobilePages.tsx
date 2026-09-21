import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { EmptyState, ErrorState, SourceDot } from '@/components/ui/States';
import { errorText } from '@/lib/errorText';
import { addWatchItems, fetchWatchlist, removeWatchItems } from '@/features/watchlist/api';
import { useMarketOverview } from '@/features/market/hooks';
import type { MarketOverview } from '@/features/market/api';
import {
  fetchAlertRules,
  fetchNotifications,
  markNotificationsRead,
  createAlertRule,
  removeAlertRule
} from '@/features/alerts/api';
import { fetchScreenerMeta, runScreener, type ScreenerResult } from '@/features/screener/api';
import { searchStocks, type SearchRow } from '@/features/search/api';
import { useAuth } from '@/providers/AuthProvider';
import { useRealtime } from '@/providers/RealtimeProvider';
import { useToast } from '@/providers/ToastProvider';
import { useTheme } from '@/providers/ThemeProvider';
import { MobilePageHead } from './MobileShell';

/**
 * 移动端页面。
 *
 * 与桌面页面的关系：<b>同一套数据、同一套口径</b>，只是排布不同——
 * 手机上不是把桌面表格缩小，而是换成「卡片 + 关键数字」的形态，
 * 表格仅在必须逐行比较时保留（并且横向可滚动）。
 *
 * 个股模块页（趋势 / 财务 / 股权 / 资金 / 行业 / 事件 / 因果 / 风险 / 评级）在移动端
 * 直接复用桌面组件并套在移动壳里：它们本身就是卡片式布局，重新做一套只会产生两份要同步维护的代码。
 */

/* ------------------------------------------------------------------
   首页：市场概览
   ------------------------------------------------------------------ */

export function MobileHomePage() {
  const { data, error, isPending, refetch, failureCount } = useMarketOverview();

  if (isPending) {
    return <div className="sa-boot">正在载入市场概览…</div>;
  }

  if (!data) {
    return (
      <>
        <MobilePageHead title="市场概览" sub="载入失败" />
        <div className="card">
          <div className="card-body">
            <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />
          </div>
        </div>
      </>
    );
  }

  return (
    <>
      <MobilePageHead
        title="市场概览"
        sub={`${data.status.asOf ?? '—'} · ${data.status.marketPhase} · 更新 ${data.status.updatedAt ?? '—'}`}
      />

      {/* 指数：两列卡片，只显示名称/点位/涨跌幅，手机上一眼够用 */}
      <div className="m-index-grid">
        {data.indices.map((index) => (
          <div key={index.code} className="m-index-card">
            <div className="m-index-name">{index.name}</div>
            <div className={`m-index-value ${index.pct >= 0 ? 't-up' : 't-down'}`}>{fmt(index.price)}</div>
            <div className={`fs-11 ${index.pct >= 0 ? 't-up' : 't-down'}`}>
              {signed(index.pct)}%
            </div>
            <div className="fs-10 t-3" style={{ marginTop: 4 }}>
              成交 {fmt(index.amount)} 亿
            </div>
          </div>
        ))}
      </div>

      {/* 涨跌家数 */}
      <div className="card mt-3">
        <div className="card-head">
          <span className="card-title">涨跌家数</span>
          <span className="card-sub">全市场 {data.breadth.total} 只</span>
        </div>
        <div className="card-body">
          <div className="m-kpi-row">
            <span className="m-kpi-label">上涨 / 下跌 / 平盘</span>
            <span className="m-kpi-value">
              <span className="t-up">{data.breadth.up}</span> / <span className="t-down">{data.breadth.down}</span> /{' '}
              {data.breadth.flat}
            </span>
          </div>
          <div className="m-kpi-row">
            <span className="m-kpi-label">涨停 / 跌停</span>
            <span className="m-kpi-value">
              <span className="t-up">{data.breadth.limitUp}</span> / <span className="t-down">{data.breadth.limitDown}</span>
            </span>
          </div>
          <div className="m-kpi-row">
            <span className="m-kpi-label">两市成交额</span>
            <span className="m-kpi-value">{fmt(data.breadth.turnover)} 亿</span>
          </div>
          <div className="m-kpi-row">
            <span className="m-kpi-label">融资余额（截至 {data.breadth.marginAsOf ?? '—'}）</span>
            <span className="m-kpi-value">{fmt(data.breadth.marginBalance)} 亿</span>
          </div>
        </div>
      </div>

      {/* 行业热力：手机上看前几名即可 */}
      <div className="card mt-3">
        <div className="card-head">
          <span className="card-title">行业涨跌</span>
          <Link className="fs-11 t-brand" to="/prosperity">
            景气度 →
          </Link>
        </div>
        <div className="card-body col gap-2">
          {[...data.industries]
            .sort((a, b) => b.pct - a.pct)
            .slice(0, 6)
            .map((sector) => (
              <div key={sector.code} className="m-kpi-row">
                <span className="m-kpi-label">{sector.name}</span>
                <span className={`m-kpi-value ${sector.pct >= 0 ? 't-up' : 't-down'}`}>
                  {signed(sector.pct)}%
                </span>
              </div>
            ))}
        </div>
      </div>

      {/* 榜单：手机上一列三张，各取前 5 */}
      <MobileRanking title="成交额榜" rows={data.rankings.amount.slice(0, 5)} valueKey="amount" />
      <MobileRanking title="涨幅榜" rows={data.rankings.gainers.slice(0, 5)} valueKey="pct" />
      <MobileRanking title="跌幅榜" rows={data.rankings.losers.slice(0, 5)} valueKey="pct" />

      {/* 数据源健康：手机上也保留，降级时必须能看到 */}
      <div className="card mt-3">
        <div className="card-head">
          <span className="card-title">数据源</span>
          <span className="card-sub">{data.status.sources.length} 个</span>
        </div>
        <div className="card-body col gap-2">
          {data.status.sources.map((source) => (
            <div key={source.name} className="row-between">
              <span className="row gap-1" style={{ alignItems: 'center' }}>
                <SourceDot status={source.status as 'ok' | 'warn' | 'err' | 'idle'} />
                <span className="fs-11 t-2">{source.name}</span>
              </span>
              <span className="fs-10 t-3">
                {source.latencyMs === null ? '' : `${source.latencyMs}ms`}
                {source.failCount > 0 ? ` · 失败 ${source.failCount}` : ''}
              </span>
            </div>
          ))}
        </div>
      </div>

      <div className="legend-block mt-3">
        <b>口径</b>
        <br />
        指数与行情来自东方财富公开接口；涨跌家数与成交额由全市场个股快照在服务端聚合；
        涨停 / 跌停取交易所专用口径（不用涨跌幅阈值反推）。
        <br />
        北向资金公开接口已不再提供逐日净买入，本页不提供该数值，也不以估算替代。
      </div>
    </>
  );
}

function MobileRanking({
  title,
  rows,
  valueKey
}: {
  title: string;
  rows: MarketOverview['rankings']['amount'];
  valueKey: 'amount' | 'pct';
}) {
  return (
    <div className="card mt-3">
      <div className="card-head">
        <span className="card-title">{title}</span>
        <span className="card-sub">前 {rows.length} 名</span>
      </div>
      <div className="card-body col gap-2">
        {rows.map((row) => (
          <Link key={row.code} className="m-kpi-row" to={`/m/stock/${row.code}`} style={{ textDecoration: 'none' }}>
            <span className="m-kpi-label">
              {row.name}
              <span className="mono fs-10 t-3" style={{ marginLeft: 6 }}>
                {row.code}
              </span>
            </span>
            <span className={`m-kpi-value ${row.pct >= 0 ? 't-up' : 't-down'}`}>
              {valueKey === 'amount' ? `${fmt(row.amount)} 亿` : `${signed(row.pct)}%`}
            </span>
          </Link>
        ))}
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------
   自选
   ------------------------------------------------------------------ */

export function MobileWatchlistPage() {
  const queryClient = useQueryClient();
  const toast = useToast();
  const realtime = useRealtime();

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['watchlist'],
    queryFn: fetchWatchlist
  });

  const removeMutation = useMutation({
    mutationFn: (codes: string[]) => removeWatchItems(codes),
    onSuccess: async () => {
      toast.toast('已移出自选', 'ok');
      await queryClient.invalidateQueries({ queryKey: ['watchlist'] });
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  if (isPending) {
    return <div className="sa-boot">正在载入自选股…</div>;
  }

  if (!data) {
    return (
      <>
        <MobilePageHead title="自选股" />
        <div className="card">
          <div className="card-body">
            <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />
          </div>
        </div>
      </>
    );
  }

  return (
    <>
      <MobilePageHead
        title="自选股"
        sub={`${data.items.length} / ${data.quota} 只 · ${
          realtime.status === 'connected' ? `实时已连接（${realtime.intervalSeconds}s）` : '实时未连接'
        }`}
        actions={
          <span className="segmented">
            {[3, 5, 10].map((seconds) => (
              <span
                key={seconds}
                className={realtime.intervalSeconds === seconds ? 'is-active' : undefined}
                onClick={() => realtime.setIntervalSeconds(seconds)}
              >
                {seconds}s
              </span>
            ))}
          </span>
        }
      />

      {data.items.length === 0 ? (
        <div className="card">
          <div className="card-body">
            <EmptyState
              title="还没有自选股"
              hint="在搜索页把标的加入自选后，这里会实时刷新行情。"
              action={
                <Link className="btn btn-sm btn-primary" to="/m/search">
                  去搜索
                </Link>
              }
            />
          </div>
        </div>
      ) : (
        <div className="col gap-2">
          {data.items.map((item) => {
            // 推送值优先，未收到推送时回落到快照值，并标出数据时间
            const live = realtime.quotes[item.code];
            const price = live?.price ?? item.price;
            const pct = live?.pct ?? item.pct;

            return (
              <div key={item.code} className="card">
                <div className="card-body is-tight" style={{ padding: 12 }}>
                  <div className="row-between">
                    <Link className="stock-cell" to={`/m/stock/${item.code}`}>
                      <span className="sc-name">
                        {item.name}
                        {item.isSt ? <span className="tag tag-danger" style={{ marginLeft: 6 }}>ST</span> : null}
                        {live ? <span className="tag tag-up" style={{ marginLeft: 6 }}>实时</span> : null}
                      </span>
                      <span className="sc-code">{item.code}</span>
                    </Link>
                    <span className="col" style={{ alignItems: 'flex-end' }}>
                      <span className={`mono fs-15 fw-700 ${tone(pct)}`}>{fmt(price)}</span>
                      <span className={`fs-11 ${tone(pct)}`}>{signed(pct)}%</span>
                    </span>
                  </div>

                  <div className="row-between mt-2">
                    <span className="fs-10 t-3">
                      {item.industry ?? '—'} · 换手 {item.turnover === null ? '—' : `${fmt(item.turnover)}%`} · 量比{' '}
                      {fmt(item.volRatio)}
                    </span>
                    <span className="row gap-2">
                      <Link className="btn btn-sm btn-ghost" to={`/m/stock/${item.code}/trend`}>
                        趋势
                      </Link>
                      {data.canEdit ? (
                        <button
                          type="button"
                          className="btn btn-sm btn-ghost"
                          disabled={removeMutation.isPending}
                          onClick={() => removeMutation.mutate([item.code])}
                        >
                          移出
                        </button>
                      ) : null}
                    </span>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      <div className="legend-block mt-3">
        <b>口径</b>
        <br />
        实时行情经 SignalR 推送（仅自选股），间隔可选 3 / 5 / 10 秒；未收到推送时显示最近一次快照值并标注时间。
        <br />
        非交易时段不轮询：表格显示最近一个交易日的收盘快照。
      </div>
    </>
  );
}

/* ------------------------------------------------------------------
   搜索
   ------------------------------------------------------------------ */

export function MobileSearchPage() {
  const [params, setParams] = useSearchParams();
  const query = params.get('q') ?? '';
  const [keyword, setKeyword] = useState(query);

  const { data, isPending, error } = useQuery({
    queryKey: ['search', query],
    queryFn: () => searchStocks({ q: query, pageSize: 30 }),
    enabled: query.trim().length > 0
  });

  const queryClient = useQueryClient();
  const toast = useToast();
  const { can } = useAuth();
  const canEdit = can('watchlist.edit');

  const addMutation = useMutation({
    mutationFn: (code: string) => addWatchItems([code]),
    onSuccess: async () => {
      toast.toast('已加入自选', 'ok');
      await queryClient.invalidateQueries({ queryKey: ['watchlist'] });
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  return (
    <>
      <MobilePageHead title="搜索" sub="代码 / 名称 / 拼音首字母 / 行业关键词" />

      <div className="row gap-2">
        <input
          className="input"
          style={{ flex: 1 }}
          placeholder="如 300750 / 宁德 / ndsd / 电池"
          value={keyword}
          onChange={(event) => setKeyword(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              setParams(keyword.trim() ? { q: keyword.trim() } : {});
            }
          }}
        />
        <button
          type="button"
          className="btn btn-primary"
          onClick={() => setParams(keyword.trim() ? { q: keyword.trim() } : {})}
        >
          搜索
        </button>
      </div>

      {!query ? (
        <div className="card mt-3">
          <div className="card-body">
            <EmptyState title="输入关键词开始搜索" hint="支持 6 位代码、中文名称、拼音首字母（ndsd）与行业关键词（电池）。" />
          </div>
        </div>
      ) : isPending ? (
        <div className="sa-boot" style={{ height: 160 }}>
          正在搜索…
        </div>
      ) : error ? (
        <div className="card mt-3">
          <div className="card-body">
            <ErrorState error={error} />
          </div>
        </div>
      ) : (data?.rows.length ?? 0) === 0 ? (
        <div className="card mt-3">
          <div className="card-body">
            <EmptyState title="没有匹配的股票" hint="换个代码、名称片段或行业关键词再试。" />
          </div>
        </div>
      ) : (
        <div className="col gap-2 mt-3">
          {data!.rows.map((row: SearchRow) => (
            <div key={row.code} className="card">
              <div className="card-body is-tight" style={{ padding: 12 }}>
                <div className="row-between">
                  <Link className="stock-cell" to={`/m/stock/${row.code}`}>
                    <span className="sc-name">
                      {row.name}
                      {row.isSt ? <span className="tag tag-danger" style={{ marginLeft: 6 }}>ST</span> : null}
                    </span>
                    <span className="sc-code">
                      {row.code} · {row.industry ?? '—'}
                    </span>
                  </Link>
                  <span className="col" style={{ alignItems: 'flex-end' }}>
                    <span className={`mono fs-14 fw-700 ${tone(row.pct)}`}>{fmt(row.price)}</span>
                    <span className={`fs-11 ${tone(row.pct)}`}>{signed(row.pct)}%</span>
                  </span>
                </div>
                <div className="row-between mt-2">
                  <span className="fs-10 t-3">
                    {row.board} · 市值 {row.cap === null ? '—' : `${fmt(row.cap)} 亿`} · PE{' '}
                    {row.pe === null ? '—' : fmt(row.pe)}
                  </span>
                  {canEdit ? (
                    <button
                      type="button"
                      className="btn btn-sm btn-outline"
                      disabled={addMutation.isPending}
                      onClick={() => addMutation.mutate(row.code)}
                    >
                      加自选
                    </button>
                  ) : null}
                </div>
              </div>
            </div>
          ))}
          <div className="fs-11 t-3">共命中 {data!.total} 只 · 已显示前 {data!.rows.length} 只</div>
        </div>
      )}
    </>
  );
}

/* ------------------------------------------------------------------
   提醒规则
   ------------------------------------------------------------------ */

export function MobileAlertsPage() {
  const queryClient = useQueryClient();
  const toast = useToast();

  const [code, setCode] = useState('');
  const [ruleType, setRuleType] = useState('price.above');
  const [threshold, setThreshold] = useState('');

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['alerts'],
    queryFn: fetchAlertRules
  });

  const createMutation = useMutation({
    mutationFn: () =>
      createAlertRule(code.trim(), ruleType, threshold.trim() === '' ? null : Number(threshold)),
    onSuccess: async () => {
      toast.toast('规则已创建', 'ok');
      setCode('');
      setThreshold('');
      await queryClient.invalidateQueries({ queryKey: ['alerts'] });
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const removeMutation = useMutation({
    mutationFn: (id: string) => removeAlertRule(id),
    onSuccess: async () => {
      toast.toast('规则已删除', 'ok');
      await queryClient.invalidateQueries({ queryKey: ['alerts'] });
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  if (isPending) {
    return <div className="sa-boot">正在载入提醒规则…</div>;
  }

  if (!data) {
    return (
      <>
        <MobilePageHead title="提醒规则" />
        <div className="card">
          <div className="card-body">
            <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />
          </div>
        </div>
      </>
    );
  }

  const types = data.types;

  return (
    <>
      <MobilePageHead title="提醒规则" sub={`${data.total} / ${data.quota} 条 · 触发后写入通知中心`} />

      <div className="card">
        <div className="card-head">
          <span className="card-title">新建规则</span>
        </div>
        <div className="card-body col gap-2">
          <input
            className="input input-sm"
            placeholder="证券代码（如 300750）"
            value={code}
            onChange={(event) => setCode(event.target.value)}
          />
          <select className="input input-sm" value={ruleType} onChange={(event) => setRuleType(event.target.value)}>
            {types.map((option) => (
              <option key={option.type} value={option.type}>
                {option.name}
                {option.unit ? `（${option.unit}）` : ''}
              </option>
            ))}
          </select>
          <input
            className="input input-sm"
            placeholder="阈值（单位见类型）"
            value={threshold}
            onChange={(event) => setThreshold(event.target.value)}
          />
          <button
            type="button"
            className="btn btn-primary"
            disabled={createMutation.isPending || code.trim().length === 0}
            onClick={() => createMutation.mutate()}
          >
            创建
          </button>
        </div>
      </div>

      <div className="col gap-2 mt-3">
        {data.rules.length === 0 ? (
          <div className="card">
            <div className="card-body">
              <EmptyState title="还没有规则" hint="在上方新建一条；触发后会在通知中心留下记录。" />
            </div>
          </div>
        ) : (
          data.rules.map((rule) => (
            <div key={rule.id} className="card">
              <div className="card-body is-tight" style={{ padding: 12 }}>
                <div className="row-between">
                  <Link className="stock-cell" to={`/m/stock/${rule.code}`}>
                    <span className="sc-name">{rule.name}</span>
                    <span className="sc-code">{rule.code}</span>
                  </Link>
                  <span className={`tag ${rule.enabled ? 'tag-up' : 'tag-outline'}`}>
                    {rule.enabled ? '启用中' : '已停用'}
                  </span>
                </div>
                <div className="m-kpi-row mt-2">
                  <span className="m-kpi-label">{rule.ruleTypeName}</span>
                  <span className="m-kpi-value">
                    {rule.threshold === null ? '—' : `${rule.threshold}${rule.thresholdUnit ?? ''}`}
                  </span>
                </div>
                <div className="row-between mt-2">
                  <span className="fs-10 t-3">
                    触发 {rule.triggerCount} 次 · 最近 {rule.lastTriggeredAt ?? '未触发'}
                  </span>
                  <button
                    type="button"
                    className="btn btn-sm btn-ghost"
                    disabled={removeMutation.isPending}
                    onClick={() => removeMutation.mutate(rule.id)}
                  >
                    删除
                  </button>
                </div>
              </div>
            </div>
          ))
        )}
      </div>

      <div className="legend-block mt-3">
        <b>口径</b>
        <br />
        评估每 30 秒一轮；同一规则触发后冷却 30 分钟，避免阈值附近震荡时刷屏。
        <br />
        一轮内同一用户的多条提醒会合并成一条通知（正文逐条列出），级别取其中最强的一条。
        <br />
        免打扰时段只写站内通知、不发起浏览器推送；免打扰在服务端执行（见「我的 → 通知偏好」）。
      </div>
    </>
  );
}

/* ------------------------------------------------------------------
   通知中心
   ------------------------------------------------------------------ */

export function MobileNotificationsPage() {
  const [unreadOnly, setUnreadOnly] = useState(false);
  const queryClient = useQueryClient();
  const toast = useToast();

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['notifications', unreadOnly],
    queryFn: () => fetchNotifications(unreadOnly)
  });

  const readMutation = useMutation({
    mutationFn: (ids: number[]) => markNotificationsRead(ids),
    onSuccess: async (count) => {
      toast.toast(`已标记 ${count} 条为已读`, 'ok');
      await queryClient.invalidateQueries({ queryKey: ['notifications'] });
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  if (isPending) {
    return <div className="sa-boot">正在载入通知…</div>;
  }

  if (!data) {
    return (
      <>
        <MobilePageHead title="通知中心" />
        <div className="card">
          <div className="card-body">
            <ErrorState error={error} onRetry={() => void refetch()} retryCount={failureCount} />
          </div>
        </div>
      </>
    );
  }

  return (
    <>
      <MobilePageHead
        title="通知中心"
        sub={data.unread > 0 ? `${data.unread} 条未读` : '没有未读通知'}
        actions={
          <button
            type="button"
            className="btn btn-sm btn-outline"
            disabled={data.unread === 0 || readMutation.isPending}
            onClick={() => readMutation.mutate([])}
          >
            全部已读
          </button>
        }
      />

      <div className="segmented">
        <span className={!unreadOnly ? 'is-active' : undefined} onClick={() => setUnreadOnly(false)}>
          全部 {data.items.length}
        </span>
        <span className={unreadOnly ? 'is-active' : undefined} onClick={() => setUnreadOnly(true)}>
          未读 {data.unread}
        </span>
      </div>

      <div className="col gap-2 mt-3">
        {data.items.length === 0 ? (
          <div className="card">
            <div className="card-body">
              <EmptyState title={unreadOnly ? '没有未读通知' : '还没有通知'} hint="提醒规则触发后会在这里留下记录。" />
            </div>
          </div>
        ) : (
          data.items.map((item) => (
            <div key={item.id} className="card">
              <div className="card-body is-tight" style={{ padding: 12 }}>
                <div className="row-between wrap gap-2">
                  <span className="row gap-2" style={{ alignItems: 'center' }}>
                    <span className={`tag ${levelClass(item.level)}`}>
                      {item.level === 'up' ? '利多' : item.level === 'down' ? '利空' : item.level === 'warn' ? '提示' : '信息'}
                    </span>
                    <span className={`fs-12 fw-600 ${item.isRead ? 't-2' : ''}`}>{item.title}</span>
                  </span>
                  <span className="fs-10 t-3">{item.createdAt}</span>
                </div>
                <div className="fs-11 t-2 mt-2" style={{ whiteSpace: 'pre-line' }}>
                  {item.body}
                </div>
                <div className="row gap-2 mt-2">
                  {item.code ? (
                    <Link className="btn btn-sm btn-ghost" to={`/m/stock/${item.code}`}>
                      查看标的
                    </Link>
                  ) : null}
                  {!item.isRead ? (
                    <button
                      type="button"
                      className="btn btn-sm btn-ghost"
                      disabled={readMutation.isPending}
                      onClick={() => readMutation.mutate([item.id])}
                    >
                      标记已读
                    </button>
                  ) : null}
                </div>
              </div>
            </div>
          ))
        )}
      </div>
    </>
  );
}

/* ------------------------------------------------------------------
   选股器（移动端：条件精简为最常用的三个区间 + 预设）
   ------------------------------------------------------------------ */

export function MobileScreenerPage() {
  const toast = useToast();
  const [preset, setPreset] = useState<string | null>(null);
  const [pctMin, setPctMin] = useState('');
  const [capMin, setCapMin] = useState('');
  const [peMax, setPeMax] = useState('');
  const [result, setResult] = useState<ScreenerResult | null>(null);

  const { data: meta } = useQuery({ queryKey: ['screener', 'meta'], queryFn: fetchScreenerMeta });

  const runMutation = useMutation({
    mutationFn: () =>
      runScreener({
        ranges: [
          ...(pctMin.trim() === '' ? [] : [{ field: 'pct', min: Number(pctMin), max: null }]),
          ...(capMin.trim() === '' ? [] : [{ field: 'cap', min: Number(capMin), max: null }]),
          ...(peMax.trim() === '' ? [] : [{ field: 'peTtm', min: 0, max: Number(peMax) }])
        ],
        enums: [],
        flags: [{ field: 'isSt', value: false }],
        sortBy: 'amount',
        sortDesc: true,
        page: 1,
        pageSize: 30,
        preset
      }),
    onSuccess: (data) => setResult(data),
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  return (
    <>
      <MobilePageHead title="条件选股器" sub="横截面筛选 · 同一时点全市场" />

      <div className="card">
        <div className="card-head">
          <span className="card-title">预设</span>
          <span className="card-sub">选中后忽略下方条件</span>
        </div>
        <div className="card-body row gap-2 wrap">
          <span
            className={`tag ${preset === null ? 'tag-up' : 'tag-outline'}`}
            onClick={() => setPreset(null)}
            style={{ cursor: 'pointer' }}
          >
            自定义
          </span>
          {(meta?.presets ?? []).map((item) => (
            <span
              key={item.key}
              className={`tag ${preset === item.key ? 'tag-up' : 'tag-outline'}`}
              onClick={() => setPreset(item.key)}
              style={{ cursor: 'pointer' }}
              title={item.description}
            >
              {item.name}
            </span>
          ))}
        </div>
      </div>

      <div className="card mt-3">
        <div className="card-head">
          <span className="card-title">条件</span>
          <span className="card-sub">留空表示不限</span>
        </div>
        <div className="card-body col gap-2">
          <input
            className="input input-sm"
            placeholder="涨幅下限（%）"
            value={pctMin}
            disabled={preset !== null}
            onChange={(event) => setPctMin(event.target.value)}
          />
          <input
            className="input input-sm"
            placeholder="总市值下限（亿元）"
            value={capMin}
            disabled={preset !== null}
            onChange={(event) => setCapMin(event.target.value)}
          />
          <input
            className="input input-sm"
            placeholder="PE(TTM) 上限"
            value={peMax}
            disabled={preset !== null}
            onChange={(event) => setPeMax(event.target.value)}
          />
          <button
            type="button"
            className="btn btn-primary"
            disabled={runMutation.isPending}
            onClick={() => runMutation.mutate()}
          >
            {runMutation.isPending ? '筛选中…' : '开始筛选'}
          </button>
        </div>
      </div>

      {result ? (
        <div className="card mt-3">
          <div className="card-head">
            <span className="card-title">结果</span>
            <span className="card-sub">
              命中 {result.total} 只 · 显示前 {result.rows.length}
            </span>
          </div>
          <div className="card-body col gap-2">
            <div className="chart-note">
              {result.applied.map((item) => (
                <div key={item}>· {item}</div>
              ))}
            </div>
            {result.rows.map((row) => (
              <Link key={row.code} className="m-kpi-row" to={`/m/stock/${row.code}`} style={{ textDecoration: 'none' }}>
                <span className="m-kpi-label">
                  {row.name}
                  <span className="mono fs-10 t-3" style={{ marginLeft: 6 }}>
                    {row.code}
                  </span>
                </span>
                <span className={`m-kpi-value ${row.pct >= 0 ? 't-up' : 't-down'}`}>
                  {fmt(row.price)} · {signed(row.pct)}%
                </span>
              </Link>
            ))}
            <span className="fs-11 t-3">
              导出建议在桌面版完成（CSV 受 export.data 权限与每日配额约束）。
            </span>
          </div>
        </div>
      ) : null}

      <div className="legend-block mt-3">
        <b>口径</b>
        <br />
        全部基于内存中的全市场快照筛选；缺失值（PE 为负）不参与区间条件，停牌与退市标的（当日无价格）不进入结果。
        <br />
        结果上方会回显实际生效的条件；字段名写错时会被明确列出，而不是静默返回全市场。
        <br />
        移动端只提供最常用的三个条件；完整条件、策略保存、分布统计与历史在桌面版选股器里。
      </div>
    </>
  );
}

/* ------------------------------------------------------------------
   我的（设置入口 + 账号 + 数据口径）
   ------------------------------------------------------------------ */

export function MobileSettingsPage() {
  const { me, signOut } = useAuth();
  const theme = useTheme();
  const realtime = useRealtime();
  const navigate = useNavigate();

  return (
    <>
      <MobilePageHead title="我的" />

      <div className="card">
        <div className="card-body row gap-3" style={{ alignItems: 'center' }}>
          <span className="avatar" style={{ width: 40, height: 40, fontSize: 15 }}>
            {(me?.nickname ?? me?.username ?? '—').slice(0, 1)}
          </span>
          <div className="grow">
            <div className="fs-14 fw-600">{me?.nickname ?? '—'}</div>
            <div className="hint">
              {me?.username ?? '—'} · {me?.roleName ?? '—'}
            </div>
          </div>
          <span className="tag tag-brand">{me?.dataScope === 'watchlist' ? '仅自选' : '全市场'}</span>
        </div>
      </div>

      <div className="card mt-3">
        <div className="card-head">
          <span className="card-title">外观</span>
        </div>
        <div className="card-body col gap-3">
          <div className="row-between">
            <span className="fs-12 t-2">主题</span>
            <span className="segmented">
              <span className={theme.theme === 'dark' ? 'is-active' : undefined} onClick={() => theme.setTheme('dark')}>
                深色
              </span>
              <span className={theme.theme === 'light' ? 'is-active' : undefined} onClick={() => theme.setTheme('light')}>
                浅色
              </span>
            </span>
          </div>
          <div className="row-between">
            <span className="fs-12 t-2">涨跌颜色</span>
            <span className="segmented">
              <span className={theme.updown === 'red-up' ? 'is-active' : undefined} onClick={() => theme.setUpdown('red-up')}>
                红涨
              </span>
              <span className={theme.updown === 'green-up' ? 'is-active' : undefined} onClick={() => theme.setUpdown('green-up')}>
                绿涨
              </span>
            </span>
          </div>
          <div className="row-between">
            <span className="fs-12 t-2">推送间隔</span>
            <span className="segmented">
              {[3, 5, 10].map((seconds) => (
                <span
                  key={seconds}
                  className={realtime.intervalSeconds === seconds ? 'is-active' : undefined}
                  onClick={() => realtime.setIntervalSeconds(seconds)}
                >
                  {seconds}s
                </span>
              ))}
            </span>
          </div>
        </div>
      </div>

      <div className="card mt-3">
        <div className="card-head">
          <span className="card-title">更多设置</span>
          <span className="card-sub">免打扰、推送订阅、PWA 安装</span>
        </div>
        <div className="card-body col gap-2">
          <Link className="btn btn-outline btn-block" to="/settings">
            打开完整设置（含推送订阅与免打扰）
          </Link>
          <Link className="btn btn-ghost btn-block" to="/notify-preview">
            查看提醒形态预览
          </Link>
          <Link className="btn btn-ghost btn-block" to="/m/about">
            数据口径与关于
          </Link>
        </div>
      </div>

      <div className="card mt-3">
        <div className="card-body">
          <button
            type="button"
            className="btn btn-outline btn-block"
            onClick={() => {
              void signOut();
              navigate('/login');
            }}
          >
            退出登录
          </button>
        </div>
      </div>
    </>
  );
}

/* ------------------------------------------------------------------
   关于（数据口径）
   ------------------------------------------------------------------ */

export function MobileAboutPage() {
  return (
    <>
      <MobilePageHead title="数据口径与关于" back={{ to: '/m/settings', text: '我的' }} />

      <div className="card">
        <div className="card-head">
          <span className="card-title">行情</span>
        </div>
        <div className="card-body">
          <div className="legend-block">
            东方财富公开接口；<b>全市场</b>按扫描周期整体刷新（端点单页上限 100 行，一轮约 60 次请求），非逐笔。
            <br />
            <b>自选股</b>：多标的快照经 SignalR 推送，间隔 3/5/10 秒；失败自动降级到腾讯备源。
            <br />
            <b>日线</b>：前复权，落本地 Parquet；指标由服务端批量计算。
            <br />
            <b>行业分类</b>：东财行业（申万一级无公开直取源）。
            <br />
            <b>北向资金</b>：公开接口已不再提供逐日净买入，本实现不提供该数值，也不以估算替代。
          </div>
        </div>
      </div>

      <div className="card mt-3">
        <div className="card-head">
          <span className="card-title">规则引擎推算</span>
        </div>
        <div className="card-body">
          <div className="legend-block">
            <b>行业景气度</b>：五个维度（资金 30 / 涨跌家数 25 / 相对强弱 20 / 估值位置 15 / 传导带宽 10）
            加权打分，权重与得分都在页面上列出，可逐项核对。
            <br />
            <b>因果链与传导带宽</b>：每一环给出可复算的证据与置信度；相关不代表因果，
            产业链上下游关系需要专门的产业数据源，本轮不提供。
          </div>
        </div>
      </div>

      <div className="card mt-3">
        <div className="card-head">
          <span className="card-title">移动端与桌面端</span>
        </div>
        <div className="card-body">
          <div className="legend-block">
            移动端路由在 <code>/m/*</code>，桌面端在根路径；两者共用同一套接口与口径，
            只是排布不同（移动端用底部 Tab 与卡片，桌面端用侧边导航与表格）。
            <br />
            个股模块页在移动端复用桌面组件（它们本身就是卡片式布局）。
            <br />
            <code>/m/*</code> 之外还提供：完整设置、预警形态预览、管理后台（桌面体验更好）。
          </div>
        </div>
      </div>
    </>
  );
}

/* ------------------------------------------------------------------
   工具
   ------------------------------------------------------------------ */

function fmt(value: number | null | undefined, digits = 2): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '—';
  }

  return value.toLocaleString('zh-CN', { minimumFractionDigits: digits, maximumFractionDigits: digits });
}

function signed(value: number | null | undefined, digits = 2): string {
  if (value === null || value === undefined) {
    return '—';
  }

  return `${value >= 0 ? '+' : ''}${fmt(value, digits)}`;
}

function tone(value: number | null | undefined): string {
  if (value === null || value === undefined) return '';
  return value > 0 ? 't-up' : value < 0 ? 't-down' : '';
}

function levelClass(level: string): string {
  switch (level) {
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
