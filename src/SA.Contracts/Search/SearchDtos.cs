namespace SA.Contracts.Search;

/// <summary>
/// 搜索结果行。对应原型 <c>design/web/search-results.html</c> 与
/// <c>design/web/_shared/data.js</c> 的 <c>stocks</c> 条目字段。
/// </summary>
/// <param name="Code">证券代码。</param>
/// <param name="Name">证券名称。</param>
/// <param name="Py">拼音首字母（小写）。</param>
/// <param name="Board">板块：沪市主板 / 深市主板 / 创业板 / 科创板 / 北交所。</param>
/// <param name="Industry">东财行业。</param>
/// <param name="Price">最新价（元）；无快照时为 null。</param>
/// <param name="Chg">涨跌额（元）。</param>
/// <param name="Pct">涨跌幅（百分数）。</param>
/// <param name="VolRatio">量比。</param>
/// <param name="Turnover">换手率（百分数）。</param>
/// <param name="Pe">市盈率（动态）。</param>
/// <param name="Pb">市净率。</param>
/// <param name="Cap">总市值（亿元）。</param>
/// <param name="IsSt">是否 ST / 退市风险标的。</param>
/// <param name="MatchedBy">命中方式：code / name / pinyin / industry，供界面解释排序。</param>
public sealed record SearchRowDto(
    string Code,
    string Name,
    string? Py,
    string Board,
    string? Industry,
    decimal? Price,
    decimal? Chg,
    decimal? Pct,
    decimal? VolRatio,
    decimal? Turnover,
    decimal? Pe,
    decimal? Pb,
    decimal? Cap,
    bool IsSt,
    string MatchedBy);

/// <summary>
/// 搜索响应。
/// </summary>
/// <param name="Query">原始查询串。</param>
/// <param name="Total">命中总数（未分页前）。</param>
/// <param name="Page">当前页（1 起）。</param>
/// <param name="PageSize">每页条数。</param>
/// <param name="Rows">结果行。</param>
/// <param name="ScopeNote">口径说明：数据范围受限时的提示文案。</param>
public sealed record SearchResultDto(
    string Query,
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<SearchRowDto> Rows,
    string? ScopeNote);

/// <summary>
/// 搜索分页与过滤参数。
/// </summary>
/// <param name="Q">查询串：代码 / 名称 / 拼音首字母 / 行业关键词。</param>
/// <param name="Board">板块过滤。</param>
/// <param name="Industry">行业过滤。</param>
/// <param name="Page">页码，1 起。</param>
/// <param name="PageSize">每页条数，默认 20，最大 200（详细设计 §1.3）。</param>
public sealed record SearchQuery(
    string? Q,
    string? Board = null,
    string? Industry = null,
    int Page = 1,
    int PageSize = 20);
