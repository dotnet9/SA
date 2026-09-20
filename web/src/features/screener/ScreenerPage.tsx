import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { useMutation, useQuery } from '@tanstack/react-query';
import { EmptyState, ErrorState, FreshnessNote } from '@/components/ui/States';
import { errorText } from '@/lib/errorText';
import { useToast } from '@/providers/ToastProvider';
import {
  exportScreener,
  fetchScreenerMeta,
  runScreener,
  type ScreenerCondition,
  type ScreenerResult
} from './api';

/**
 * 条件选股器。
 *
 * 结构与 `design/web/screener.html` 对应：预设 → 条件编辑 → 结果表 → 导出 → 口径说明。
 *
 * 结果表上方始终展示「实际生效的条件」（服务端回显）：字段名写错时服务端会明确列出被忽略的项，
 * 而不是静默返回全市场结果让人误以为筛过了。
 */
export function ScreenerPage() {
  const { data: meta, error, isPending, refetch, failureCount } = useQuery({
    queryKey: ['screener', 'meta'],
    queryFn: fetchScreenerMeta
  });

  if (isPending) {
    return <div className="sa-boot">正在载入选股器…</div>;
  }

  if (!meta) {
    return (
      <>
        <div className="sa-pagehead">
          <div>
            <h1>条件选股器</h1>
            <div className="sub">按横截面条件筛选全市场</div>
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

  return <Content meta={meta} />;
}

function Content({ meta }: { meta: Awaited<ReturnType<typeof fetchScreenerMeta>> }) {
  const toast = useToast();
  const [preset, setPreset] = useState<string | null>(null);
  const [values, setValues] = useState<Record<string, { min: string; max: string }>>({});
  const [board, setBoard] = useState('');
  const [excludeSt, setExcludeSt] = useState(true);
  const [sortBy, setSortBy] = useState('amount');
  const [result, setResult] = useState<ScreenerResult | null>(null);

  const condition: ScreenerCondition = useMemo(
    () => ({
      ranges: Object.entries(values)
        .filter(([, value]) => value.min !== '' || value.max !== '')
        .map(([field, value]) => ({
          field,
          min: value.min === '' ? null : Number(value.min),
          max: value.max === '' ? null : Number(value.max)
        })),
      enums: board ? [{ field: 'board', values: [board] }] : [],
      flags: [{ field: 'isSt', value: !excludeSt }],
      sortBy,
      sortDesc: true,
      page: 1,
      pageSize: 50,
      // 预设与自定义条件二选一：选了预设就忽略其余条件，避免两者叠加后无法解释结果
      preset
    }),
    [values, board, excludeSt, sortBy, preset]
  );

  const runMutation = useMutation({
    mutationFn: () => runScreener(condition),
    onSuccess: (data) => setResult(data),
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const exportMutation = useMutation({
    mutationFn: () => exportScreener(condition),
    onSuccess: (outcome) => {
      if (outcome.ok) {
        toast.toast('导出已开始', 'ok');
      } else {
        toast.toast(outcome.message ?? '导出失败', 'error');
      }
    },
    onError: (mutationError) => toast.toast(errorText(mutationError), 'error')
  });

  const setRange = (field: string, key: 'min' | 'max', value: string) =>
    setValues((previous) => ({
      ...previous,
      [field]: { min: previous[field]?.min ?? '', max: previous[field]?.max ?? '', [key]: value }
    }));

  return (
    <>
      <div className="sa-pagehead">
        <div>
          <div className="breadcrumb">
            <Link to="/market">市场概览</Link>
            <span className="sep">/</span>
            <span>条件选股器</span>
          </div>
          <h1>条件选股器</h1>
          <div className="sub">
            横截面筛选（同一时点全市场）· 导出剩余 {meta.exportQuota} 次
            {result?.asOf ? ` · 行情时间 ${result.asOf}` : ''}
          </div>
        </div>
      </div>

      {/* 预设 */}
      <div className="card">
        <div className="card-head">
          <span className="card-title">预设条件</span>
          <span className="card-sub">选中预设后忽略下方的自定义条件</span>
        </div>
        <div className="card-body row gap-2 wrap">
          <span
            className={`tag ${preset === null ? 'tag-up' : 'tag-outline'}`}
            style={{ cursor: 'pointer' }}
            onClick={() => setPreset(null)}
          >
            自定义
          </span>
          {meta.presets.map((item) => (
            <span
              key={item.key}
              className={`tag ${preset === item.key ? 'tag-up' : 'tag-outline'}`}
              style={{ cursor: 'pointer' }}
              title={item.description}
              onClick={() => setPreset(item.key)}
            >
              {item.name}
            </span>
          ))}
          {preset ? (
            <div className="chart-note" style={{ width: '100%' }}>
              {meta.presets.find((item) => item.key === preset)?.description}
            </div>
          ) : null}
        </div>
      </div>

      {/* 条件编辑 */}
      <div className="card mt-4">
        <div className="card-head">
          <span className="card-title">筛选条件</span>
          <span className="card-sub">{preset ? '已选预设，以下条件将不生效' : '留空表示不限'}</span>
        </div>
        <div className="card-body">
          <div className="grid grid-3" style={{ opacity: preset ? 0.5 : 1 }}>
            {meta.fields.map((field) => (
              <div key={field.field} className="col gap-1">
                <span className="fs-11 t-3">
                  {field.name}（{field.unit}）
                </span>
                <span className="row gap-2">
                  <input
                    className="input input-sm"
                    style={{ width: '100%' }}
                    placeholder="下限"
                    disabled={preset !== null}
                    value={values[field.field]?.min ?? ''}
                    onChange={(event) => setRange(field.field, 'min', event.target.value)}
                  />
                  <input
                    className="input input-sm"
                    style={{ width: '100%' }}
                    placeholder="上限"
                    disabled={preset !== null}
                    value={values[field.field]?.max ?? ''}
                    onChange={(event) => setRange(field.field, 'max', event.target.value)}
                  />
                </span>
              </div>
            ))}
          </div>

          <div className="row gap-3 wrap mt-3" style={{ alignItems: 'center' }}>
            <label className="row gap-2" style={{ alignItems: 'center' }}>
              <span className="fs-12 t-2">板块</span>
              <select
                className="input input-sm"
                style={{ width: 140 }}
                value={board}
                disabled={preset !== null}
                onChange={(event) => setBoard(event.target.value)}
              >
                <option value="">全部</option>
                {meta.boards.map((item) => (
                  <option key={item} value={item}>
                    {item}
                  </option>
                ))}
              </select>
            </label>

            <label className="row gap-2" style={{ alignItems: 'center' }}>
              <input
                type="checkbox"
                checked={excludeSt}
                disabled={preset !== null}
                onChange={(event) => setExcludeSt(event.target.checked)}
              />
              <span className="fs-12 t-2">排除 ST</span>
            </label>

            <label className="row gap-2" style={{ alignItems: 'center' }}>
              <span className="fs-12 t-2">排序</span>
              <select
                className="input input-sm"
                style={{ width: 140 }}
                value={sortBy}
                onChange={(event) => setSortBy(event.target.value)}
              >
                {meta.fields.map((field) => (
                  <option key={field.field} value={field.field}>
                    {field.name}
                  </option>
                ))}
                <option value="amount">成交额</option>
              </select>
            </label>

            <span className="row gap-2" style={{ marginLeft: 'auto' }}>
              <button
                type="button"
                className="btn btn-sm btn-primary"
                disabled={runMutation.isPending}
                onClick={() => runMutation.mutate()}
              >
                {runMutation.isPending ? '筛选中…' : '开始筛选'}
              </button>
              <button
                type="button"
                className="btn btn-sm btn-outline"
                disabled={exportMutation.isPending || meta.exportQuota <= 0}
                onClick={() => exportMutation.mutate()}
              >
                {meta.exportQuota > 0 ? `导出 CSV（剩 ${meta.exportQuota} 次）` : '导出额度已用完'}
              </button>
            </span>
          </div>
        </div>
      </div>

      {/* 结果 */}
      {result ? (
        <div className="card mt-4">
          <div className="card-head">
            <span className="card-title">筛选结果</span>
            <span className="card-sub">
              命中 {result.total} 只 · 显示前 {result.rows.length} 只
            </span>
          </div>
          <div className="card-body is-flush">
            {/* 回显实际生效的条件 */}
            <div className="chart-note" style={{ padding: '8px 12px' }}>
              {result.applied.map((item) => (
                <div key={item}>· {item}</div>
              ))}
              {result.scopeNote ? <div>· {result.scopeNote}</div> : null}
            </div>

            {result.rows.length === 0 ? (
              <EmptyState title="没有符合条件的标的" hint="放宽条件或换一个预设再试。" />
            ) : (
              <div className="tbl-wrap">
                <table className="tbl is-comfort">
                  <thead>
                    <tr>
                      <th>名称 / 代码</th>
                      <th>板块</th>
                      <th>行业</th>
                      <th className="num">现价</th>
                      <th className="num">涨跌幅</th>
                      <th className="num hide-mobile">换手率</th>
                      <th className="num hide-mobile">量比</th>
                      <th className="num">成交额</th>
                      <th className="num hide-mobile">PE(TTM)</th>
                      <th className="num hide-mobile">PB</th>
                      <th className="num">总市值</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.rows.map((row) => (
                      <tr key={row.code} data-chg={row.pct >= 0 ? 'up' : 'down'}>
                        <td>
                          <Link className="stock-cell" to={`/stock/${row.code}`}>
                            <span className="sc-name">
                              {row.name}
                              {row.isSt ? <span className="tag tag-danger" style={{ marginLeft: 6 }}>ST</span> : null}
                            </span>
                            <span className="sc-code">{row.code}</span>
                          </Link>
                        </td>
                        <td className="fs-11 t-2">{row.board}</td>
                        <td className="fs-11 t-2">{row.industry ?? '—'}</td>
                        <td className="num mono">{row.price.toFixed(2)}</td>
                        <td className={`num ${row.pct > 0 ? 'is-up' : row.pct < 0 ? 'is-down' : 'is-flat'}`}>
                          {row.pct >= 0 ? '+' : ''}
                          {row.pct.toFixed(2)}%
                        </td>
                        <td className="num hide-mobile">{row.turnover.toFixed(2)}%</td>
                        <td className="num hide-mobile">{row.volRatio.toFixed(2)}</td>
                        <td className="num">{row.amount.toFixed(2)} 亿</td>
                        <td className="num hide-mobile">{row.peTtm === null ? '—' : row.peTtm.toFixed(2)}</td>
                        <td className="num hide-mobile">{row.pb === null ? '—' : row.pb.toFixed(2)}</td>
                        <td className="num">{row.cap.toFixed(2)} 亿</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      ) : null}

      <FreshnessNote asOf={result?.asOf} source="全市场快照（同一时点横截面）" />

      <div className="legend-block mt-4">
        <b>数据来源与口径</b>
        <br />
        全部基于内存中的全市场快照筛选：横截面条件（市值、PE、涨跌幅…）只有在同一时点的全市场数据上才有意义。
        <br />
        缺失值不参与区间筛选：PE 为负（亏损）或为空的标的不会被当作「低 PE」筛出来。
        <br />
        停牌与退市标的（当日无价格）不进入结果，否则它们的比率字段会污染排序。
        <br />
        结果表上方会回显实际生效的条件；字段名写错时会被明确列出，而不是静默返回全市场。
        <br />
        导出为 CSV（UTF-8 BOM，Excel 直接打开不乱码），受 <code>export.data</code> 权限与每日配额约束。
      </div>
    </>
  );
}
