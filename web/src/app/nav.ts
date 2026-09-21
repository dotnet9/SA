/**
 * 导航定义。逐条对应原型 `design/web/_shared/app.js` 的 `NAV` 数组
 * （分组名、顺序、图标字符、功能点编码完全一致），
 * 差异只有一处：原型的 href 是 html 文件名，这里是路由路径。
 */

/** 单个导航项。 */
export interface NavItem {
  /** 用于高亮的键，与原型 `data-page` 对应。 */
  key: string;
  text: string;
  icon: string;
  /** 路由前缀。个股模块统一为 `/stock`，实际目标由当前股票代码拼出。 */
  path: string;
  /** 所需功能点；null 表示登录即可见（如个人设置）。 */
  fp: string | null;
  /**
   * 个股模块 Key（仅个股矩阵项有值）。
   * 原型的模块页没有代码参数（固定展示「焦点股」），实现版把代码放进 URL，
   * 因此导航项只声明模块，由 {@link navTarget} 结合当前股票拼出完整路径。
   */
  module?: string;
}

/** 导航分组。 */
export interface NavGroup {
  group: string;
  items: NavItem[];
}

/** 个股矩阵 8 个模块的 Key，供个股页标签栏复用。 */
export const StockModuleKeys = [
  'trend',
  'finance',
  'equity',
  'capital',
  'industry',
  'events',
  'risk',
  'rating'
] as const;

/** 个股模块的中文名，用于标签栏与页面标题。 */
export const StockModuleNames: Record<string, string> = {
  overview: '矩阵总览',
  trend: '趋势与价格结构',
  finance: '盈利与财务表现',
  equity: '投资与股权结构',
  capital: '资金面与筹码',
  industry: '行业与同业对比',
  events: '事件时间线与影响',
  risk: '风险与舆情监控',
  rating: '机构评级与预测'
};

/** 个股模块对应的功能点。 */
export const StockModuleFunctionPoints: Record<string, string> = {
  overview: 'stock.trend',
  trend: 'stock.trend',
  finance: 'stock.finance',
  equity: 'stock.equity',
  capital: 'stock.capital',
  industry: 'stock.industry',
  events: 'stock.events',
  risk: 'stock.risk',
  rating: 'stock.rating'
};

/** 尚未选择股票时的兜底代码（与原型主演示股一致）。 */
export const DefaultStockCode = '300750';

/** 全部导航分组。 */
export const Nav: NavGroup[] = [
  {
    group: '总览',
    items: [
      { key: 'market', text: '市场概览', icon: '▦', path: '/market', fp: 'market.view' },
      { key: 'search', text: '股票搜索', icon: '⌕', path: '/search', fp: 'stock.search' },
      { key: 'watchlist', text: '自选股盯盘', icon: '★', path: '/watchlist', fp: 'watchlist.view' }
    ]
  },
  {
    group: '个股分析矩阵',
    items: [
      { key: 'stock', text: '个股总览', icon: '◉', path: '/stock', fp: 'stock.trend', module: 'overview' },
      { key: 'stock-trend', text: '趋势与价格结构', icon: '◪', path: '/stock', fp: 'stock.trend', module: 'trend' },
      { key: 'stock-finance', text: '盈利与财务表现', icon: '▤', path: '/stock', fp: 'stock.finance', module: 'finance' },
      { key: 'stock-equity', text: '投资与股权结构', icon: '⛓', path: '/stock', fp: 'stock.equity', module: 'equity' },
      { key: 'stock-capital', text: '资金面与筹码', icon: '◐', path: '/stock', fp: 'stock.capital', module: 'capital' },
      { key: 'stock-industry', text: '行业与同业对比', icon: '◫', path: '/stock', fp: 'stock.industry', module: 'industry' },
      { key: 'stock-events', text: '事件时间线与影响', icon: '◈', path: '/stock', fp: 'stock.events', module: 'events' },
      { key: 'stock-causal', text: '因果链与传导带宽', icon: '⇄', path: '/stock', fp: 'stock.trend', module: 'causal' },
      { key: 'stock-risk', text: '风险与舆情监控', icon: '⚠', path: '/stock', fp: 'stock.risk', module: 'risk' },
      { key: 'stock-rating', text: '机构评级与预测', icon: '◎', path: '/stock', fp: 'stock.rating', module: 'rating' }
    ]
  },
  {
    group: '拓扑图',
    items: [
      { key: 'topology', text: '四种拓扑图总览', icon: '⁂', path: '/topology', fp: 'topology.view' },
      { key: 'prosperity', text: '行业景气度', icon: '◮', path: '/prosperity', fp: 'stock.industry' }
    ]
  },
  {
    group: '选股与提醒',
    items: [
      { key: 'screener', text: '条件选股器', icon: '⚙', path: '/screener', fp: 'screener.use' },
      { key: 'alerts', text: '提醒规则', icon: '◔', path: '/alerts', fp: 'alert.manage' },
      { key: 'notifications', text: '通知中心', icon: '◍', path: '/notifications', fp: 'notify.view' }
    ]
  },
  {
    group: '系统',
    items: [
      { key: 'settings', text: '个人设置', icon: '⚒', path: '/settings', fp: null },
      { key: 'admin-users', text: '用户管理', icon: '☰', path: '/admin/users', fp: 'admin.users' },
      { key: 'admin-permissions', text: '角色与权限', icon: '⛨', path: '/admin/permissions', fp: 'admin.permissions' },
      { key: 'admin-datasource', text: '数据源监控', icon: '◱', path: '/admin/datasource', fp: 'admin.datasource' },
      { key: 'admin-security', text: '登录与安全', icon: '⛭', path: '/admin/security', fp: 'admin.security' }
    ]
  }
];

