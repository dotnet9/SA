import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router';

/**
 * 全局搜索。交互与原型的顶栏搜索一致：⌘/Ctrl+K 或 `/` 唤起、
 * 输入即开面板、Esc 关闭、点击面板外收起。
 *
 * 数据侧在第 2 批接入 `/api/search`（代码 / 名称 / 拼音首字母 / 行业），
 * 本批只落交互与骨架，面板内明确标注尚未接入，避免给出假结果。
 */
export function GlobalSearch() {
  const [keyword, setKeyword] = useState('');
  const [open, setOpen] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const navigate = useNavigate();

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
            navigate(`/search?q=${encodeURIComponent(keyword)}`);
            setOpen(false);
          }
        }}
      />
      <span className="sa-search-kbd">
        <kbd>⌘</kbd>
        <kbd>K</kbd>
      </span>

      {open ? (
        <div ref={panelRef} className="sa-search-panel is-open">
          <div className="sa-search-group">{keyword.trim() ? `匹配「${keyword.trim()}」` : '热门搜索'}</div>
          <div className="hint" style={{ padding: '10px 12px', lineHeight: 1.8 }}>
            搜索结果接口在第 2 批接入（<code>/api/search</code>）。
            <br />
            当前可用：输入代码或名称后回车，进入搜索结果页。
          </div>
        </div>
      ) : null}
    </div>
  );
}
