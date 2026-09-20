import { useLocation } from 'react-router';
import { AllNavItems } from '@/app/nav';

/**
 * 尚未实现的页面占位。
 *
 * 实施期按批次推进（实施计划 §8），本批只落地外壳、登录与权限裁剪；
 * 明细页的数据接口在各自批次实现。这里明确写出「属于第几批、需要哪些功能点」，
 * 而不是给出假数据或空白页。
 */
export function PlaceholderPage({ title, batch, requires }: { title: string; batch: string; requires?: string[] }) {
  const location = useLocation();
  const navItem = AllNavItems.find((item) => location.pathname.startsWith(item.path));

  return (
    <>
      <div className="sa-pagehead">
        <div>
          <h1>{title}</h1>
          <div className="sub">
            路由 <code>{location.pathname}</code>
            {navItem ? ` · 导航项「${navItem.text}」` : ''}
          </div>
        </div>
      </div>

      <div className="placeholder-note">
        <div>
          <strong>本页内容在{batch}实现。</strong>
        </div>
        <div>
          当前批次已完成：应用外壳、登录与会话、功能点授权与前端裁剪（导航按角色实时隐藏）、
          主题与涨跌色、数据层与元数据迁移。本页所需的数据接口尚未接入，因此不展示任何数值。
        </div>
        <div>
          所需功能点：{(requires ?? [navItem?.fp ?? '登录即可']).map((fp) => (
            <span key={fp} className="tag tag-outline" style={{ marginRight: 6 }}>
              {fp}
            </span>
          ))}
        </div>
      </div>
    </>
  );
}
