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
  value: '价值研究',
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

/**
 * 个股页的 5 个 Tab（实施计划 §6.2）。
 *
 * 原 9 个模块页收编为 5 个 Tab，**不删除任何功能**：
 * 旧模块路径仍可直达（`/stock/:code/trend` 打开 Tab 1 并展开技术段），
 * 因此分享链接与浏览器前进后退不破坏。
 */
export const StockTabKeys = ['value', 'fundamental', 'capital', 'risk', 'institution'] as const;

/** Tab 名称。 */
export const StockTabNames: Record<string, string> = {
  value: '价值研究',
  fundamental: '基本面',
  capital: '筹码与资金',
  risk: '风险与事件',
  institution: '机构观点'
};

/**
 * 每个 Tab 收编的模块（第一个是该 Tab 的默认落点）。
 *
 * `value` 的第一个元素是新的价值研究页，其余 Tab 落到原模块页。
 */
export const StockTabModules: Record<string, string[]> = {
  value: ['value', 'overview', 'trend'],
  fundamental: ['finance', 'industry'],
  capital: ['equity', 'capital'],
  risk: ['risk', 'events', 'causal'],
  institution: ['rating']
};

/** 旧模块 → 所属 Tab。 */
export const StockTabOfModule: Record<string, string> = Object.fromEntries(
  Object.entries(StockTabModules).flatMap(([tab, modules]) => modules.map((module) => [module, tab]))
);

/** 由模块解析所属 Tab；未知模块回退到第一个 Tab（不报 404）。 */
export function tabOfModule(module: string | undefined): string {
  if (!module) {
    return StockTabKeys[0];
  }

  return StockTabOfModule[module] ?? StockTabKeys[0];
}

/** 尚未选择股票时的兜底代码（与原型主演示股一致）。 */
export const DefaultStockCode = '300750';

/**
 * 公开功能点：对应接口匿名即可访问，因此前端也不得据此拦截。
 *
 * 必须与后端 `SA.Domain.Authorization.FunctionPointCatalog.PublicCodes` 保持一致
 * （那里是唯一事实来源）。前端这份拷贝只用于「菜单显隐」：
 * 后端已经放开这些接口，若菜单还按功能点隐藏，就会出现
 * 「有权限但看不到入口」的怪象——受限角色明明能读到财务数据，菜单里却没有那一项。
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

/** 导航裁剪结果。 */
export interface NavVisibility {
  /** 可见的导航分组（含锁定项）。 */
  groups: NavGroup[];
  /** 因当前角色功能点不足而隐藏的项数（登录后才会出现）。 */
  hidden: number;
  /** 因未登录而锁定显示的项数（登录后才能进入）。 */
  locked: number;
}

/**
 * 按登录态与功能点裁剪导航分组。与原型 `renderNav()` 的取舍不同之处在于
 * <b>未登录时不再把需登录的菜单藏起来，而是锁定显示</b>：
 * 藏起来会让访客无从知道这个站有什么，锁定显示则在保留入口的同时明确告知「登录后可用」。
 *
 * @param functionPoints 当前角色的功能点；<c>null</c> 表示未登录。
 */
export function filterNav(functionPoints: readonly string[] | null): NavVisibility {
  const anonymous = functionPoints === null;
  let hidden = 0;
  let locked = 0;

  const groups = Nav.map((group) => {
    const items: NavItem[] = [];

    for (const item of group.items) {
      // 公开项对所有人可见：后端已放开这些接口，隐藏只会造成「有权限却看不到入口」
      if (isPublicNavItem(item)) {
        items.push(item);
        continue;
      }

      if (anonymous) {
        // 需登录：锁定显示，点击回登录页
        locked += 1;
        items.push(item);
        continue;
      }

      if (item.fp === null || functionPoints.includes(item.fp)) {
        items.push(item);
        continue;
      }

      hidden += 1;
    }

    return { group: group.group, items };
  }).filter((group) => group.items.length > 0);

  return { groups, hidden, locked };
}

/**
 * 登录后的落地路由：管理员进后台用户管理，其余进市场概览。
 * 与原型 `index.html` 的跳转规则一致（admin → admin-users，其他 → market），
 * 但若目标无权限则顺延到第一个可见入口。
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

/** 未登录时的落地路由：公开首页（市场概览）。 */
export const AnonymousLandingPath = '/market';

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
