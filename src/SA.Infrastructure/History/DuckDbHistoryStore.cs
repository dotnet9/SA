using System.Text;
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;
using SA.Application.Abstractions;
using SA.Domain.History;
using SA.Infrastructure.Storage;

namespace SA.Infrastructure.History;

/// <summary>
/// 时序历史的分片路径。每个标的一份 Parquet 文件，数据集之间用目录区分
/// （详细设计 §4：<c>daily</c> 按 <c>market/code</c> 分区、其余按 <c>year/code</c>）。
/// </summary>
/// <remarks>
/// 为什么用「一标的一份文件」而不是「一天一份」：一天一份会让全市场产生 5,900 × 244 个碎文件，
/// 在 Windows 上打开成本极高（实施计划 §12 已列为风险）。一标一份既天然支持「重算 + 覆盖该标的」，
/// 又让读取一次只碰一个文件。
/// </remarks>
public sealed class ParquetPaths(DataPaths paths)
{
    /// <summary>日线数据集目录。</summary>
    public string DailyRoot => Path.Combine(paths.ParquetRoot, "daily");

    /// <summary>指标数据集目录。</summary>
    public string IndicatorRoot => Path.Combine(paths.ParquetRoot, "indicator");

    /// <summary>
    /// 取某标的的日线文件路径，形如 <c>parquet/daily/0/300750.parquet</c>。
    /// </summary>
    /// <param name="code">证券代码。</param>
    public string DailyFile(string code)
    {
        var market = Domain.Common.MarketCodes.MarketOf(code);
        return Path.Combine(DailyRoot, market.ToString(), $"{code}.parquet");
    }

    /// <summary>
    /// 取某标的的指标文件路径，按年分区 <c>parquet/indicator/{year}/{code}.parquet</c>。
    /// </summary>
    /// <remarks>
    /// 指标随日线同步增长，按年分区让单个文件保持在一年 240 行的量级；
    /// 跨年时新文件自动产生，读取时按年拼接。
    /// </remarks>
    public string IndicatorFile(string code, int year) =>
        Path.Combine(IndicatorRoot, year.ToString(), $"{code}.parquet");

