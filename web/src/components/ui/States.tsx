import type { ReactNode } from 'react';
import { ApiError, defaultMessage, ErrorCode } from '@/lib/errors';

/**
 * 空态、错误态与数据新鲜度提示。
 *
 * 三条共同约定（实施计划 §5.1 / §10）：
 * 1. 空数据一律显式说明原因，不允许静默留空，也不允许填假值；
 * 2. 数据未就绪（1003）区分于失败，提示「采集中 + 第几次重试」而不是报错；
 * 3. 每个数据区块都要能看到数据时间与来源。
 */

/** 空态。 */
export function EmptyState({
  title,
  hint,
  action
}: {
  title: string;
  hint?: ReactNode;
  action?: ReactNode;
}) {
  return (
    <div className="empty">
      <div className="fs-13 t-2">{title}</div>
      {hint ? <div className="fs-11 t-3" style={{ lineHeight: 1.8, maxWidth: 520 }}>{hint}</div> : null}
      {action}
    </div>
  );
}

/**
 * 错误态。
 *
 * 展示业务错误码与 traceId：用户可以把 traceId 直接对着日志排查，
 * 而不需要描述「什么时间点了哪里」。
 */
export function ErrorState({
  error,
  onRetry,
  retryCount
}: {
  error: unknown;
  onRetry?: () => void;
  /** 已重试次数（来自 TanStack Query 的 failureCount），用于「采集中」文案。 */
  retryCount?: number;
}) {
  const apiError = error instanceof ApiError ? error : null;
  const collecting = apiError?.isDataNotReady ?? false;

  const title = collecting
    ? retryCount && retryCount > 0
      ? `数据正在采集（第 ${retryCount} 次重试）`
      : '数据正在采集'
    : apiError?.message ?? defaultMessage(ErrorCode.Unexpected);

  const hint = collecting
    ? '首次启动需要先采集全市场行情与行业数据，通常在一分钟内完成。页面会自动重试，也可以手动刷新。'
    : apiError
      ? `错误码 ${apiError.code}${apiError.traceId ? ` · traceId ${apiError.traceId}` : ''}`
      : '请稍后重试；若持续失败，请在后台「数据源监控」查看采集状态。';

  return (
    <div className="empty">
      <div className="fs-13 t-1 fw-600">{title}</div>
      <div className="fs-11 t-3" style={{ lineHeight: 1.8, maxWidth: 560 }}>
        {hint}
      </div>
      {onRetry ? (
        <button type="button" className="btn btn-outline btn-sm" onClick={onRetry}>
          {collecting ? '立即重试' : '重试'}
        </button>
      ) : null}
    </div>
  );
}

/**
 * 数据新鲜度提示：数据时间 + 来源 + 口径说明。
 *
 * `asOf` 为空表示尚未采集到，此时不显示时间也不显示「刚刚」这类模糊词。
 */
export function FreshnessNote({
  asOf,
  source,
  note
}: {
  asOf?: string | null;
  source?: string;
  note?: ReactNode;
}) {
  const parts: string[] = [];
  if (asOf) {
    parts.push(`数据时间 ${asOf}`);
  } else {
    parts.push('尚无数据');
  }
  if (source) {
    parts.push(source);
  }

  return (
    <div className="chart-note">
      <span>{parts.join(' · ')}</span>
      {note ? <span>{note}</span> : null}
    </div>
  );
}

/** 数据源健康状态点（后台数据源监控与市场页页头共用）。 */
export function SourceDot({ status }: { status: 'ok' | 'warn' | 'err' | 'idle' }) {
  const cls = status === 'ok' ? 'dot-ok' : status === 'warn' ? 'dot-warn' : status === 'err' ? 'dot-err' : 'dot';
  const label = status === 'ok' ? '正常' : status === 'warn' ? '降级' : status === 'err' ? '不可用' : '未采集';
  return (
    <span className="row gap-1" style={{ alignItems: 'center' }}>
      <span className={`dot ${cls}`} />
      <span className="fs-11 t-3">{label}</span>
    </span>
  );
}
