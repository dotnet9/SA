using SA.Application.Abstractions;
using SA.Domain.History;

namespace SA.Api.Tests.Fakes;

/// <summary>
/// K 线源的测试替身：按请求区间返回确定性的日线，便于断言指标与相对强弱的数值。
/// </summary>
/// <remarks>
/// 生成规则固定（收盘价 = 100 + 序号），因此 MA/MACD 的期望值可以手算复核，
/// 不依赖任何外部数据。
/// </remarks>
internal sealed class FakeKlineSource : IKlineSource
{
    /// <summary>基准日期：所有序列从这一天开始。</summary>
    public static readonly DateOnly Start = new(2025, 1, 2);

    /// <summary>可为某个代码设定「无数据」，用于验证 1003 → 按需采集 → 就绪的路径。</summary>
    private readonly HashSet<string> _empty;

    /// <summary>按代码覆盖可用交易日数，用于构造「样本不足」场景。</summary>
    private readonly Dictionary<string, int> _tradingDaysByCode = new(StringComparer.Ordinal);

    public FakeKlineSource(IEnumerable<string>? emptyFor = null, int tradingDays = 400)
    {
        _empty = new HashSet<string>(emptyFor ?? [], StringComparer.Ordinal);
        TradingDays = tradingDays;
    }

    /// <summary>可用的交易日总数（≈ 两年）。</summary>
    public int TradingDays { get; }

    /// <summary>覆盖某只股票的可用交易日数（例如只给 40 天，用于验证样本不足）。</summary>
    public void SetTradingDays(string code, int days) => _tradingDaysByCode[code] = days;

    public string Name => "测试源 · K 线";

    public string Domains => "行情,历史";

    /// <summary>记录每个代码被请求过多少次，用于验证按需采集确实被触发。</summary>
    public Dictionary<string, int> RequestCount { get; } = new(StringComparer.Ordinal);

    /// <summary>把某只股票标记为「现在有数据了」，模拟采集完成。</summary>
    public void MakeAvailable(string code) => _empty.Remove(code);

    public Task<IReadOnlyList<DailyBar>> GetDailyAsync(
        string code,
        DateOnly from,
        DateOnly to,
        int adjust,
        int period = 101,
        CancellationToken cancellationToken = default)
    {
        RequestCount[code] = RequestCount.TryGetValue(code, out var count) ? count + 1 : 1;

        if (_empty.Contains(code))
        {
            return Task.FromResult<IReadOnlyList<DailyBar>>([]);
        }

        // 复权口径不同 → 数值不同，但「前复权 / 不复权」的比值恒定：用于验证除权检测不误报
        var scale = adjust == SA.Infrastructure.Collect.Adapters.EastMoneyKlineSource.AdjustFront ? 1m : 1.25m;

        // 覆盖交易日数的代码只提供「最近 N 个交易日」：与真实场景一致（新股的可用历史更短）
        var first = _tradingDaysByCode.TryGetValue(code, out var custom)
            ? Math.Max(0, TradingDays - custom)
            : 0;

        var bars = new List<DailyBar>();
        for (var i = first; i < TradingDays; i++)
        {
            var date = NextTradingDay(Start, i);
            if (date < from || date > to)
            {
                continue;
            }

            var close = (100m + i) * scale;
            bars.Add(new DailyBar(
                Date: date,
                Open: close - 1,
                High: close + 2,
                Low: close - 2,
                Close: close,
                Volume: 1_000_000m + i * 1000m,
                Amount: (1_000_000m + i * 1000m) * close,
                Turnover: 1.2m,
                VolRatio: 1m,
                AdjFactor: 1m));
        }

        return Task.FromResult<IReadOnlyList<DailyBar>>(bars);
    }

    public Task<SourceProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SourceProbeResult.Success(1, 200, TradingDays));

    /// <summary>
    /// 板块指数日线替身：生成一条与个股同结构、但趋势略弱的序列，
    /// 用于验证景气度与传导带宽在「行业指数可用」时的计算路径。
    /// </summary>
    public Task<IReadOnlyList<DailyBar>> GetSectorDailyAsync(
        string sectorCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        RequestCount[sectorCode] = RequestCount.TryGetValue(sectorCode, out var count) ? count + 1 : 1;

        var bars = new List<DailyBar>();
        for (var i = 0; i < TradingDays; i++)
        {
            var date = NextTradingDay(Start, i);
            if (date < from || date > to)
            {
                continue;
            }

            // 与个股相近但不同的斜率：相关性应当较高但不等于 1
            var close = 1000m + i * 1.05m;
            bars.Add(new DailyBar(
                Date: date,
                Open: close - 2,
                High: close + 4,
                Low: close - 4,
                Close: close,
                Volume: 5_000_000m,
                Amount: 5_000_000m * close,
                Turnover: 1m,
                VolRatio: 1m,
                AdjFactor: 1m));
        }

        return Task.FromResult<IReadOnlyList<DailyBar>>(bars);
    }

    /// <summary>第 i 个交易日（跳过周末；不处理节假日，测试数据不需要）。</summary>
    public static DateOnly NextTradingDay(DateOnly start, int index)
    {
        var date = start;
        var remaining = index;
        while (remaining > 0)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                remaining--;
            }
        }

        return date;
    }
}
