using DuckDB.NET.Data;

namespace SA.Collector.Probe;

/// <summary>
/// DuckDB 最小读写样例：验证 net11.0 下 DuckDB.NET 可用（实施计划 §3.5 第 8 项）。
/// 若此处不通过，按 §13 触发回退到 net10.0 的判定条件。
/// </summary>
internal static class DuckDbSmoke
{
    /// <summary>
    /// 运行最小读写与 Parquet 往返测试。
    /// </summary>
    /// <param name="dataRoot">数据根目录（data/）。</param>
    public static async Task<int> RunAsync(string dataRoot)
    {
        var smokeDir = Path.Combine(dataRoot, "smoke");
        Directory.CreateDirectory(smokeDir);
        var databaseFile = Path.Combine(smokeDir, "duckdb-smoke.duckdb");
        var parquetFile = Path.Combine(smokeDir, "duckdb-smoke.parquet");
        var dbPath = databaseFile.Replace('\\', '/');
        var pqPath = parquetFile.Replace('\\', '/');

        if (File.Exists(databaseFile))
        {
            File.Delete(databaseFile);
        }

        if (File.Exists(parquetFile))
        {
            File.Delete(parquetFile);
        }

        try
        {
            await using var connection = new DuckDBConnection($"Data Source={dbPath}");
            await connection.OpenAsync().ConfigureAwait(false);

            await using (var create = connection.CreateCommand())
            {
                create.CommandText =
                    "CREATE TABLE daily (code VARCHAR, trade_date DATE, close DECIMAL(18,4), volume BIGINT)";
                await create.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            // 参数化插入：验证参数绑定（后续各数据集写入都依赖它）
            for (var i = 0; i < 5; i++)
            {
                await using var insert = connection.CreateCommand();
                insert.CommandText =
                    "INSERT INTO daily VALUES ($code, $date, $close, $volume)";
                insert.Parameters.Add(new DuckDBParameter { ParameterName = "code", Value = "300750" });
                insert.Parameters.Add(new DuckDBParameter
                {
                    ParameterName = "date",
                    Value = new DateOnly(2026, 9, 14 + i)
                });
                insert.Parameters.Add(new DuckDBParameter
                {
                    ParameterName = "close",
                    Value = 300m + i
                });
                insert.Parameters.Add(new DuckDBParameter { ParameterName = "volume", Value = 100000L + i });
                await insert.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await using (var select = connection.CreateCommand())
            {
                select.CommandText = "SELECT count(*), sum(volume), max(close) FROM daily";
                await using var reader = await select.ExecuteReaderAsync().ConfigureAwait(false);
                if (await reader.ReadAsync().ConfigureAwait(false))
                {
                    Console.WriteLine(
                        $"  DuckDB 读写：行数={reader.GetInt64(0)} 总量={reader.GetInt64(1)} 最高收盘={reader.GetDecimal(2)}");
                }
            }

            // Parquet 往返：时序历史的核心形态
            await using (var copy = connection.CreateCommand())
            {
                copy.CommandText = $"COPY (SELECT * FROM daily ORDER BY trade_date) TO '{pqPath}' (FORMAT PARQUET)";
                await copy.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await using (var readBack = connection.CreateCommand())
            {
                readBack.CommandText = $"SELECT count(*) FROM read_parquet('{pqPath}')";
                var count = Convert.ToInt64(await readBack.ExecuteScalarAsync().ConfigureAwait(false));
                Console.WriteLine($"  Parquet 往返：写入 {new FileInfo(parquetFile).Length} 字节，读回 {count} 行");
            }

            // UPSERT 语义（各采集任务幂等的前提，实施计划 §9）。
            // DuckDB 的 ON CONFLICT 要求目标表声明 UNIQUE/PRIMARY KEY，因此时序数据集一律按
            // (code, trade_date) 建主键——这一点在后续各数据集建表时必须遵守。
            await using (var upsert = connection.CreateCommand())
            {
                upsert.CommandText =
                    "CREATE TABLE upsert_probe (code VARCHAR, trade_date DATE, close DECIMAL(18,4), PRIMARY KEY (code, trade_date)); " +
                    "INSERT INTO upsert_probe VALUES ('300750', DATE '2026-09-18', 1); " +
                    "INSERT INTO upsert_probe VALUES ('300750', DATE '2026-09-18', 2) ON CONFLICT DO NOTHING;";
                await upsert.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await using (var verify = connection.CreateCommand())
            {
                verify.CommandText = "SELECT count(*), min(close) FROM upsert_probe";
                await using var reader = await verify.ExecuteReaderAsync().ConfigureAwait(false);
                if (await reader.ReadAsync().ConfigureAwait(false))
                {
                    var rows = reader.GetInt64(0);
                    var close = reader.GetDecimal(1);
                    Console.WriteLine($"  重复写入去重：行数={rows} 收盘={close}（期望 1 / 1）");
                    if (rows != 1 || close != 1m)
                    {
                        Console.Error.WriteLine("  → 幂等写入未按预期去重，请检查主键定义");
                        return 1;
                    }
                }
            }

            Console.WriteLine("  DuckDB 最小样例：通过（含参数绑定、Parquet 往返、主键冲突去重）");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  DuckDB 最小样例：失败 —— {ex.GetType().Name}: {ex.Message}");
            Console.Error.WriteLine("  完整异常链：");
            Console.Error.WriteLine(ex.ToString());
            Console.Error.WriteLine("  → 若为 net11.0 兼容性问题，按实施计划 §13 触发回退 net10.0 的评估。");
            return 1;
        }
    }
}
