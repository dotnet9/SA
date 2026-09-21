namespace SA.Domain.Authorization;

/// <summary>
/// 功能点全集（7 组）。内容逐条对应 docs/需求规格.md §7.1 与原型
/// <c>design/web/_shared/data.js:847-890</c> 的 <c>functionPoints</c>，是授权判定与后台权限矩阵的唯一事实来源。
/// </summary>
/// <remarks>
/// 注意文档内部的不一致：需求规格 §7.1 标题写「24 项」、§7.2 写角色预设「24 / 20 / 4」，
/// 但其表格与原型数组实际列出 <b>28</b> 条编码（管理员 28 / 普通用户 20 / 访客 4）。
/// 实现以表格与原型数组为准（不丢任何编码），数量口径待文档确认后统一。
/// </remarks>
public static class FunctionPointCatalog
{
    /// <summary>市场概览。</summary>
    public const string MarketView = "market.view";

    /// <summary>股票搜索。</summary>
    public const string StockSearch = "stock.search";

    /// <summary>趋势与价格结构。</summary>
    public const string StockTrend = "stock.trend";

    /// <summary>盈利与财务表现。</summary>
    public const string StockFinance = "stock.finance";

    /// <summary>公司投资与股权结构。</summary>
    public const string StockEquity = "stock.equity";

    /// <summary>资金面与筹码。</summary>
    public const string StockCapital = "stock.capital";

    /// <summary>行业与同业对比。</summary>
    public const string StockIndustry = "stock.industry";

    /// <summary>事件时间线与影响。</summary>
    public const string StockEvents = "stock.events";

    /// <summary>风险与舆情监控。</summary>
    public const string StockRisk = "stock.risk";

    /// <summary>机构评级与盈利预测。</summary>
    public const string StockRating = "stock.rating";

    /// <summary>拓扑图总览。</summary>
    public const string TopologyView = "topology.view";

    /// <summary>查看自选股。</summary>
    public const string WatchlistView = "watchlist.view";

    /// <summary>编辑自选股。</summary>
    public const string WatchlistEdit = "watchlist.edit";

    /// <summary>使用条件选股器。</summary>
    public const string ScreenerUse = "screener.use";

    /// <summary>保存选股策略。</summary>
    public const string ScreenerSaveStrategy = "screener.saveStrategy";

    /// <summary>管理提醒规则。</summary>
    public const string AlertManage = "alert.manage";

    /// <summary>查看通知中心。</summary>
    public const string NotifyView = "notify.view";

    /// <summary>全市场数据范围。</summary>
    public const string DataScopeAll = "data.scope.all";

    /// <summary>仅自选数据范围。</summary>
    public const string DataScopeWatchlist = "data.scope.watchlist";

    /// <summary>导出数据。</summary>
    public const string ExportData = "export.data";

    /// <summary>历史数据年限。</summary>
    public const string HistoryYears = "history.years";

    /// <summary>单日查询上限。</summary>
    public const string QuotaDaily = "quota.daily";

    /// <summary>编辑事件标注。</summary>
    public const string EventEdit = "event.edit";

    /// <summary>共享策略。</summary>
    public const string StrategyShare = "strategy.share";

    /// <summary>用户管理。</summary>
    public const string AdminUsers = "admin.users";

    /// <summary>角色与权限。</summary>
    public const string AdminPermissions = "admin.permissions";

    /// <summary>数据源监控。</summary>
    public const string AdminDatasource = "admin.datasource";

    /// <summary>登录与安全。</summary>
    public const string AdminSecurity = "admin.security";

