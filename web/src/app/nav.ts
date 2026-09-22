/**
 * 导航定义。逐条对应原型 `design/web/_shared/app.js` 的 `NAV` / `NAV_MORE`，
 * 以及 `design/web/_shared/stock-panels.js` 的 `TABS`。
 *
 * 信息架构（用户愿景）：
 *   左侧只有两项——大盘概况、自选股；其余功能收进底部可折叠的「更多」。
 *   个股信息在右侧以 Tab 呈现（9 个），点列表任一行即内联打开。
 *
 * 本应用**不做权限控制**：没有登录、没有角色、没有功能点。
 * 原先按功能点裁剪菜单的逻辑随之删除（`can()` 恒真，裁剪只会白写一遍）。
 */

/** 单个导航项。 */
export interface NavItem {
  /** 用于高亮的键。 */
  key: string;
  text: string;
  icon: string;
  /** 路由路径。 */
  path: string;
}

/** 主导航项：只有两项。 */
export const PrimaryNav: NavItem[] = [
  { key: 'market', text: '大盘概况', icon: '▦', path: '/market' },
  { key: 'watchlist', text: '自选股', icon: '★', path: '/watchlist' }
];

/**
 * 「更多」区：其余功能，默认收起。
 *
 * 用户与权限三页（用户管理 / 角色与权限 / 登录与安全）已随「去掉登录」删除；
 * 保留「数据源监控」——它展示采集任务与上游连通性，是运维信息，与权限无关。
 */
export const MoreNav: NavItem[] = [
  { key: 'screener', text: '条件选股器', icon: '⚙', path: '/screener' },
  { key: 'alerts', text: '提醒规则', icon: '◔', path: '/alerts' },
  { key: 'notifications', text: '通知中心', icon: '◍', path: '/notifications' },
  { key: 'search', text: '股票搜索', icon: '⌕', path: '/search' },
  { key: 'topology', text: '四种拓扑图', icon: '⁂', path: '/topology' },
  { key: 'prosperity', text: '行业景气度', icon: '◮', path: '/prosperity' },
  { key: 'settings', text: '设置', icon: '⚒', path: '/settings' },
  { key: 'admin-datasource', text: '数据源监控', icon: '◱', path: '/admin/datasource' }
];

/**
 * 个股页的 9 个 Tab（与原型 `stock-panels.js` 的 `TABS` 一一对应）。
 *
 * 原来是 9 个独立模块页，现在改成 9 个 Tab 同页切换：
 * 模块路径仍然可达（`/stock/:code/trend` 会打开对应 Tab），分享链接不破坏。
 */
export const StockTabKeys = [
  'overview',
  'trend',
  'finance',
  'equity',
  'capital',
  'industry',
  'events',
  'risk',
  'rating'
] as const;

/** Tab 名称。 */
export const StockTabNames: Record<string, string> = {
  overview: '概览',
  trend: '趋势',
  finance: '财务',
  equity: '股权',
  capital: '资金',
  industry: '行业',
  events: '事件',
  risk: '风险',
  rating: '评级'
};

/** 旧模块别名 → 新 Tab 键（`causal` 曾是一个独立模块，现并入「事件」）。 */
const TabAliases: Record<string, string> = { causal: 'events' };

/** 由任意模块名解析 Tab 键；未知值回退到第一个 Tab（不报 404）。 */
export function tabOfModule(module: string | undefined): string {
  if (!module) {
    return StockTabKeys[0];
  }

  const key = TabAliases[module] ?? module;
  return (StockTabKeys as readonly string[]).includes(key) ? key : StockTabKeys[0];
}

/** 尚未选择股票时的兜底代码（与原型主演示股一致）。 */
export const DefaultStockCode = '300750';

/** 全部导航项（扁平）。 */
export const AllNavItems: NavItem[] = [...PrimaryNav, ...MoreNav];

/**
 * 判断导航项是否处于选中态。
 *
 * 个股页（`/stock/...`）统一高亮「大盘概况」——个股是从大盘或自选点进去的，
 * 高亮回到入口所在的那一项更符合直觉。
 */
export function isNavItemActive(item: NavItem, pathname: string): boolean {
  if (pathname === item.path || pathname.startsWith(`${item.path}/`)) {
    return true;
  }

  return item.key === 'market' && pathname.startsWith('/stock');
}

/**
 * 移动端底部 3 个 Tab（大盘 / 自选 / 更多）。
 *
 * 与桌面端一致：两项为主，其余收进「更多」。
 */
export const MobileTabs: NavItem[] = [
  { key: 'market', text: '大盘', icon: '▦', path: '/m' },
  { key: 'watchlist', text: '自选', icon: '★', path: '/m/watchlist' },
  { key: 'more', text: '更多', icon: '☰', path: '/m/more' }
];