    /// <summary>指标目录下某标的的全部文件（跨年），按年份升序。</summary>
    public IReadOnlyList<string> IndicatorFiles(string code)
    {
        if (!Directory.Exists(IndicatorRoot))
        {
            return [];
        }

        return Directory.EnumerateDirectories(IndicatorRoot)
            .Select(dir => Path.Combine(dir, $"{code}.parquet"))
            .Where(File.Exists)
            .OrderBy(path => Path.GetFileName(Path.GetDirectoryName(path)), StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>确保某文件所在目录存在。</summary>
    public static void EnsureDirectory(string file)
    {
        var directory = Path.GetDirectoryName(file);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}

/// <summary>
/// 日线历史的 Parquet 存储（DuckDB 查询）。
/// </summary>
/// <remarks>
/// <para>
/// DuckDB 在这里是<b>无状态查询引擎</b>：所有数据都在 Parquet 文件里，每次操作开一个内存连接
/// （<c>Data Source=:memory:</c>），用完即弃。这样做的好处是没有数据库文件锁、没有并发写入的
/// 相互阻塞，也不需要为「数据库与 Parquet 谁是真相」做额外约定——Parquet 就是唯一真相。
/// </para>
/// <para>
/// 合并写入采用「读旧 + 去重 + 写临时文件 + 原子替换」：同一交易日的重复入库以新数据为准，
/// 因此任务重跑不产生重复行（实施计划 §9.2）。
/// </para>
/// </remarks>
public sealed class DuckDbHistoryStore(ParquetPaths paths, ILogger<DuckDbHistoryStore> logger) : IDailyHistoryStore
{
    /// <summary>写入串行化：DuckDB 每次操作都要初始化原生库，并发开多个连接收益为负。</summary>
    private static readonly SemaphoreSlim WriteGate = new(1, 1);

    private const string Columns =
        "trade_date DATE, open DECIMAL(18,4), high DECIMAL(18,4), low DECIMAL(18,4), close DECIMAL(18,4), " +
        "volume DECIMAL(20,2), amount DECIMAL(20,2), turnover DECIMAL(10,4), vol_ratio DECIMAL(10,4), adj_factor DECIMAL(10,6)";

    /// <inheritdoc />
    public async Task<DateOnly?> GetLastDateAsync(string code, CancellationToken cancellationToken = default)
    {
        var file = paths.DailyFile(code);
        if (!File.Exists(file))
        {
            return null;
        }

        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT max(trade_date) FROM read_parquet({Literal(file)})";
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : ToDateOnly(value);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DailyBar>> GetLatestAsync(
        string code,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var file = paths.DailyFile(code);
        if (!File.Exists(file))
        {
            return [];
        }

        // 先按日期倒序取最近 N 根，再在内存里反转为升序（与图形/指标的读取顺序一致）
        var bars = await QueryAsync(
            $"""
             SELECT trade_date, open, high, low, close, volume, amount, turnover, vol_ratio, adj_factor
             FROM read_parquet({Literal(file)})
             ORDER BY trade_date DESC
             LIMIT {Math.Max(1, limit)}
             """,
            ReadDailyBar,
            cancellationToken).ConfigureAwait(false);

        bars.Reverse();
        return bars;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DailyBar>> GetRangeAsync(
        string code,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var file = paths.DailyFile(code);
        if (!File.Exists(file))
        {
            return [];
        }

        return await QueryAsync(
            $"""
             SELECT trade_date, open, high, low, close, volume, amount, turnover, vol_ratio, adj_factor
             FROM read_parquet({Literal(file)})
             WHERE trade_date >= DATE '{from:yyyy-MM-dd}' AND trade_date <= DATE '{to:yyyy-MM-dd}'
             ORDER BY trade_date
             """,
            ReadDailyBar,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> MergeAsync(
        string code,
        IReadOnlyList<DailyBar> bars,
        CancellationToken cancellationToken = default)
    {
        if (bars.Count == 0)
        {
            return 0;
        }

        var file = paths.DailyFile(code);
        ParquetPaths.EnsureDirectory(file);

        await WriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = OpenConnection();

            await using (var create = connection.CreateCommand())
            {
                create.CommandText = $"CREATE TEMP TABLE incoming ({Columns})";
                await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await InsertDailyAsync(connection, bars, cancellationToken).ConfigureAwait(false);

            // 旧数据里去掉与本次写入同日的行，再并上本次数据（新数据为准）
            var union = File.Exists(file)
                ? $"""
                   SELECT * FROM read_parquet({Literal(file)})
                   WHERE trade_date NOT IN (SELECT trade_date FROM incoming)
                   UNION ALL SELECT * FROM incoming
                   """
                : "SELECT * FROM incoming";

            var temp = file + ".tmp";
            await using (var copy = connection.CreateCommand())
            {
                copy.CommandText =
                    $"COPY (SELECT * FROM ({union}) ORDER BY trade_date) TO {Literal(temp)} (FORMAT PARQUET)";
                await copy.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var count = connection.CreateCommand())
            {
                count.CommandText = $"SELECT count(*) FROM read_parquet({Literal(temp)})";
                var total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));

                // 原子替换：先写 .tmp 再覆盖，避免中途失败留下半截文件
                File.Move(temp, file, overwrite: true);
                return total;
            }
        }
        finally
        {
            WriteGate.Release();
        }
    }

    /// <inheritdoc />
    public Task<int> CountCodesAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(paths.DailyRoot))
        {
            return Task.FromResult(0);
        }

        var count = Directory.EnumerateFiles(paths.DailyRoot, "*.parquet", SearchOption.AllDirectories).Count();
        return Task.FromResult(count);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string code, CancellationToken cancellationToken = default)
    {
        var file = paths.DailyFile(code);
        if (File.Exists(file))
        {
            File.Delete(file);
            logger.LogInformation("已删除日线文件：{File}", file);
        }

        return Task.CompletedTask;
    }

    /// <summary>批量插入临时表；优先用 Appender（快），不可用时退回参数化 INSERT。</summary>
    private static async Task InsertDailyAsync(
        DuckDBConnection connection,
        IReadOnlyList<DailyBar> bars,
        CancellationToken cancellationToken)
    {
        using var appender = connection.CreateAppender("incoming");
        foreach (var bar in bars)
        {
            var row = appender.CreateRow();
            row.AppendValue(bar.Date);
            row.AppendValue(bar.Open);
            row.AppendValue(bar.High);
            row.AppendValue(bar.Low);
            row.AppendValue(bar.Close);
            row.AppendValue(bar.Volume);
            row.AppendValue(bar.Amount);
            row.AppendValue(bar.Turnover);
            row.AppendValue(bar.VolRatio);
            row.AppendValue(bar.AdjFactor);
            row.EndRow();
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static DailyBar ReadDailyBar(DuckDBDataReader reader) =>
        new(
            reader.GetFieldValue<DateOnly>(0),
            reader.GetDecimal(1),
            reader.GetDecimal(2),
            reader.GetDecimal(3),
            reader.GetDecimal(4),
            reader.GetDecimal(5),
            reader.GetDecimal(6),
            reader.GetDecimal(7),
            reader.GetDecimal(8),
            reader.GetDecimal(9));

    /// <summary>打开一个内存连接（Parquet 为唯一存储，连接无需持久化）。</summary>
    internal static DuckDBConnection OpenConnection()
    {
        var connection = new DuckDBConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    /// <summary>DuckDB 的字符串字面量：路径里的反斜杠需要转成正斜杠并转义单引号。</summary>
    internal static string Literal(string value) => $"'{value.Replace('\\', '/').Replace("'", "''")}'";

    /// <summary>把 DuckDB 返回的日期标量归一成 <see cref="DateOnly"/>。</summary>
    internal static DateOnly ToDateOnly(object value) => value switch
    {
        DateOnly date => date,
        DateTime dateTime => DateOnly.FromDateTime(dateTime),
        string text => DateOnly.Parse(text),
        _ => DateOnly.FromDateTime(Convert.ToDateTime(value))
    };

    /// <summary>执行查询并把每行映射成结果对象。</summary>
    private static async Task<List<T>> QueryAsync<T>(
        string sql,
        Func<DuckDBDataReader, T> map,
        CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var result = new List<T>();
        using var reader = (DuckDBDataReader)await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(map(reader));
        }

        return result;
    }
}

/// <summary>
/// 指标历史的 Parquet 存储。与日线同构，只是列不同、按年分区（跨年时读取多个文件）。
/// </summary>
public sealed class DuckDbIndicatorStore(ParquetPaths paths, ILogger<DuckDbIndicatorStore> logger) : IIndicatorStore
{
    private static readonly SemaphoreSlim WriteGate = new(1, 1);

    private const string Columns =
        "trade_date DATE, ma5 DECIMAL(18,4), ma10 DECIMAL(18,4), ma20 DECIMAL(18,4), ma60 DECIMAL(18,4), " +
        "dif DECIMAL(18,6), dea DECIMAL(18,6), macd DECIMAL(18,6), k DECIMAL(12,4), d DECIMAL(12,4), j DECIMAL(12,4), " +
        "rsi6 DECIMAL(12,4), rsi12 DECIMAL(12,4), rsi24 DECIMAL(12,4), " +
        "boll_up DECIMAL(18,4), boll_mid DECIMAL(18,4), boll_low DECIMAL(18,4)";

    private const string Select =
        "SELECT trade_date, ma5, ma10, ma20, ma60, dif, dea, macd, k, d, j, rsi6, rsi12, rsi24, boll_up, boll_mid, boll_low";

    /// <inheritdoc />
    public async Task<DateOnly?> GetLastDateAsync(string code, CancellationToken cancellationToken = default)
    {
        var files = paths.IndicatorFiles(code);
        if (files.Count == 0)
        {
            return null;
        }

        await using var connection = DuckDbHistoryStore.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT max(trade_date) FROM read_parquet([{string.Join(',', files.Select(DuckDbHistoryStore.Literal))}])";

        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : DuckDbHistoryStore.ToDateOnly(value);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IndicatorRow>> GetLatestAsync(
        string code,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var files = paths.IndicatorFiles(code);
        if (files.Count == 0)
        {
            return [];
        }

        await using var connection = DuckDbHistoryStore.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"{Select} FROM read_parquet([{string.Join(',', files.Select(DuckDbHistoryStore.Literal))}]) " +
            $"ORDER BY trade_date DESC LIMIT {Math.Max(1, limit)}";

        var rows = new List<IndicatorRow>();
        await using (var reader = (DuckDBDataReader)await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(ReadIndicatorRow(reader));
            }
        }

        rows.Reverse();
        return rows;
    }

    /// <inheritdoc />
    public async Task<int> MergeAsync(
        string code,
        IReadOnlyList<IndicatorRow> rows,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
        {
            return 0;
        }

        // 按「本次数据的年份」分组写入：跨年批次会分别落到两个文件
        var byYear = rows.GroupBy(row => row.Date.Year).OrderBy(group => group.Key).ToList();
        var total = 0;

        foreach (var group in byYear)
        {
            total += await MergeYearAsync(code, group.Key, group.ToList(), cancellationToken).ConfigureAwait(false);
        }

        return total;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string code, CancellationToken cancellationToken = default)
    {
        foreach (var file in paths.IndicatorFiles(code))
        {
            File.Delete(file);
            logger.LogInformation("已删除指标文件：{File}", file);
        }

        return Task.CompletedTask;
    }

    private async Task<int> MergeYearAsync(
        string code,
        int year,
        IReadOnlyList<IndicatorRow> rows,
        CancellationToken cancellationToken)
    {
        var file = paths.IndicatorFile(code, year);
        ParquetPaths.EnsureDirectory(file);

        await WriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = DuckDbHistoryStore.OpenConnection();

            await using (var create = connection.CreateCommand())
            {
                create.CommandText = $"CREATE TEMP TABLE incoming ({Columns})";
                await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            using (var appender = connection.CreateAppender("incoming"))
            {
                foreach (var row in rows)
                {
                    var appenderRow = appender.CreateRow();
                    appenderRow.AppendValue(row.Date);
                    AppendNullable(appenderRow, row.Ma5);
                    AppendNullable(appenderRow, row.Ma10);
                    AppendNullable(appenderRow, row.Ma20);
                    AppendNullable(appenderRow, row.Ma60);
                    AppendNullable(appenderRow, row.Dif);
                    AppendNullable(appenderRow, row.Dea);
                    AppendNullable(appenderRow, row.Macd);
                    AppendNullable(appenderRow, row.K);
                    AppendNullable(appenderRow, row.D);
                    AppendNullable(appenderRow, row.J);
                    AppendNullable(appenderRow, row.Rsi6);
                    AppendNullable(appenderRow, row.Rsi12);
                    AppendNullable(appenderRow, row.Rsi24);
                    AppendNullable(appenderRow, row.BollUp);
                    AppendNullable(appenderRow, row.BollMid);
                    AppendNullable(appenderRow, row.BollLow);
                    appenderRow.EndRow();
                }
            }

            var union = File.Exists(file)
                ? $"SELECT * FROM read_parquet({DuckDbHistoryStore.Literal(file)}) " +
                  "WHERE trade_date NOT IN (SELECT trade_date FROM incoming) UNION ALL SELECT * FROM incoming"
                : "SELECT * FROM incoming";

            var temp = file + ".tmp";
            await using (var copy = connection.CreateCommand())
            {
                copy.CommandText =
                    $"COPY (SELECT * FROM ({union}) ORDER BY trade_date) TO {DuckDbHistoryStore.Literal(temp)} (FORMAT PARQUET)";
                await copy.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var count = connection.CreateCommand();
            count.CommandText = $"SELECT count(*) FROM read_parquet({DuckDbHistoryStore.Literal(temp)})";
            var total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            File.Move(temp, file, overwrite: true);
            return total;
        }
        finally
        {
            WriteGate.Release();
        }
    }

    private static void AppendNullable(IDuckDBAppenderRow row, decimal? value)
    {
        if (value is null)
        {
            row.AppendNullValue();
        }
        else
        {
            row.AppendValue(value.Value);
        }
    }

    private static IndicatorRow ReadIndicatorRow(DuckDBDataReader reader) =>
        new(
            reader.GetFieldValue<DateOnly>(0),
            Nullable(reader, 1),
            Nullable(reader, 2),
            Nullable(reader, 3),
            Nullable(reader, 4),
            Nullable(reader, 5),
            Nullable(reader, 6),
            Nullable(reader, 7),
            Nullable(reader, 8),
            Nullable(reader, 9),
            Nullable(reader, 10),
            Nullable(reader, 11),
            Nullable(reader, 12),
            Nullable(reader, 13),
            Nullable(reader, 14),
            Nullable(reader, 15),
            Nullable(reader, 16));

    private static decimal? Nullable(DuckDBDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
}