    /// <summary>
    /// 全部功能点分组，顺序即权限矩阵的展示顺序。
    /// </summary>
    /// <remarks>
    /// <b>整组标记为公开</b>：需求要求「公开市场数据不登录也能看」，而「模块访问」一组
    /// （大盘、搜索、个股各模块、拓扑）全是公开数据，对应接口匿名即可读取。
    /// 因此这些功能点<b>不参与授权判定</b>——它们仍然保留在矩阵里，但会明确标注为「公开」，
    /// 而不是做成点了没反应的开关（见 <see cref="PublicCodes"/>）。
    /// </remarks>
    public static IReadOnlyList<FunctionPointGroup> Groups { get; } =
    [
        new("模块访问",
        [
            new(MarketView, "市场概览", "查看大盘指数、涨跌家数、行业排行"),
            new(StockSearch, "股票搜索", "按代码、名称、拼音搜索股票"),
            new(StockTrend, "趋势与价格结构", "K 线、均线、MACD、相对强弱"),
            new(StockFinance, "盈利与财务表现", "营收、利润、毛利、ROE、现金流"),
            new(StockEquity, "公司投资与股权结构", "股权拓扑、股东、机构持仓、质押"),
            new(StockCapital, "资金面与筹码", "主力资金、北向、两融、龙虎榜、筹码分布"),
            new(StockIndustry, "行业与同业对比", "产业链拓扑、同业对比、估值分位"),
            new(StockEvents, "事件时间线与影响", "四种拓扑图与事件表"),
            new(StockRisk, "风险与舆情监控", "风险矩阵、告警、舆情情绪"),
            new(StockRating, "机构评级与盈利预测", "评级分布、目标价、一致预期"),
            new(TopologyView, "拓扑图总览", "四种拓扑图集中对照页")
        ], IsPublic: true),
        new("自选与选股",
        [
            new(WatchlistView, "查看自选股", "查看自选股盯盘列表"),
            new(WatchlistEdit, "编辑自选股", "添加、删除、分组、排序"),
            new(ScreenerUse, "使用条件选股器", "组合条件筛选全市场"),
            new(ScreenerSaveStrategy, "保存选股策略", "保存与载入个人策略")
        ]),
        new("提醒与通知",
        [
            new(AlertManage, "管理提醒规则", "新建、编辑、启停提醒规则"),
            new(NotifyView, "查看通知中心", "查看站内通知与历史提醒")
        ]),
        new("数据范围",
        [
            new(DataScopeAll, "全市场数据", "可查询全市场任意股票"),
            new(DataScopeWatchlist, "仅自选数据", "仅可查询自选股范围内的股票")
        ]),
        new("操作权限",
        [
            new(ExportData, "导出数据", "导出表格与图表为 CSV / PNG"),
            new(HistoryYears, "历史数据年限", "可查看的历史数据最大年限"),
            new(QuotaDaily, "单日查询上限", "单日可发起的分析查询次数上限")
        ]),
        new("编辑权限",
        [
            new(EventEdit, "编辑事件标注", "修改事件影响方向与强度"),
            new(StrategyShare, "共享策略", "将选股策略共享给其他用户")
        ]),
        new("后台管理",
        [
            new(AdminUsers, "用户管理", "新增、禁用、重置密码、分配角色"),
            new(AdminPermissions, "角色与权限", "配置角色与功能点开关"),
            new(AdminDatasource, "数据源监控", "查看采集状态、手工触发、重试"),
            new(AdminSecurity, "登录与安全", "登录日志、在线会话、锁定策略")
        ])
    ];

    /// <summary>
    /// 扁平化的全部功能点编码，顺序与 <see cref="Groups"/> 一致。
    /// </summary>
    public static IReadOnlyList<string> AllCodes { get; } =
        Groups.SelectMany(g => g.Items).Select(i => i.Code).ToArray();

    /// <summary>
    /// 公开功能点：对应接口匿名即可访问，因此不参与授权判定。
    /// </summary>
    /// <remarks>
    /// 它们仍然出现在权限矩阵里（用户需要知道有哪些模块），但界面必须标注为「公开」，
    /// 否则会出现「关掉开关却依然能访问」的假开关。
    /// </remarks>
    public static IReadOnlySet<string> PublicCodes { get; } =
        Groups.Where(g => g.IsPublic)
            .SelectMany(g => g.Items)
            .Select(i => i.Code)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// 真正参与授权判定的功能点（公开面之外的）。
    /// </summary>
    public static IReadOnlyList<string> GatedCodes { get; } =
        AllCodes.Where(code => !PublicCodes.Contains(code)).ToArray();

    /// <summary>
    /// 编码到功能点的索引，用于校验与展示。
    /// </summary>
    public static IReadOnlyDictionary<string, FunctionPoint> ByCode { get; } =
        Groups.SelectMany(g => g.Items)
            .Select(i => i with { IsPublic = PublicCodes.Contains(i.Code) })
            .ToDictionary(i => i.Code, StringComparer.Ordinal);

    /// <summary>
    /// 判断编码是否为受支持的功能点。
    /// </summary>
    public static bool Contains(string code) => ByCode.ContainsKey(code);

    /// <summary>
    /// 判断功能点是否属于公开面（不登录即可访问）。
    /// </summary>
    public static bool IsPublic(string code) => PublicCodes.Contains(code);
}
