import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { stockPathKeepingTab } from '@/app/useCurrentStock';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { EmptyState, ErrorState, FreshnessNote } from '@/components/ui/States';
import { errorText } from '@/lib/errorText';
import { useToast } from '@/providers/ToastProvider';
import { useRealtime } from '@/providers/RealtimeProvider';
import {
  createWatchGroup,
  fetchWatchlist,
  removeWatchGroup,
  removeWatchItems,
  reorderWatchItems,
  type WatchItem
} from './api';

/** 推送间隔可选值（与后端 PushIntervals 一致）。 */
const IntervalOptions = [3, 5, 10] as const;

/**
 * 自选股。
 *
 * 结构与 `design/web/watchlist.html` 对应：分组标签 → 行情表 → 批量操作 → 实时状态。
 * 行情优先取推送值，未收到推送时回落到快照值，并明确标注数据时间（不把快照当实时）。
 */
export function WatchlistPage() {
  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['watchlist'],
    queryFn: fetchWatchlist
  });

  if (isPending) {
    return <div className="sa-boot">正在载入自选股…</div>;
  }

  if (!data) {
    return (
      <>
        <Head count={0} />
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

function Head({ count, quota }: { count: number; quota?: number }) {
  return (
    <div className="sa-pagehead">
      <div>
        <div className="breadcrumb">
          <Link to="/market">市场概览</Link>
          <span className="sep">/</span>
          <span>自选股</span>
        </div>
        <h1>自选股</h1>
        <div className="sub">
          {quota ? `${count} / ${quota} 只` : `${count} 只`} · 行情按推送间隔实时更新
        </div>
      </div>
    </div>
  );
}

function Content({ data }: { data: Awaited<ReturnType<typeof fetchWatchlist>> }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const realtime = useRealtime();

  const [activeGroup, setActiveGroup] = useState<string | 'all' | 'ungrouped'>('all');
  const [selected, setSelected] = useState<string[]>([]);
  const [newGroup, setNewGroup] = useState('');
  const [dragging, setDragging] = useState<string | null>(null);
  const [order, setOrder] = useState<string[] | null>(null);

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['watchlist'] });

  /* --- 变更操作 --- */
  const removeMutation = useMutation({
    mutationFn: (codes: string[]) => removeWatchItems(codes),
    onSuccess: async (count) => {
      toast.toast(`已移出 ${count} 只`, 'ok');
      setSelected([]);
      await invalidate();
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const createGroupMutation = useMutation({
    mutationFn: (name: string) => createWatchGroup(name),
    onSuccess: async () => {
      setNewGroup('');
      toast.toast('分组已创建', 'ok');
      await invalidate();
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const removeGroupMutation = useMutation({
    mutationFn: (groupId: string) => removeWatchGroup(groupId),
    onSuccess: async () => {
      toast.toast('分组已删除，组内股票已移回未分组', 'ok');
      setActiveGroup('all');
      await invalidate();
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const reorderMutation = useMutation({
    mutationFn: (items: { code: string; groupId: string | null; note: string | null }[]) => reorderWatchItems(items),
    onSuccess: async () => {
      setOrder(null);
      await invalidate();
    },
    onError: (mutationError) => {
      // 保存失败要恢复本地顺序，避免界面显示的与服务器不一致
      setOrder(null);
      toast.toast(errorText(mutationError), 'error');
    }
  });

  /* --- 过滤与排序 --- */
  const items = useMemo(() => {
    const base = order
      ? [...data.items].sort((a, b) => order.indexOf(a.code) - order.indexOf(b.code))
      : data.items;

    switch (activeGroup) {
      case 'all':
        return base;
      case 'ungrouped':
        return base.filter((item) => !item.groupId);
      default:
        return base.filter((item) => item.groupId === activeGroup);
    }
  }, [data.items, activeGroup, order]);

  const allSelected = items.length > 0 && selected.length === items.length;

  const toggleAll = () => setSelected(allSelected ? [] : items.map((item) => item.code));
  const toggleOne = (code: string) =>
    setSelected((previous) => (previous.includes(code) ? previous.filter((c) => c !== code) : [...previous, code]));

  /* --- 拖拽排序 --- */
  const handleDrop = (targetCode: string) => {
    if (!dragging || dragging === targetCode || !data.canEdit) {
      setDragging(null);
      return;
    }

    const codes = items.map((item) => item.code);
    const from = codes.indexOf(dragging);
    const to = codes.indexOf(targetCode);
    if (from < 0 || to < 0) {
      setDragging(null);
      return;
    }

    const next = [...codes];
    next.splice(to, 0, ...next.splice(from, 1));

    // 本地先行展示，再提交；失败会回滚（见 reorderMutation）
    setOrder(next);
    setDragging(null);

    const groupIdOf = new Map(data.items.map((item) => [item.code, item.groupId]));
    const noteOf = new Map(data.items.map((item) => [item.code, item.note]));
    reorderMutation.mutate(
      next.map((code) => ({ code, groupId: groupIdOf.get(code) ?? null, note: noteOf.get(code) ?? null }))
    );
  };

  return (
    <>
      <Head count={data.items.length} quota={data.quota} />

      {/* 实时状态条 */}
      <div className="card is-accent">
        <div className="card-body is-tight row-between wrap gap-3">
          <span className="row gap-3 wrap" style={{ alignItems: 'center' }}>
            <RealtimeBadge status={realtime.status} />
            <span className="fs-11 t-3">
              {realtime.status === 'connected'
                ? `已订阅 ${realtime.subscribed.length} 只 · 推送间隔 ${realtime.intervalSeconds} 秒`
                : '未连接实时行情，表格显示最近一次快照值'}
            </span>
            {realtime.lastPushAt ? (
              <span className="fs-11 t-3">最近推送 {new Date(realtime.lastPushAt).toLocaleTimeString('zh-CN')}</span>
            ) : null}
          </span>
          <span className="segmented">
            {IntervalOptions.map((seconds) => (
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

      {/* 分组标签 */}
      <div className="row gap-2 wrap mt-3" style={{ alignItems: 'center' }}>
        <span className="segmented">
          <span className={activeGroup === 'all' ? 'is-active' : undefined} onClick={() => setActiveGroup('all')}>
            全部 {data.items.length}
          </span>
          <span
            className={activeGroup === 'ungrouped' ? 'is-active' : undefined}
            onClick={() => setActiveGroup('ungrouped')}
          >
            未分组 {data.items.filter((item) => !item.groupId).length}
          </span>
          {data.groups.map((group) => (
            <span
              key={group.id}
              className={activeGroup === group.id ? 'is-active' : undefined}
              onClick={() => setActiveGroup(group.id)}
            >
              {group.name} {group.count}
            </span>
          ))}
        </span>

        {data.canEdit ? (
          <span className="row gap-2" style={{ marginLeft: 'auto' }}>
            <input
              className="input input-sm"
              style={{ width: 130 }}
              placeholder="新分组名"
              value={newGroup}
              onChange={(event) => setNewGroup(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter' && newGroup.trim()) {
                  createGroupMutation.mutate(newGroup.trim());
                }
              }}
            />
            <button
              type="button"
              className="btn btn-sm btn-outline"
              disabled={!newGroup.trim() || createGroupMutation.isPending}
              onClick={() => createGroupMutation.mutate(newGroup.trim())}
            >
              新建分组
            </button>
            {activeGroup !== 'all' && activeGroup !== 'ungrouped' ? (
              <button
                type="button"
                className="btn btn-sm btn-ghost"
                onClick={() => removeGroupMutation.mutate(activeGroup)}
              >
                删除当前分组
              </button>
            ) : null}
          </span>
        ) : null}
      </div>

      {/* 批量操作 */}
      {data.canEdit && selected.length > 0 ? (
        <div className="card mt-3">
          <div className="card-body is-tight row-between">
            <span className="fs-12 t-2">已选 {selected.length} 只</span>
            <button
              type="button"
              className="btn btn-sm btn-outline"
              disabled={removeMutation.isPending}
              onClick={() => removeMutation.mutate(selected)}
            >
              批量移出自选
            </button>
          </div>
        </div>
      ) : null}

      {/* 行情表 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">自选行情</span>
          <span className="card-sub">
            {data.canEdit ? '拖动行可调整顺序 · ' : ''}推送值优先，未推送时显示快照值
          </span>
        </div>
        <div className="card-body is-flush">
          {items.length === 0 ? (
            <EmptyState
              title={data.items.length === 0 ? '还没有自选股' : '当前分组下没有股票'}
              hint={
                data.items.length === 0
                  ? '在搜索页把标的加入自选后，这里会实时刷新行情。'
                  : '切换到「全部」标签查看，或把股票拖到该分组。'
              }
              action={
                data.items.length === 0 ? (
                  <Link className="btn btn-sm btn-primary" to="/search">
                    去搜索股票
                  </Link>
                ) : null
              }
            />
          ) : (
            <div className="tbl-wrap">
              <table className="tbl is-comfort">
                <thead>
                  <tr>
                    {data.canEdit ? (
                      <th style={{ width: 32 }}>
                        <input type="checkbox" checked={allSelected} onChange={toggleAll} aria-label="全选" />
                      </th>
                    ) : null}
                    <th>名称 / 代码</th>
                    <th>行业</th>
                    <th className="num">现价</th>
                    <th className="num">涨跌幅</th>
                    <th className="num hide-mobile">成交额</th>
                    <th className="num hide-mobile">换手率</th>
                    <th className="num hide-mobile">量比</th>
                    <th>备注</th>
                    <th className="col-actions" style={{ width: 130 }}>
                      操作
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <WatchRow
                      key={item.code}
                      item={item}
                      canEdit={data.canEdit}
                      selected={selected.includes(item.code)}
                      dragging={dragging === item.code}
                      onToggle={() => toggleOne(item.code)}
                      onDragStart={() => setDragging(item.code)}
                      onDrop={() => handleDrop(item.code)}
                      onRemove={() => removeMutation.mutate([item.code])}
                      onWatchlistUpdate={invalidate}
                    />
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      <FreshnessNote
        asOf={data.asOf}
        source="实时推送（自选股）· 快照为全市场扫描结果"
        note={data.canEdit ? undefined : '当前账号没有编辑自选的权限，仅可查看。'}
      />

      <div className="legend-block mt-4">
        <b>数据来源与口径</b>
        <br />
        实时行情：东方财富多标的快照端点（请求数与标的数无关），失败自动降级到腾讯备源；推送间隔 3 / 5 / 10 秒可选。
        <br />
        非交易时段不轮询：表格显示最近一个交易日的收盘快照，并在上方标注连接状态。
        <br />
        权限：读取需 <code>watchlist.view</code>，增删改需 <code>watchlist.edit</code>；数量上限取自角色配额 <code>watchlist.max</code>。
        <br />
        数据范围为「仅自选股」的账号，市场排行与搜索结果同样只包含自选范围内的标的。
      </div>
    </>
  );
}

/** 一行自选。 */
function WatchRow({
  item,
  canEdit,
  selected,
  dragging,
  onToggle,
  onDragStart,
  onDrop,
  onRemove,
  onWatchlistUpdate
}: {
  item: WatchItem;
  canEdit: boolean;
  selected: boolean;
  dragging: boolean;
  onToggle: () => void;
  onDragStart: () => void;
  onDrop: () => void;
  onRemove: () => void;
  onWatchlistUpdate: () => Promise<unknown>;
}) {
  const live = useRealtime().quotes[item.code];
  const [note, setNote] = useState(item.note ?? '');
  const [savingNote, setSavingNote] = useState(false);

  // 推送值优先；没有推送时用快照值，并靠 data-live 属性区分展示
  const price = live?.price ?? item.price;
  const pct = live?.pct ?? item.pct;
  const amount = live?.amount ?? item.amount;
  const turnover = live?.turnover ?? item.turnover;
  const volRatio = live?.volRatio ?? item.volRatio;
  const isLive = Boolean(live);

  const saveNote = async () => {
    if (note === (item.note ?? '')) {
      return;
    }

    setSavingNote(true);
    try {
      await reorderWatchItems([{ code: item.code, groupId: item.groupId, note }]);
      await onWatchlistUpdate();
    } finally {
      setSavingNote(false);
    }
  };

  return (
    <tr
      data-chg={pct === null ? 'flat' : pct >= 0 ? 'up' : 'down'}
      data-live={isLive ? 'true' : undefined}
      draggable={canEdit}
      onDragStart={onDragStart}
      onDragOver={(event) => event.preventDefault()}
      onDrop={onDrop}
      style={{ opacity: dragging ? 0.4 : 1, cursor: canEdit ? 'grab' : undefined }}
    >
      {canEdit ? (
        <td>
          <input type="checkbox" checked={selected} onChange={onToggle} aria-label={`选择 ${item.name}`} />
        </td>
      ) : null}
      <td>
        <Link className="stock-cell" to={stockPathKeepingTab(item.code)}>
          <span className="sc-name">
            {item.name}
            {item.isSt ? <span className="tag tag-danger" style={{ marginLeft: 6 }}>ST</span> : null}
            {isLive ? <span className="tag tag-up" style={{ marginLeft: 6 }}>实时</span> : null}
          </span>
          <span className="sc-code">{item.code}</span>
        </Link>
      </td>
      <td className="t-2">{item.industry ?? '—'}</td>
      <td className="num mono">{fmt(price)}</td>
      <td className={`num ${pct === null ? '' : pct > 0 ? 'is-up' : pct < 0 ? 'is-down' : 'is-flat'}`}>
        {signed(pct)}
      </td>
      <td className="num hide-mobile">{amount === null ? '—' : `${fmt(amount)} 亿`}</td>
      <td className="num hide-mobile">{turnover === null ? '—' : `${fmt(turnover)}%`}</td>
      <td className="num hide-mobile">{fmt(volRatio)}</td>
      <td>
        <input
          className="input input-sm"
          style={{ width: 140 }}
          placeholder="备注"
          value={note}
          disabled={!canEdit || savingNote}
          onChange={(event) => setNote(event.target.value)}
          onBlur={() => void saveNote()}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              (event.target as HTMLInputElement).blur();
            }
          }}
        />
      </td>
      <td className="col-actions">
        <span className="row gap-2">
          <Link className="btn btn-sm btn-outline" to={`/stock/${item.code}/trend`}>
            趋势
          </Link>
          {canEdit ? (
            <button type="button" className="btn btn-sm btn-ghost" onClick={onRemove}>
              移出
            </button>
          ) : null}
        </span>
      </td>
    </tr>
  );
}

/** 连接状态徽标。 */
function RealtimeBadge({ status }: { status: string }) {
  const map: Record<string, { text: string; cls: string }> = {
    connected: { text: '实时已连接', cls: 'tag-up' },
    connecting: { text: '正在连接', cls: 'tag-outline' },
    reconnecting: { text: '正在重连', cls: 'tag-warn' },
    closed: { text: '连接已断开', cls: 'tag-down' },
    idle: { text: '未启用实时', cls: 'tag-outline' }
  };

  const current = map[status] ?? map.idle;
  return <span className={`tag ${current.cls}`}>{current.text}</span>;
}

function fmt(value: number | null | undefined, digits = 2): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '—';
  }

  return value.toLocaleString('zh-CN', { minimumFractionDigits: digits, maximumFractionDigits: digits });
}

function signed(value: number | null | undefined): string {
  if (value === null || value === undefined) {
    return '—';
  }

  return `${value >= 0 ? '+' : ''}${fmt(value)}%`;
}
