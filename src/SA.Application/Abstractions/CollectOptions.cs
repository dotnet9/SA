using SA.Domain.Entities.Collect;

namespace SA.Application.Abstractions;

/// <summary>
/// 采集配置。对应 <c>Sa:Collector</c> 节（详细设计 §10），并补充本批新增的两个键。
/// </summary>
public sealed class CollectOptions
{
    /// <summary>配置节名。</summary>
    public const string SectionName = "Sa:Collector";

    /// <summary>
    /// 自选股推送间隔（秒）。仅自选股走多标的快照端点，可以按 3 秒刷新。
    /// </summary>
    public int QuoteIntervalSeconds { get; set; } = 3;

    /// <summary>
    /// 全市场快照扫描间隔（秒）。
    /// </summary>
    /// <remarks>
    /// <b>本批新增的配置项</b>：全市场列表端点单页上限 100 行（实测），
    /// 扫完约 5,900 只需约 60 次请求，因此不可能用 3 秒的节奏。
    /// 默认 60 秒与限速（8 req/s）配合，既保证排行榜与涨跌家数可用，又不会打到上游限频。
    /// </remarks>
    public int FullScanIntervalSeconds { get; set; } = 60;

    /// <summary>全市场列表与板块列表每日强制重跑的本地时刻（HH:mm），用于元数据自愈。</summary>
    public string DailyRefreshTime { get; set; } = "15:40";

    /// <summary>单源最大重试次数（指数退避）。</summary>
    public int MaxRetry { get; set; } = 3;

    /// <summary>退避基数（毫秒）：1s → 2s → 4s。</summary>
    public int BackoffBaseMs { get; set; } = 1000;

    /// <summary>并发上限。</summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    /// 同一域名两次请求之间的最小间隔（毫秒）。按域名各自计时，互不占用配额。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 取代了原先的全局令牌桶（<c>RequestsPerSecond</c>）。全局桶的问题是
    /// 「查财务」会把「查行情」的配额吃掉：一轮全市场扫描（约 60 次）之后，
    /// 同一秒内的指数刷新只能排队等待。
    /// </para>
    /// <para>
    /// 默认 700ms（约 1.4 req/s/域名），取值参考 <c>edge_riches</c> 的 <c>fetch_data.py</c>。
    /// 全市场扫描因此约 42 秒，仍在 60 秒的扫描周期内；比原来的 4 req/s 保守，
    /// 而实测 <c>push2</c> 主机正是被高频连续请求触发拒服的。
    /// </para>
    /// </remarks>
    public int PerHostMinIntervalMs { get; set; } = 700;

    /// <summary>
    /// 轮换用的 User-Agent 池。
    /// </summary>
    /// <remarks>
    /// 固定单一 UA 是实测被风控的特征之一（实施计划 §4.1）。池内覆盖
    /// Chrome / Firefox / Safari / Edge 与 Windows / macOS / Linux，每次请求随机取一个。
    /// 留空则回退到 <see cref="FallbackUserAgent"/>，保证请求始终带 UA。
    /// </remarks>
    public string[] UserAgents { get; set; } = [];

    /// <summary>UA 池为空时的兜底 UA。</summary>
    public string FallbackUserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0 Safari/537.36";

    /// <summary>同一域名连续失败达到该次数即熔断（进入冷却）。</summary>
    public int BreakerThreshold { get; set; } = 3;

    /// <summary>熔断后的冷却时长（秒）；到期放行一次试探（半开）。</summary>
    public int BreakerCooldownSeconds { get; set; } = 60;

    /// <summary>单请求超时（秒）。</summary>
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>连续失败达到该次数即标记数据源降级并切换备用源（详细设计 §6.2）。</summary>
    public int DegradeAfterFailures { get; set; } = 3;

    /// <summary>
    /// 数据源被判为不可用后的冷却时长（分钟）：冷却期内不再向该源发请求。
    /// </summary>
    public int CooldownMinutes { get; set; } = 5;

    /// <summary>
    /// 历史回补的回看天数（默认 1 年，实施计划 §2 决策 5）。
    /// </summary>
    public int BackfillLookbackDays { get; set; } = 365;

    /// <summary>
    /// 单轮回补的标的上限。
    /// </summary>
    /// <remarks>
    /// 全市场 1 年日线约 5,900 个请求，按 4 req/s 需要约 25 分钟，不可能也不该在
    /// 一次调度周期内跑完。因此每轮只处理固定数量，其余由游标在后续轮次继续，
    /// 节奏由 <see cref="BackfillIntervalMinutes"/> 控制。
    /// </remarks>
    public int BackfillCodesPerRun { get; set; } = 400;

    /// <summary>回补轮的间隔（分钟）。</summary>
    public int BackfillIntervalMinutes { get; set; } = 10;

    /// <summary>回补优先级：成交额前多少名排在全市场之前（指数始终最优先）。</summary>
    public int BackfillPriorityCodes { get; set; } = 300;

    /// <summary>是否启用采集宿主服务。测试与纯排查场景可关闭。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>腾讯备源是否启用（快照类降级）。</summary>
    public bool EnableTencentFallback { get; set; } = true;

    /// <summary>
    /// 东财实时行情主机基地址。
    /// </summary>
    /// <remarks>
    /// 默认 <c>https://push2.eastmoney.com</c>。保留为配置项是为了应对实测到的一种情况：
    /// 该主机在被连续请求（一轮全市场扫描约 60 次）后会临时拒服（返回截断响应），
    /// 而 <c>https://push2delay.eastmoney.com</c> 仍然可用。
    /// <b>换成 delay 主机意味着行情有延迟</b>，因此不做自动 failover——由运维显式配置，
    /// 并且界面上的数据时间仍以指数快照的时间戳为准，不会被无声地当成实时数据。
    /// </remarks>
    public string Push2BaseUrl { get; set; } = "https://push2.eastmoney.com";

    /// <summary>非交易日是否仍跑一次收盘快照（用于补齐节假日后的首个交易日）。</summary>
    public bool CollectOnNonTradingDay { get; set; } = true;

    /// <summary>市场概览端点的缓存时长（秒）；0 表示不缓存（详细设计 §12 路由级缓存）。</summary>
    public int OverviewCacheSeconds { get; set; } = 60;

    /// <summary>数据源降级阈值对应的状态。</summary>
    public string DegradedState(int consecutiveFailures) =>
        consecutiveFailures >= DegradeAfterFailures ? DataSourceStates.Err
        : consecutiveFailures > 0 ? DataSourceStates.Warn
        : DataSourceStates.Ok;
}
