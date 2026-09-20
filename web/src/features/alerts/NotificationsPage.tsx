import { useState } from 'react';
import { Link } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { EmptyState, ErrorState } from '@/components/ui/States';
import { errorText } from '@/lib/errorText';
import { useRealtime } from '@/providers/RealtimeProvider';
import { useToast } from '@/providers/ToastProvider';
import { fetchNotifications, markNotificationsRead } from './api';

/**
 * 通知中心。
 *
 * 结构与 `design/web/notifications.html` 对应：未读统计 → 筛选 → 通知列表 → 口径说明。
 *
 * 与提醒规则的分工：规则决定「什么时候提醒」，通知是「已经提醒过什么」的历史，
 * 因此删除规则不会删除通知（页面在说明里写明）。
 */
export function NotificationsPage() {
  const [unreadOnly, setUnreadOnly] = useState(false);
  const realtime = useRealtime();

  const { data, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['notifications', unreadOnly],
    queryFn: () => fetchNotifications(unreadOnly),
    // 收到实时提醒后自动刷新，避免用户手动点刷新才知道有新通知
    refetchInterval: realtime.lastPushAt ? 30_000 : false
  });

  const queryClient = useQueryClient();
  const toast = useToast();

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
        <Head unread={0} />
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
      <Head unread={data.unread} />

      <div className="row gap-2 wrap" style={{ alignItems: 'center' }}>
        <span className="segmented">
          <span className={!unreadOnly ? 'is-active' : undefined} onClick={() => setUnreadOnly(false)}>
            全部 {data.items.length}
          </span>
          <span className={unreadOnly ? 'is-active' : undefined} onClick={() => setUnreadOnly(true)}>
            未读 {data.unread}
          </span>
        </span>

        <span className="row gap-2" style={{ marginLeft: 'auto' }}>
          <button
            type="button"
            className="btn btn-sm btn-outline"
            disabled={data.unread === 0 || readMutation.isPending}
            onClick={() => readMutation.mutate([])}
          >
            全部标记已读
          </button>
          <Link className="btn btn-sm btn-ghost" to="/alerts">
            管理提醒规则
          </Link>
        </span>
      </div>

      <div className="card mt-3">
        <div className="card-head">
          <span className="card-title">通知记录</span>
          <span className="card-sub">{unreadOnly ? '仅未读' : '全部'} · 共 {data.items.length} 条</span>
        </div>
        <div className="card-body">
          {data.items.length === 0 ? (
            <EmptyState
              title={unreadOnly ? '没有未读通知' : '还没有通知'}
              hint="提醒规则触发后会在这里留下记录；可以在「提醒规则」页新建规则。"
            />
          ) : (
            <div className="col gap-3">
              {data.items.map((item) => (
                <div key={item.id} className="col gap-1">
                  <div className="row-between wrap gap-2">
                    <span className="row gap-2 wrap" style={{ alignItems: 'center' }}>
                      <span className={`tag ${levelClass(item.level)}`}>
                        {item.level === 'up' ? '利多' : item.level === 'down' ? '利空' : item.level === 'warn' ? '提示' : '信息'}
                      </span>
                      <span className={`fw-600 fs-12 ${item.isRead ? 't-2' : ''}`}>{item.title}</span>
                      {!item.isRead ? <span className="tag tag-outline">未读</span> : null}
                    </span>
                    <span className="row gap-2" style={{ alignItems: 'center' }}>
                      <span className="fs-11 t-3">{item.createdAt}</span>
                      {item.code ? (
                        <Link className="btn btn-sm btn-ghost" to={`/stock/${item.code}`}>
                          查看
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
                    </span>
                  </div>
                  <div className="fs-11 t-2" style={{ whiteSpace: 'pre-line' }}>
                    {item.body}
                  </div>
                  <div className="divider" />
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      <div className="legend-block mt-4">
        <b>数据来源与口径</b>
        <br />
        通知由提醒规则触发产生；评估每 30 秒一轮，同一条规则触发后冷却 30 分钟。
        <br />
        已读状态按账号保存；删除提醒规则不会删除已产生的通知，历史仍可回溯（规则 Id 会保留）。
        <br />
        实时推送仅在本页打开时生效（经 <code>/hubs/quotes</code> 的用户分组下发），
        离线期间产生的通知会在下次打开时列出。
      </div>
    </>
  );
}

function Head({ unread }: { unread: number }) {
  return (
    <div className="sa-pagehead">
      <div>
        <div className="breadcrumb">
          <Link to="/watchlist">自选股</Link>
          <span className="sep">/</span>
          <span>通知中心</span>
        </div>
        <h1>通知中心</h1>
        <div className="sub">{unread > 0 ? `${unread} 条未读` : '没有未读通知'}</div>
      </div>
    </div>
  );
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
