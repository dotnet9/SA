import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { searchStocks } from '@/features/search/api';
import { fetchMarketStocks } from '@/features/market/api';

/**
 * 全局搜索：聚焦即开面板、输入即出结果、点结果进个股、底部进完整搜索结果页。
 *
 * 交互对齐原型 `_shared/app.js` 的 `bindSearch()`：
 *   - ⌘/Ctrl+K 或 `/` 唤起，Esc 关闭，点击面板外收起；
 *   - 面板内最多 8 条，带 名称 / 代码 · 板块 / 行业 / 现价与涨跌幅；
 *   - 回车直接进**第一条**结果（输入代码后回车的常见意图就是「就去这只」）；
 *     想看完整列表点底部「查看全部搜索结果」。
 *
 * 空关键词时列出**市值前列**而不是写死一份「热门搜索」：写死的清单会随行情过期，
 * 而按市值取前 8 是真实且不会过期的内容（口径在分组标题里写清楚）。
 */

/** 输入防抖：避免每敲一个字都打一次接口。 */
const DebounceMs = 260;

/** 面板内最多显示几条。 */
const MaxItems = 8;

function fmt(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '—';
  }

  return value.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function tone(value: number | null | undefined): string {
  if (value === null || value === undefined || value === 0) return 'is-flat';
  return value > 0 ? 'is-up' : 'is-down';
}

export function GlobalSearch() {
  const [keyword, setKeyword] = useState('');
  const [debounced, setDebounced] = useState('');
  const [open, setOpen] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const navigate = useNavigate();

  /* 防抖：输入停顿后再打接口 */
  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(keyword.trim()), DebounceMs);
    return () => window.clearTimeout(timer);
  }, [keyword]);

  const trimmed = debounced.length > 0;

  /* 有关键词：搜索；无关键词：市值前列（面板一打开就有内容，且不会过期） */
  const search = useQuery({
    queryKey: ['search', 'quick', debounced],
    queryFn: () => searchStocks({ q: debounced, pageSize: MaxItems }),
    enabled: open && trimmed
  });

  const top = useQuery({
    queryKey: ['market', 'stocks', 'top-cap', MaxItems],
    queryFn: () => fetchMarketStocks({ sortBy: 'cap', desc: true, pageSize: MaxItems }),
    enabled: open && !trimmed,
    staleTime: 60_000
  });

  const rows = trimmed ? (search.data?.rows ?? []) : (top.data?.rows ?? []);
  const loading = trimmed ? search.isPending : top.isPending;

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      const target = event.target as HTMLElement | null;
      const isTyping = target?.tagName === 'INPUT' || target?.tagName === 'TEXTAREA';

      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault();
        inputRef.current?.focus();
        setOpen(true);
        return;
      }

      if (event.key === '/' && !isTyping) {
        event.preventDefault();
        inputRef.current?.focus();
        setOpen(true);
        return;
      }

      if (event.key === 'Escape') {
        setOpen(false);
        inputRef.current?.blur();
      }
    }

    function onClick(event: MouseEvent) {
      const target = event.target as Node;
      if (panelRef.current?.contains(target) || inputRef.current?.contains(target)) {
        return;
      }

      setOpen(false);
    }

    document.addEventListener('keydown', onKeyDown);
    document.addEventListener('click', onClick);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      document.removeEventListener('click', onClick);
    };
  }, []);

  const goToStock = (code: string) => {
    setOpen(false);
    navigate(`/market/${code}`);
  };

  const submit = () => {
    /* 结果还没回来时退回完整结果页，避免回车没反应 */
    const first = rows[0];
    if (trimmed && first) {
      goToStock(first.code);
      return;
    }

    setOpen(false);
    navigate(`/search${trimmed ? `?q=${encodeURIComponent(debounced)}` : ''}`);
  };

  return (
    <div className="sa-search">
      <span className="sa-search-icon">⌕</span>
      <input
        ref={inputRef}
        className="sa-search-input"
        type="search"
        autoComplete="off"
        placeholder="搜索股票代码 / 名称 / 拼音首字母，如 300750、宁德时代、ndsd"
        value={keyword}
        onChange={(event) => {
          setKeyword(event.target.value);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            submit();
          }
        }}
      />
      <span className="sa-search-kbd">
        <kbd>⌘</kbd>
        <kbd>K</kbd>
      </span>

      {open ? (
        <div ref={panelRef} className="sa-search-panel is-open">
          <div className="sa-search-group">
            {trimmed ? `匹配 ${search.data?.total ?? rows.length} 只` : '市值前列'}
          </div>

          {loading ? (
            <div className="fs-12 t-3" style={{ padding: '10px 12px' }}>
              搜索中…
            </div>
          ) : rows.length === 0 ? (
            <div className="fs-12 t-3" style={{ padding: '10px 12px' }}>
              没有匹配的股票
            </div>
          ) : (
            rows.slice(0, MaxItems).map((row) => (
              <Link
                key={row.code}
                className="sa-search-item"
                to={`/market/${row.code}`}
                onClick={() => setOpen(false)}
              >
                <span className="stock-cell" style={{ minWidth: 0 }}>
                  <span className="sc-name">{row.name}</span>
                  <span className="sc-code">
                    {row.code} · {row.board}
                  </span>
                </span>
                {row.industry ? (
                  <span className="tag tag-outline" style={{ marginLeft: 'auto' }}>
                    {row.industry}
                  </span>
                ) : null}
                <span className={`chg ${tone(row.pct)}`} style={{ width: 78, textAlign: 'right' }}>
                  {fmt(row.price)}
                  <span> {row.pct === null ? '' : `${row.pct >= 0 ? '+' : ''}${fmt(row.pct)}%`}</span>
                </span>
              </Link>
            ))
          )}

          <div className="menu-sep" />
          <Link
            className="sa-search-item"
            to={`/search${trimmed ? `?q=${encodeURIComponent(debounced)}` : ''}`}
            onClick={() => setOpen(false)}
          >
            <span className="t-3 fs-12">查看全部搜索结果 →</span>
          </Link>
        </div>
      ) : null}
    </div>
  );
}
