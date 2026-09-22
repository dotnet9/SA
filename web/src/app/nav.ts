/**
 * 导航定义。逐条对应原型 `design/web/_shared/app.js` 的 `NAV` / `NAV_MORE`，
 * 以及 `design/web/_shared/stock-panels.js` 的 `TABS`。
 *
 * 信息架构（用户愿景）：
 *   左侧只有两项——大盘概况、自选股；其余功能收进底部可折叠的「更多」。
 *   个股信息在右侧以 Tab 呈现（9 个），点列表任一行即内联打开。
 */

/** 单个导航项。 */
export interface NavItem {
  /** 用于高亮的键。 */
  key: string;
  text: string;
  icon: string;
  /** 路由路径。 */
  path: string;
  /** 所需功能点；null 表示登录即可见（如个人设置）。 */
  fp: string | null;
}

/** 主导航项：只有两项。 */
export const PrimaryNav: NavItem[] = [
  { key: 'market', text: '大盘概况', icon: '▦', path: '/market', fp: 'market.view' },
  { key: 'watchlist', text: '自选股', icon: '★', path: '/watchlist', fp: 'watchlist.view' }
];

/**
 * 「更多」区：其余功能，默认收起。
 *
 * 保留原有分组顺序（选股与提醒 → 系统），但不再分组展示——一个折叠区里放平更省地方。
 */
export const MoreNav: NavItem[] = [
  { key: 'screener', text: '条件选股器', icon: '⚙', path: '/screener', fp: 'screener.use' },
  { key: 'alerts', text: '提醒规则', icon: '◔', path: '/alerts', fp: 'alert.manage' },
  { key: 'notifications', text: '通知中心', icon: '◍', path: '/notifications', fp: 'notify.view' },
  { key: 'search', text: '股票搜索', icon: '⌕', path: '/search', fp: 'stock.search' },
  { key: 'topology', text: '四种拓扑图', icon: '⁂', path: '/topology', fp: 'topology.view' },
  { key: 'prosperity', text: '行业景气度', icon: '◮', path: '/prosperity', fp: 'stock.industry' },
  { key: 'settings', text: '个人设置', icon: '⚒', path: '/settings', fp: null },
  { key: 'admin-users', text: '用户管理', icon: '☰', path: '/admin/users', fp: 'admin.users' },
  { key: 'admin-permissions', text: '角色与权限', icon: '⛨', path: '/admin/permissions', fp: 'admin.permissions' },
  { key: 'admin-datasource', text: '数据源监控', icon: '◱', path: '/admin/datasource', fp: 'admin.datasource' },
  { key: 'admin-security', text: '登录与安全', icon: '⛭', path: '/admin/security', fp: 'admin.security' }
];

/**
 * 个股页的 9 个 Tab（与原型 `stock-panels.js` 的 `TABS` 一一对应）。
 *
 * 原来是 9 个独立模块页 + 5 个收编 Tab，现在改成 9 个 Tab 同页切换：
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

/** 个股模块对应的功能点（用于按权限隐藏 Tab）。 */
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

/**
 * 公开功能点：对应接口匿名即可访问，因此前端也不得据此拦截。
 *
 * 必须与后端 `SA.Domain.Authorization.FunctionPointCatalog.PublicCodes` 保持一致
 * （那里是唯一事实来源）。前端这份拷贝只用于「菜单显隐」。
 */
export const PublicFunctionPoints: readonly string[] = [
  'market.view',
  'stock.search',
  'stock.trend',
  'stock.finance',
  'stock.equity',
  'stock.capital',
  'stock.industry',
  'stock.events',
  'stock.risk',
  'stock.rating',
  'topology.view'
];

/** 导航项是否公开（不登录即可访问）。 */
export function isPublicNavItem(item: NavItem): boolean {
  return item.fp !== null && PublicFunctionPoints.includes(item.fp);
}

/** 导航项是否需要登录：公开项不需要，其余（含「登录即可见」）都需要。 */
export function requiresLogin(item: NavItem): boolean {
  return !isPublicNavItem(item);
}

/** 全部导航项（扁平）。 */
export const AllNavItems: NavItem[] = [...PrimaryNav, ...MoreNav];

/** 导航裁剪结果。 */
export interface NavVisibility {
  /** 可见的主导航项。 */
  primary: NavItem[];
  /** 可见的「更多」项。 */
  more: NavItem[];
  /** 因当前角色功能点不足而隐藏的项数。 */
  hidden: number;
  /** 因未登录而锁定显示的项数。 */
  locked: number;
}

/**
 * 按登录态与功能点裁剪导航。
 *
 * 与原型 `renderNav()` 的取舍不同之处：**未登录时不再把需登录的菜单藏起来，而是锁定显示**。
 * 藏起来会让访客无从知道这个站有什么，锁定显示则保留入口并明确告知「登录后可用」。
 *
 * @param functionPoints 当前角色的功能点；null 表示未登录。
 */
export function filterNav(functionPoints: readonly string[] | null): NavVisibility {
  const anonymous = functionPoints === null;
  let hidden = 0;
  let locked = 0;

  const keep = (items: NavItem[]): NavItem[] => {
    const out: NavItem[] = [];

    for (const item of items) {
      if (isPublicNavItem(item)) {
        out.push(item);
        continue;
      }

      if (anonymous) {
        locked += 1;
        out.push(item);
        continue;
      }

      if (item.fp === null || functionPoints.includes(item.fp)) {
        out.push(item);
        continue;
      }

      hidden += 1;
    }

    return out;
  };

  return {
    primary: keep(PrimaryNav),
    more: keep(MoreNav),
    hidden,
    locked
  };
}

/**
 * 登录后的落地路由：管理员进后台用户管理，其余进大盘概况。
 * 若目标无权限则顺延到第一个可见入口。
 */
export function landingPath(functionPoints: readonly string[]): string {
  const preferred = functionPoints.includes('admin.users') ? '/admin/users' : '/market';
  const allowed = AllNavItems.filter(
    (item) => isPublicNavItem(item) || item.fp === null || functionPoints.includes(item.fp)
  );

  if (allowed.some((item) => item.path === preferred)) {
    return preferred;
  }

  return allowed[0]?.path ?? '/settings';
}

/** 未登录时的落地路由：公开首页（大盘概况）。 */
export const AnonymousLandingPath = '/market';

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
  { key: 'market', text: '大盘', icon: '▦', path: '/m', fp: 'market.view' },
  { key: 'watchlist', text: '自选', icon: '★', path: '/m/watchlist', fp: 'watchlist.view' },
  { key: 'more', text: '更多', icon: '☰', path: '/m/more', fp: null }
];