/**
 * 移动端底部 5 个 Tab（需求规格 §8.3：市场 / 自选 / 选股 / 通知 / 我的）。
 */
export const MobileTabs: NavItem[] = [
  { key: 'market', text: '市场', icon: '▦', path: '/market', fp: 'market.view' },
  { key: 'watchlist', text: '自选', icon: '★', path: '/watchlist', fp: 'watchlist.view' },
  { key: 'screener', text: '选股', icon: '⚙', path: '/screener', fp: 'screener.use' },
  { key: 'notifications', text: '通知', icon: '◍', path: '/notifications', fp: 'notify.view' },
  { key: 'settings', text: '我的', icon: '⚒', path: '/settings', fp: null }
];

/** 全部导航项（扁平）。 */
export const AllNavItems: NavItem[] = Nav.flatMap((group) => group.items);

/**
 * 按功能点裁剪导航分组。与原型 `renderNav()` 行为一致：
 * 组内无可见项则整组不渲染，并返回被隐藏的数量用于提示文案。
 */
export function filterNav(functionPoints: readonly string[]): { groups: NavGroup[]; hidden: number } {
  let hidden = 0;

  const groups = Nav.map((group) => {
    const items = group.items.filter((item) => item.fp === null || functionPoints.includes(item.fp));
    hidden += group.items.length - items.length;
    return { group: group.group, items };
  }).filter((group) => group.items.length > 0);

  return { groups, hidden };
}

/**
 * 登录后的落地路由：管理员进后台用户管理，其余进市场概览。
 * 与原型 `index.html` 的跳转规则一致（admin → admin-users，其他 → market），
 * 但若目标无权限则顺延到第一个可见入口。
 */
export function landingPath(functionPoints: readonly string[]): string {
  const preferred = functionPoints.includes('admin.users') ? '/admin/users' : '/market';
  const allowed = AllNavItems.filter((item) => item.fp === null || functionPoints.includes(item.fp));

  if (allowed.some((item) => item.path === preferred)) {
    return preferred;
  }

  return allowed[0]?.path ?? '/settings';
}

/**
 * 导航项的实际目标地址。个股模块需要拼上当前股票代码
 * （原型固定展示焦点股，实现版把代码放进 URL，便于分享与刷新）。
 */
export function navTarget(item: NavItem, stockCode: string): string {
  if (!item.module) {
    return item.path;
  }

  return item.module === 'overview' ? `/stock/${stockCode}` : `/stock/${stockCode}/${item.module}`;
}

/**
 * 判断导航项是否处于选中态。个股模块按「代码 + 模块」精确匹配，
 * 而不是简单的前缀匹配，否则 9 个模块会同时高亮。
 */
export function isNavItemActive(item: NavItem, pathname: string): boolean {
  if (!item.module) {
    return pathname === item.path || pathname.startsWith(`${item.path}/`);
  }

  const segments = pathname.split('/').filter(Boolean);
  if (segments[0] !== 'stock' || segments.length < 2) {
    return false;
  }

  const module = segments[2] ?? 'overview';
  return module === item.module;
}
