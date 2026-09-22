import { useState } from 'react';
import { CaliberGroups } from './caliber';

/**
 * 数据口径抽屉。
 *
 * 页面正文不放解释性文字（用户要求「不要显示太多的文字」），
 * 所有口径说明集中到这里，从顶栏的 ⓘ 按钮打开，任意页可用。
 *
 * 字段级口径仍用卡片上的 hover 问号标注，不占版面。
 */
export function CaliberDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [expanded, setExpanded] = useState<string | null>(CaliberGroups[0]?.title ?? null);

  if (!open) {
    return null;
  }

  return (
    <div className="overlay is-open" onClick={onClose}>
      <aside className="drawer spec-drawer" onClick={(event) => event.stopPropagation()}>
        <div className="drawer-head">
          <span className="card-title">数据口径</span>
          <button type="button" className="icon-btn" onClick={onClose} title="关闭">
            ✕
          </button>
        </div>

        <div className="drawer-body">
          {CaliberGroups.map((group) => {
            const isOpen = expanded === group.title;

            return (
              <div key={group.title} className="spec-group">
                <button
                  type="button"
                  className="spec-group-title"
                  onClick={() => setExpanded(isOpen ? null : group.title)}
                >
                  <span>{isOpen ? '▾' : '▸'}</span>
                  {group.title}
                  <span className="fs-11 t-3">{group.items.length}</span>
                </button>

                {isOpen ? (
                  <ul>
                    {group.items.map((item) => (
                      <li key={item}>{item}</li>
                    ))}
                  </ul>
                ) : null}
              </div>
            );
          })}
        </div>
      </aside>
    </div>
  );
}
