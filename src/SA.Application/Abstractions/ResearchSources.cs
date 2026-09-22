using SA.Domain.Entities.Research;

namespace SA.Application.Abstractions;

/// <summary>
/// 主营构成源（东财 F10 <c>BusinessAnalysis</c>）。
/// </summary>
/// <remarks>
/// 一次请求给齐业务范围、主营构成与经营评述三块（实测东芯股份 46 KB）。
/// 三块各有必须处理的层级/口径问题，详见 <see cref="BusinessComposition"/> 的说明。
/// </remarks>
public interface IBusinessCompositionSource : IProbeable
{
    /// <summary>取某标的的主营构成（含业务范围与经营评述原文）。</summary>
    /// <param name="code">证券代码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<BusinessCompositionResult> GetAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>
/// 主营构成的一次拉取结果。
/// </summary>
/// <param name="Items">构成项（已标记 <c>其中:</c> 子项，未过滤——由展示层决定）。</param>
/// <param name="BusinessScope">业务范围原文。</param>
/// <param name="BusinessReview">管理层经营评述原文；<b>只做原文展示，不做任何解析</b>。</param>
public readonly record struct BusinessCompositionResult(
    IReadOnlyList<BusinessComposition> Items,
    string? BusinessScope,
    string? BusinessReview);

/// <summary>
/// 股本结构源（东财 F10 <c>CapitalStockStructure</c>）。
/// </summary>
public interface ICapitalStructureSource : IProbeable
{
    /// <summary>取某标的的股本变动历史与限售解禁。</summary>
    /// <param name="code">证券代码。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<CapitalStructureResult> GetAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>
/// 股本结构的一次拉取结果。
/// </summary>
/// <param name="ShareChanges">股本变动历史（含变动原因），按变动日期降序。</param>
/// <param name="UpcomingUnlocks">
/// 待解禁项。<b>空列表表示「暂无待解禁」而不是「暂无数据」</b>——
/// 实测东芯股份即为此例，其总股本与流通股本相等，确已全流通。
/// </param>
public readonly record struct CapitalStructureResult(
    IReadOnlyList<ShareChange> ShareChanges,
    IReadOnlyList<UpcomingUnlock> UpcomingUnlocks);

/// <summary>
/// 公告源（东财 <c>np-anotice-stock</c>）。
/// </summary>
/// <remarks>
/// 只取列表（标题 / 日期 / 类型 / 原文链接），<b>不做正文解析</b>（实施计划 §1.3）。
/// </remarks>
public interface IAnnouncementSource : IProbeable
{
    /// <summary>取某标的的公告列表。</summary>
    /// <param name="code">证券代码。</param>
    /// <param name="limit">最多取多少条（上游支持分页，这里一次取够展示所需的量）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<Announcement>> GetAsync(
        string code,
        int limit = 60,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 研报源（东财 <c>reportapi</c>）。
/// </summary>
/// <remarks>
/// <b>不提供目标价</b>：实测 <c>indvAimPriceT</c> / <c>indvAimPriceL</c> 全为空字符串。
/// </remarks>
public interface IResearchReportSource : IProbeable
{
    /// <summary>取某标的的研报列表。</summary>
    /// <param name="code">证券代码。</param>
    /// <param name="limit">最多取多少篇。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<ResearchReport>> GetAsync(
        string code,
        int limit = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 个股研究数据存储（主营构成 / 股本结构 / 限售解禁 / 公告 / 研报）。
/// </summary>
/// <remarks>
/// 合并成一个存储接口而不是五个：它们都是「按代码存、按代码读」的从属数据，
/// 且都由同一个采集任务写入；拆开只会让注册与注入变啰嗦。
/// </remarks>
public interface IResearchStore
{
    /// <summary>写入或覆盖主营概况（业务范围 + 经营评述原文）。</summary>
    Task<int> UpsertProfileAsync(BusinessProfile profile, CancellationToken cancellationToken = default);

    /// <summary>取某标的的主营概况；未采集返回 null。</summary>
    Task<BusinessProfile?> GetProfileAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>写入或覆盖主营构成。</summary>
    Task<int> UpsertCompositionsAsync(
        IReadOnlyList<BusinessComposition> items,
        CancellationToken cancellationToken = default);

    /// <summary>取某标的的全部主营构成（按报告期降序、口径、排名）。</summary>
    Task<IReadOnlyList<BusinessComposition>> GetCompositionsAsync(
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>写入或覆盖股本变动历史。</summary>
    Task<int> UpsertShareChangesAsync(
        IReadOnlyList<ShareChange> rows,
        CancellationToken cancellationToken = default);

    /// <summary>取某标的的股本变动历史（按变动日期降序）。</summary>
    Task<IReadOnlyList<ShareChange>> GetShareChangesAsync(
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 用最新一次拉取整体替换某标的的限售解禁。
    /// </summary>
    /// <remarks>
    /// 与其它表不同，这里用「整体替换」：上游 <c>xsjj</c> 给的是<b>当前剩余</b>的待解禁清单，
    /// 已过期的解禁会被上游移除。若按主键 upsert，历史解禁会一直留在库里，
    /// 界面就会把「早已解禁完的」当成「未来待解禁」。
    /// </remarks>
    Task<int> ReplaceUpcomingUnlocksAsync(
        string code,
        IReadOnlyList<UpcomingUnlock> rows,
        CancellationToken cancellationToken = default);

    /// <summary>取某标的的待解禁清单（按解禁日升序）。</summary>
    Task<IReadOnlyList<UpcomingUnlock>> GetUpcomingUnlocksAsync(
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>写入或覆盖公告。</summary>
    Task<int> UpsertAnnouncementsAsync(
        IReadOnlyList<Announcement> rows,
        CancellationToken cancellationToken = default);

    /// <summary>取某标的的公告（按公告日期降序）；<paramref name="columnName"/> 非空时按类型过滤。</summary>
    Task<IReadOnlyList<Announcement>> GetAnnouncementsAsync(
        string code,
        string? columnName = null,
        int limit = 200,
        CancellationToken cancellationToken = default);

    /// <summary>取某标的的公告类型统计（类型 → 条数），用于分组展示。</summary>
    Task<IReadOnlyDictionary<string, int>> GetAnnouncementTypesAsync(
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>写入或覆盖研报。</summary>
    Task<int> UpsertReportsAsync(
        IReadOnlyList<ResearchReport> rows,
        CancellationToken cancellationToken = default);

    /// <summary>取某标的的研报（按发布日期降序）。</summary>
    Task<IReadOnlyList<ResearchReport>> GetReportsAsync(
        string code,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>该标的最近一次写入时间；从未采集返回 null。</summary>
    Task<DateTimeOffset?> GetLastUpdatedAtAsync(string code, CancellationToken cancellationToken = default);
}
