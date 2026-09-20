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
    /// 限速：每秒允许的请求数。
    /// </summary>
    /// <remarks>
    /// 默认 4 而非更高：实测东财 <c>push2</c> 主机在被高频连续请求后开始返回截断响应，
    /// 一轮全市场扫描（60 次请求）按 4 req/s 约 15 秒完成，对 60 秒的扫描周期足够，
    /// 且明显降低了被限流的概率。
    /// </remarks>
    public double RequestsPerSecond { get; set; } = 4;

    /// <summary>单请求超时（秒）。</summary>
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>连续失败达到该次数即标记数据源降级并切换备用源（详细设计 §6.2）。</summary>
    public int DegradeAfterFailures { get; set; } = 3;

    /// <summary>
    /// 数据源被判为不可用后的冷却时长（分钟）：冷却期内不再向该源发请求。
    /// </summary>
    public int CooldownMinutes { get; set; } = 5;

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
