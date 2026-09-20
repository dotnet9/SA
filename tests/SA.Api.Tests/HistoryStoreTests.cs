using Microsoft.Extensions.Logging.Abstractions;
using SA.Domain.History;
using SA.Infrastructure.History;
using SA.Infrastructure.Storage;

namespace SA.Api.Tests;

/// <summary>
/// 时序历史存储（Parquet + DuckDB）。这条链路是趋势模块的地基：
/// 写不进去或读不出来，后面所有指标与图形都是空的，因此用真实文件往返验证。
/// </summary>
public sealed class HistoryStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sa-history", Guid.NewGuid().ToString("N"));
    private readonly ParquetPaths _paths;
    private readonly DuckDbHistoryStore _daily;
    private readonly DuckDbIndicatorStore _indicators;

    public HistoryStoreTests()
    {
        var dataPaths = new DataPaths(_root);
        dataPaths.EnsureCreated();

        _paths = new ParquetPaths(dataPaths);
        _daily = new DuckDbHistoryStore(_paths, NullLogger<DuckDbHistoryStore>.Instance);
        _indicators = new DuckDbIndicatorStore(_paths, NullLogger<DuckDbIndicatorStore>.Instance);
    }

    [Fact]
    public async Task 日线写入后可读回且顺序为升序()
    {
        var bars = BuildBars(new DateOnly(2026, 9, 14), 5);

        var total = await _daily.MergeAsync("300750", bars);

        Assert.Equal(5, total);

        var read = await _daily.GetLatestAsync("300750", 10);
        Assert.Equal(5, read.Count);
        Assert.Equal(new DateOnly(2026, 9, 14), read[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 18), read[^1].Date);

        // 字段不能串位：收盘必须大于最低、小于最高
        var last = read[^1];
        Assert.True(last.Close >= last.Low && last.Close <= last.High);

        // 大额成交额（115 亿）必须原样往返，不能被精度截断
        Assert.Equal(11_537_664_647.91m + 4m, last.Amount);
    }

    [Fact]
    public async Task 同日重复写入以新数据为准且不产生重复行()
    {
        var first = BuildBars(new DateOnly(2026, 9, 14), 5);
        await _daily.MergeAsync("300750", first);

        // 第二次入库覆盖最后一天，并追加一天
        var patched = new List<DailyBar>
        {
            first[^1] with { Close = 999.99m },
            BuildBars(new DateOnly(2026, 9, 21), 1)[0]
        };

        var total = await _daily.MergeAsync("300750", patched);

        Assert.Equal(6, total);

        var read = await _daily.GetLatestAsync("300750", 10);
        Assert.Equal(6, read.Count);

        var overwritten = read.Single(bar => bar.Date == new DateOnly(2026, 9, 18));
        Assert.Equal(999.99m, overwritten.Close);
    }

    [Fact]
    public async Task 未入库的标的返回空而不是异常()
    {
        Assert.Null(await _daily.GetLastDateAsync("000001"));
        Assert.Empty(await _daily.GetLatestAsync("000001", 10));
        Assert.Empty(await _daily.GetRangeAsync("000001", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public async Task 按区间读取只返回区间内的行()
    {
        await _daily.MergeAsync("300750", BuildBars(new DateOnly(2026, 9, 14), 5));

        var range = await _daily.GetRangeAsync("300750", new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 17));

        Assert.Equal(3, range.Count);
        Assert.Equal(new DateOnly(2026, 9, 15), range[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 17), range[^1].Date);
    }

    [Fact]
    public async Task 最后一个交易日可用于增量判断()
    {
        Assert.Null(await _daily.GetLastDateAsync("300750"));

        await _daily.MergeAsync("300750", BuildBars(new DateOnly(2026, 9, 14), 3));

        Assert.Equal(new DateOnly(2026, 9, 16), await _daily.GetLastDateAsync("300750"));
    }

    [Fact]
    public async Task 删除标的会清空其历史()
    {
        await _daily.MergeAsync("300750", BuildBars(new DateOnly(2026, 9, 14), 3));

        await _daily.DeleteAsync("300750");

        Assert.Null(await _daily.GetLastDateAsync("300750"));
        Assert.Equal(0, await _daily.CountCodesAsync());
    }

    [Fact]
    public async Task 指标的可空列能正确往返()
    {
        // 前两行指标不足窗口 → 全 null，第三行开始有值；这是「新股显示 —」的数据基础
        var rows = new List<IndicatorRow>
        {
            Indicator("2026-09-14", ma5: null, rsi6: null),
            Indicator("2026-09-15", ma5: null, rsi6: null),
            Indicator("2026-09-16", ma5: 10.5m, rsi6: 62.3m)
        };

        await _indicators.MergeAsync("300750", rows);

        var read = await _indicators.GetLatestAsync("300750", 10);

        Assert.Equal(3, read.Count);
        Assert.Null(read[0].Ma5);
        Assert.Null(read[0].Rsi6);
        Assert.Equal(10.5m, read[2].Ma5);
        Assert.Equal(62.3m, read[2].Rsi6);
        Assert.Equal(new DateOnly(2026, 9, 16), await _indicators.GetLastDateAsync("300750"));
    }

    [Fact]
    public async Task 指标跨年分区后可一次性读回()
    {
        var rows = new List<IndicatorRow>
        {
            Indicator("2025-12-31", ma5: 1m, rsi6: 50m),
            Indicator("2026-01-02", ma5: 2m, rsi6: 51m),
            Indicator("2026-01-05", ma5: 3m, rsi6: 52m)
        };

        await _indicators.MergeAsync("300750", rows);

        // 两个年份两个文件
        Assert.Equal(2, _paths.IndicatorFiles("300750").Count);

        var read = await _indicators.GetLatestAsync("300750", 10);
        Assert.Equal(3, read.Count);
        Assert.Equal(new DateOnly(2025, 12, 31), read[0].Date);
        Assert.Equal(new DateOnly(2026, 1, 5), read[^1].Date);

        Assert.Equal(new DateOnly(2026, 1, 5), await _indicators.GetLastDateAsync("300750"));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // 原生库释放有延迟，清理失败不影响测试结论
        }
    }

    private static List<DailyBar> BuildBars(DateOnly start, int count)
    {
        var bars = new List<DailyBar>(count);
        for (var i = 0; i < count; i++)
        {
            var close = 300m + i;
            bars.Add(new DailyBar(
                Date: start.AddDays(i),
                Open: close - 1,
                High: close + 5,
                Low: close - 5,
                Close: close,
                Volume: 380_713m + i,
                Amount: 11_537_664_647.91m + i,
                Turnover: 0.89m,
                VolRatio: 0.88m,
                AdjFactor: 1m));
        }

        return bars;
    }

    private static IndicatorRow Indicator(string date, decimal? ma5, decimal? rsi6) =>
        new(
            Date: DateOnly.Parse(date),
            Ma5: ma5,
            Ma10: ma5,
            Ma20: ma5,
            Ma60: null,
            Dif: 0.5m,
            Dea: 0.4m,
            Macd: 0.2m,
            K: 60m,
            D: 55m,
            J: 70m,
            Rsi6: rsi6,
            Rsi12: rsi6,
            Rsi24: null,
            BollUp: 320m,
            BollMid: 310m,
            BollLow: 300m);
}
