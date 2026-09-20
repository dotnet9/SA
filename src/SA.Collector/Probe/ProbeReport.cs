using System.Text;

namespace SA.Collector.Probe;

/// <summary>
/// 探针报告输出：控制台摘要 + Markdown 明细。Markdown 用于沉淀「字段名固化」证据，
/// 也是后续各域适配器解析单测的输入。
/// </summary>
internal static class ProbeReport
{
    /// <summary>
    /// 打印单条结果（运行过程中的实时反馈），只输出一行摘要。
    /// </summary>
    public static void PrintProgress(ProbeResult result)
    {
        var status = result.Ok ? "OK " : "FAIL";
        var code = result.StatusCode?.ToString() ?? "-";
        Console.WriteLine(
            $"  [{status}] {result.Definition.Name,-24} HTTP {code,-4} {result.ElapsedMs,5}ms {result.Bytes,8}B  {result.Error ?? FirstDetail(result)}");
    }

    /// <summary>
    /// 生成 Markdown 报告全文。
    /// </summary>
    public static string Build(IReadOnlyList<ProbeResult> results, string title)
    {
        var builder = new StringBuilder();
        var ok = results.Count(r => r.Ok);
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        builder.AppendLine($"- 采集时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"- 结果：{ok}/{results.Count} 可达");
        builder.AppendLine();

        builder.AppendLine("## 汇总");
        builder.AppendLine();
        builder.AppendLine("| 探针 | 域 | 状态 | HTTP | 耗时(ms) | 字节 | 字段数 | 结论 |");
        builder.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var r in results)
        {
            builder.AppendLine(
                $"| `{r.Definition.Name}` | {r.Definition.Domain} | {(r.Ok ? "可达" : "**失败**")} | {r.StatusCode?.ToString() ?? "-"} | {r.ElapsedMs} | {r.Bytes} | {r.FieldNames.Count} | {Escape(r.Error ?? FirstDetail(r))} |");
        }

        builder.AppendLine();
        builder.AppendLine("## 明细");
        foreach (var r in results)
        {
            builder.AppendLine();
            builder.AppendLine($"### {r.Definition.Name}（{r.Definition.Domain}）");
            builder.AppendLine();
            builder.AppendLine($"- 用途：{r.Definition.Purpose}");
            builder.AppendLine($"- URL：`{r.Definition.Url}`");
            if (r.Definition.Note is not null)
            {
                builder.AppendLine($"- 备注：{r.Definition.Note}");
            }

            if (r.FieldNames.Count > 0)
            {
                builder.AppendLine($"- 字段（{r.FieldNames.Count}）：{Escape(string.Join(", ", r.FieldNames))}");
            }

            builder.AppendLine();
            builder.AppendLine("```");
            foreach (var line in r.DetailLines)
            {
                builder.AppendLine(line);
            }

            builder.AppendLine("```");
        }

        builder.AppendLine();
        builder.AppendLine("## 待人工处置");
        builder.AppendLine();
        var failed = results.Where(r => !r.Ok).ToList();
        if (failed.Count == 0)
        {
            builder.AppendLine("无。全部探针可达。");
        }
        else
        {
            foreach (var r in failed)
            {
                builder.AppendLine($"- `{r.Definition.Name}`：{r.Error}（{r.Definition.Purpose}）");
            }
        }

        return builder.ToString();
    }

    private static string FirstDetail(ProbeResult result) =>
        result.DetailLines.Count > 0 ? result.DetailLines[0] : "(无明细)";

    private static string Escape(string value) => value
        .Replace("|", "\\|")
        .Replace("\r", " ")
        .Replace("\n", " ");
}
