using System.Text;
using SA.Collector.Probe;
using SA.Infrastructure.Storage;

// 采集器当前提供两种运行形态：
//   1) 诊断模式：--probe / --duckdb-smoke，用于实施首日实测与后续上游漂移排障；
//   2) 调度模式：默认形态，第 2 批接入 SchedulerHostedService 后实现。

Console.OutputEncoding = Encoding.UTF8;

var options = CommandLineOptions.Parse(args);

if (options.ShowHelp)
{
    Console.WriteLine(CommandLineOptions.HelpText);
    return 0;
}

var dataRoot = DataPaths.ResolveRoot(configured: null);

if (options.DuckDbSmoke)
{
    Console.WriteLine($"== DuckDB 最小读写样例（{dataRoot}）==");
    return await DuckDbSmoke.RunAsync(dataRoot).ConfigureAwait(false);
}

if (options.Probe is not null)
{
    var probes = ProbeCatalog.Select(options.Probe);
    if (probes.Count == 0)
    {
        Console.Error.WriteLine($"未找到匹配的探针：{options.Probe}");
        Console.Error.WriteLine(CommandLineOptions.HelpText);
        return 2;
    }

    Console.WriteLine($"== 数据源探针（{probes.Count} 项）==");

    var runner = new ProbeRunner();
    var results = await runner.RunAsync(probes, ProbeReport.PrintProgress).ConfigureAwait(false);

    var ok = results.Count(r => r.Ok);
    var report = ProbeReport.Build(results, options.ReportTitle ?? "SA 数据源首日实测报告");
    var outPath = options.Out ?? Path.Combine(dataRoot, "logs", $"probe-{DateTime.Now:yyyyMMdd-HHmmss}.md");
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    await File.WriteAllTextAsync(outPath, report, new UTF8Encoding(false)).ConfigureAwait(false);

    Console.WriteLine($"== 结果：{ok}/{results.Count} 可达 ==");
    Console.WriteLine($"报告已写入：{outPath}");

    var failed = results.Where(r => !r.Ok).ToList();
    if (failed.Count > 0)
    {
        Console.WriteLine("失败项：");
        foreach (var f in failed)
        {
            Console.WriteLine($"  - {f.Definition.Name}: {f.Error}");
        }
    }

    return failed.Count == 0 ? 0 : 1;
}

Console.WriteLine(CommandLineOptions.HelpText);
return 0;

/// <summary>
/// 命令行参数。刻意保持极简，避免在诊断路径上引入配置框架。
/// </summary>
internal sealed class CommandLineOptions
{
    /// <summary>帮助文本。</summary>
    public const string HelpText = """
        SA.Collector —— 行情采集与数据源诊断

        用法：
          dotnet run --project src/SA.Collector -- [选项]

        选项：
          --probe <all|域|探针名>   逐源实测连通性并输出字段名报告。
                                    域：行情 / 资金 / 财务 / 股权 / 事件 / 舆情 / 评级 / 降级源
                                    例：--probe all   --probe 资金   --probe kline-daily-front
          --out <路径>              报告输出路径，默认 data/logs/probe-<时间戳>.md
          --title <标题>            报告标题
          --duckdb-smoke            DuckDB 最小读写样例（验证 net11.0 兼容性）
          --help                    显示本帮助

        无选项时进入调度模式（第 2 批实现）。
        """;

    /// <summary>探针选择器；为 null 表示未指定。</summary>
    public string? Probe { get; private init; }

    /// <summary>报告输出路径。</summary>
    public string? Out { get; private init; }

    /// <summary>报告标题。</summary>
    public string? ReportTitle { get; private init; }

    /// <summary>是否运行 DuckDB 最小样例。</summary>
    public bool DuckDbSmoke { get; private init; }

    /// <summary>是否显示帮助。</summary>
    public bool ShowHelp { get; private init; }

    /// <summary>
    /// 解析参数。未知参数视为错误并转帮助。
    /// </summary>
    public static CommandLineOptions Parse(string[] args)
    {
        string? probe = null;
        string? outPath = null;
        string? title = null;
        var smoke = false;
        var help = false;
        var valid = true;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--probe":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        probe = args[++i];
                    }
                    else
                    {
                        probe = "all";
                    }

                    break;
                case "--out":
                    if (i + 1 < args.Length)
                    {
                        outPath = args[++i];
                    }
                    else
                    {
                        valid = false;
                    }

                    break;
                case "--title":
                    if (i + 1 < args.Length)
                    {
                        title = args[++i];
                    }

                    break;
                case "--duckdb-smoke":
                    smoke = true;
                    break;
                case "--help":
                case "-h":
                case "/?":
                    help = true;
                    break;
                default:
                    valid = false;
                    break;
            }
        }

        return new CommandLineOptions
        {
            Probe = probe,
            Out = outPath,
            ReportTitle = title,
            DuckDbSmoke = smoke,
            ShowHelp = help || !valid
        };
    }
}
