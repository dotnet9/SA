using SA.Domain.Entities.Capital;

namespace SA.Application.Abstractions;

/// <summary>
/// 个股资金流源（行情侧主机，逐日序列）。
/// </summary>
/// <remarks>
/// <b>与 <see cref="ICapitalSource"/> 分开的原因</b>：两者走的是完全不同的上游主机
/// （资金流在 <c>push2his</c>，报表在 <c>datacenter-web</c>）。
/// 数据源状态与冷却窗口是按「数据源」生效的，若把两者合成一个源，
/// 一旦行情侧主机不可用，报表侧的请求也会被一起冷却掉——表现为「资金面页只显示一半数据」。
/// 拆开之后，一条链路出问题只影响它自己。
/// </remarks>
public interface IFundFlowSource : IProbeable
{
    /// <summary>取个股逐日资金流（按日期升序）。</summary>
    /// <param name="code">证券代码。</param>
    /// <param name="days">取最近多少个交易日。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<FundFlowDaily>> GetFundFlowAsync(
        string code,
        int days = 60,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 资金面与筹码报表源（数据中心主机：龙虎榜、大宗交易、两融明细、陆股通持股）。
/// </summary>
/// <remarks>
/// 四个数据集来自同一主机的四张报表，披露频率从逐日到季度不等，
/// 因此各自独立取数、独立失败：某一路不可用时页面按区块降级，而不是整页没有数据。
/// </remarks>
public interface ICapitalSource : IProbeable
{
    /// <summary>取龙虎榜上榜记录（按日期倒序）。</summary>
    Task<IReadOnlyList<BillboardRecord>> GetBillboardsAsync(
        string code,
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>取大宗交易记录（按日期倒序）。</summary>
    Task<IReadOnlyList<BlockTrade>> GetBlockTradesAsync(
        string code,
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>取两融明细（按日期倒序）。</summary>
    Task<IReadOnlyList<MarginDetail>> GetMarginDetailsAsync(
        string code,
        int limit = 30,
        CancellationToken cancellationToken = default);

    /// <summary>取陆股通持股（按报告期倒序，季频）。</summary>
    Task<IReadOnlyList<NorthboundHolding>> GetNorthboundAsync(
        string code,
        int limit = 8,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 资金面数据存储。
/// </summary>
public interface ICapitalStore
{
    /// <summary>取个股资金流（按日期升序）。</summary>
    Task<IReadOnlyList<FundFlowDaily>> GetFundFlowAsync(string code, int days, CancellationToken cancellationToken = default);

    /// <summary>取龙虎榜记录（按日期倒序）。</summary>
    Task<IReadOnlyList<BillboardRecord>> GetBillboardsAsync(string code, int limit, CancellationToken cancellationToken = default);

    /// <summary>取大宗交易（按日期倒序）。</summary>
    Task<IReadOnlyList<BlockTrade>> GetBlockTradesAsync(string code, int limit, CancellationToken cancellationToken = default);

    /// <summary>取两融明细（按日期升序，便于画趋势）。</summary>
    Task<IReadOnlyList<MarginDetail>> GetMarginDetailsAsync(string code, int limit, CancellationToken cancellationToken = default);

    /// <summary>取陆股通持股（按报告期倒序）。</summary>
    Task<IReadOnlyList<NorthboundHolding>> GetNorthboundAsync(string code, int limit, CancellationToken cancellationToken = default);

    /// <summary>写入资金流（按 代码+日期 upsert）。</summary>
    Task<int> UpsertFundFlowAsync(IReadOnlyList<FundFlowDaily> rows, CancellationToken cancellationToken = default);

    /// <summary>写入龙虎榜（按 代码+日期+原因 upsert，并清理超出窗口的旧记录）。</summary>
    Task<int> UpsertBillboardsAsync(string code, IReadOnlyList<BillboardRecord> rows, CancellationToken cancellationToken = default);

    /// <summary>替换大宗交易（同一天多笔，整体替换最简单也最不容易出错）。</summary>
    Task<int> ReplaceBlockTradesAsync(string code, IReadOnlyList<BlockTrade> rows, CancellationToken cancellationToken = default);

    /// <summary>写入两融明细（按 代码+日期 upsert）。</summary>
    Task<int> UpsertMarginDetailsAsync(IReadOnlyList<MarginDetail> rows, CancellationToken cancellationToken = default);

    /// <summary>写入陆股通持股（按 代码+报告期 upsert）。</summary>
    Task<int> UpsertNorthboundAsync(IReadOnlyList<NorthboundHolding> rows, CancellationToken cancellationToken = default);

    /// <summary>最近写入时间。</summary>
    Task<DateTimeOffset?> GetLastUpdatedAtAsync(string code, CancellationToken cancellationToken = default);
}
